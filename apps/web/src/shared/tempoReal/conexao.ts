import type { QueryClient } from '@tanstack/react-query'
import { renovar, tokenAtual } from '../sessao/sessao'
import { chavesAfetadas } from './invalidacao'
import { LeitorSse } from './sse'

/** Espera entre tentativas: o `retry` da API (5 s) dobrando a cada falha seguida, até 60 s. */
const ESPERA_MAXIMA = 60_000
/** Sinais que chegam juntos (um arrasto mexe em vários itens) viram uma invalidação só. */
const JANELA_MS = 300

export interface OpcoesConexao {
    consultas: QueryClient
    url?: string
    /** Para testes: espera entre tentativas. */
    esperar?: (ms: number) => Promise<void>
}

/**
 * Mantém a conexão com `GET /api/v1/eventos` enquanto `ativa`. Cada sinal invalida as consultas do recurso e das
 * visões; ao reconectar depois de uma queda, invalida tudo, porque os sinais do intervalo se perderam. Devolve a função
 * que encerra.
 */
export function conectarTempoReal({
    consultas,
    url = `${globalThis.location.origin}/api/v1/eventos`,
    esperar,
}: OpcoesConexao): () => void {
    let ativa = true
    let controle: AbortController | null = null
    let pendentes = new Set<string>()
    let temporizador: ReturnType<typeof setTimeout> | undefined
    const dormir = esperar ?? ((ms: number) => new Promise<void>((r) => setTimeout(r, ms)))

    function agendarInvalidacao(tipo: string) {
        pendentes.add(tipo)
        if (temporizador) return
        temporizador = setTimeout(() => {
            const tipos = pendentes
            pendentes = new Set()
            temporizador = undefined
            for (const chave of chavesAfetadas(tipos)) void consultas.invalidateQueries({ queryKey: [chave] })
        }, JANELA_MS)
    }

    async function ciclo() {
        let falhas = 0
        let jaConectou = false
        let espera = 5_000
        while (ativa) {
            controle = new AbortController()
            try {
                const resposta = await fetch(
                    new Request(url, {
                        headers: { Accept: 'text/event-stream', Authorization: `Bearer ${tokenAtual() ?? ''}` },
                        signal: controle.signal,
                        cache: 'no-store',
                    }),
                )
                if (resposta.status === 401) {
                    // Token vencido: renova e tenta de novo; sessão encerrada desliga o tempo real (a casca sai da tela).
                    if (!(await renovar())) return
                    continue
                }
                if (resposta.status === 403) return // contato de cliente: o portal não recebe sinais
                if (!resposta.ok || !resposta.body) throw new Error(`eventos: ${resposta.status}`)

                if (jaConectou) void consultas.invalidateQueries()
                jaConectou = true
                falhas = 0
                const leitor = new LeitorSse()
                const decodificador = new TextDecoder()
                const fluxo = resposta.body.getReader()
                for (;;) {
                    const { value, done } = await fluxo.read()
                    if (done) break
                    for (const mensagem of leitor.alimentar(decodificador.decode(value, { stream: true }))) {
                        if (mensagem.evento !== 'mudanca') continue
                        try {
                            const sinal = JSON.parse(mensagem.dados) as { tipo?: string }
                            if (sinal.tipo) agendarInvalidacao(sinal.tipo)
                        } catch {
                            // sinal malformado: ignora
                        }
                    }
                    if (leitor.retry) espera = leitor.retry
                }
            } catch {
                if (!ativa) return
                falhas++
            }
            if (!ativa) return
            await dormir(Math.min(espera * 2 ** Math.max(falhas - 1, 0), ESPERA_MAXIMA))
        }
    }

    void ciclo()
    return () => {
        ativa = false
        controle?.abort()
        clearTimeout(temporizador)
    }
}
