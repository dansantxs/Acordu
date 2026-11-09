import { useQueryClient } from '@tanstack/react-query'
import { useRouter } from '@tanstack/react-router'
import { useEffect } from 'react'
import { api } from '../api/cliente'
import type { components } from '../api/interno'
import { avisar } from '../avisos/avisos'
import { notificar } from '../notificacoes/navegador'
import { consultaCronometro } from './cronometro'
import { expedienteAgora } from './expediente'
import { deveLembrar, intervaloLembrete } from './lembrete'

const A_CADA_MS = 60_000
export const CAMINHO_LEMBRETE = '/tarefas?responsavel=eu'

const consultaCalendarioDoLembrete = {
    queryKey: ['configuracoes', 'calendario'] as const,
    queryFn: async () => (await api.GET('/api/v1/configuracoes/calendario')).data!,
    staleTime: 10 * 60_000,
}

/**
 * Uma conferência do lembrete (F12-13 a F12-16), separada do React para testar sem relógio falso: guarda quando o app
 * abriu, a última vez que havia cronômetro e o último lembrete.
 */
export function criarConferenciaLembrete(deps: {
    escopo: string
    abertoEm: number
    calendario: () => Promise<components['schemas']['CalendarioDto']>
    haCronometro: () => Promise<boolean>
    lembrar: (agora: number, intervalo: number) => void
}) {
    let ultimoCronometro: number | null = null
    let ultimoLembrete: number | null = null
    return async (agora: number) => {
        const intervalo = intervaloLembrete(deps.escopo)
        if (!intervalo) return
        const [calendario, ha] = await Promise.all([deps.calendario(), deps.haCronometro()])
        if (ha) {
            ultimoCronometro = agora
            return
        }
        const expediente = expedienteAgora(calendario, new Date(agora))
        const lembrar = deveLembrar({
            agora,
            intervaloMinutos: intervalo,
            inicioDoExpediente: expediente ? agora - expediente.minutosDesdeInicio * 60_000 : null,
            referencias: [deps.abertoEm, ultimoCronometro, ultimoLembrete],
        })
        if (!lembrar) return
        ultimoLembrete = agora
        deps.lembrar(agora, intervalo)
    }
}

/**
 * Lembrete no expediente: uma vez por minuto, se a pessoa ligou neste navegador, confere o calendário da empresa e o
 * cronômetro. Aviso no app e, se ligada, notificação do sistema (F12c). Montado uma vez, na casca, para quem tem
 * `apontamento.criar`.
 */
export function useLembreteCronometro(escopo: string, ativo: boolean) {
    const consultas = useQueryClient()
    const roteador = useRouter()

    useEffect(() => {
        if (!ativo) return
        let parado = false
        const abrir = (caminho: string) => roteador.history.push(caminho)
        const conferir = criarConferenciaLembrete({
            escopo,
            // Abrir o app conta como ponto de partida: quem chega às 10h não é lembrado na hora.
            abertoEm: Date.now(),
            calendario: () => consultas.fetchQuery(consultaCalendarioDoLembrete),
            haCronometro: async () =>
                (await consultas.fetchQuery({ ...consultaCronometro, staleTime: 30_000 })) !== null,
            lembrar: (agora, intervalo) => {
                if (parado) return
                avisar('Nenhum cronômetro rodando. Começar agora?', {
                    acao: { rotulo: 'Minhas tarefas', executar: () => abrir(CAMINHO_LEMBRETE) },
                })
                notificar(
                    escopo,
                    {
                        // Mesmo id nas abas abertas na mesma janela de tempo: uma notificação só.
                        id: `lembrete:${Math.floor(agora / (intervalo * 60_000))}`,
                        titulo: 'Nenhum cronômetro rodando',
                        corpo: 'Começar agora?',
                        caminho: CAMINHO_LEMBRETE,
                    },
                    abrir,
                    agora,
                )
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
    }, [ativo, escopo, consultas, roteador])
}
