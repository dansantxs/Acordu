import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import type { components } from '../api/interno'
import { avisar } from '../avisos/avisos'
import { consultaCalendario } from '../calendario/consultaCalendario'
import { notificar } from '../notificacoes/navegador'
import { consultaCronometro } from './cronometro'
import { expedienteAgora } from './expediente'
import { deveLembrar, intervaloLembrete, lembreteLigadoEm, registrarLembrete, ultimoLembrete } from './lembrete'

const A_CADA_MS = 60_000
export const CAMINHO_LEMBRETE = '/tarefas?responsavel=eu'

/**
 * Mostra o lembrete (F12-13): aviso no app com "Minhas tarefas" e, se ligada, a notificação do sistema (F12c). O id
 * é o minuto do lembrete, igual nas abas que lembram juntas: uma notificação só.
 */
export function mostrarLembrete(escopo: string, agora: number, abrir: (caminho: string) => void): void {
    avisar('Nenhum cronômetro rodando. Começar agora?', {
        acao: { rotulo: 'Minhas tarefas', executar: () => abrir(CAMINHO_LEMBRETE) },
    })
    notificar(
        escopo,
        {
            id: `lembrete:${Math.floor(agora / 60_000)}`,
            titulo: 'Nenhum cronômetro rodando',
            corpo: 'Começar agora?',
            caminho: CAMINHO_LEMBRETE,
        },
        abrir,
        agora,
    )
}

/**
 * Uma conferência do lembrete (F12-13 a F12-16), separada do React para testar sem relógio falso. O calendário vem
 * primeiro: fora do expediente, o cronômetro nem é consultado (a API não acorda à noite por causa de uma aba
 * esquecida). O último lembrete fica no navegador, compartilhado entre as abas (F12-15).
 */
export function criarConferenciaLembrete(deps: {
    escopo: string
    abertoEm: number
    calendario: () => Promise<components['schemas']['CalendarioDto']>
    haCronometro: () => Promise<boolean>
    lembrar: (agora: number) => void
}) {
    let ultimoCronometro: number | null = null
    return async (agora: number) => {
        const intervalo = intervaloLembrete(deps.escopo)
        if (!intervalo) return
        const expediente = expedienteAgora(await deps.calendario(), new Date(agora))
        if (!expediente) return
        if (await deps.haCronometro()) {
            ultimoCronometro = agora
            return
        }
        const lembrar = deveLembrar({
            agora,
            intervaloMinutos: intervalo,
            inicioDoExpediente: agora - expediente.minutosDesdeInicio * 60_000,
            referencias: [deps.abertoEm, ultimoCronometro, ultimoLembrete(deps.escopo), lembreteLigadoEm(deps.escopo)],
        })
        if (!lembrar) return
        registrarLembrete(deps.escopo, agora)
        deps.lembrar(agora)
    }
}

/**
 * Lembrete no expediente: uma vez por minuto, se a pessoa ligou neste navegador. Montado uma vez, na casca, para quem
 * tem `apontamento.criar`.
 */
export function useLembreteCronometro(escopo: string, ativo: boolean, abrir: (caminho: string) => void) {
    const consultas = useQueryClient()

    useEffect(() => {
        if (!ativo) return
        let parado = false
        const conferir = criarConferenciaLembrete({
            escopo,
            // Abrir o app conta como ponto de partida: quem chega às 10h não é lembrado na hora.
            abertoEm: Date.now(),
            calendario: () => consultas.fetchQuery({ ...consultaCalendario, staleTime: 10 * 60_000 }),
            haCronometro: async () =>
                (await consultas.fetchQuery({ ...consultaCronometro, staleTime: 30_000 })) !== null,
            lembrar: (agora) => {
                if (!parado) mostrarLembrete(escopo, agora, abrir)
            },
        })
        const timer = window.setInterval(() => {
            // Lembrete é conveniência: falha de rede espera a próxima volta.
            conferir(Date.now()).catch(() => undefined)
        }, A_CADA_MS)
        return () => {
            parado = true
            window.clearInterval(timer)
        }
    }, [ativo, escopo, consultas, abrir])
}
