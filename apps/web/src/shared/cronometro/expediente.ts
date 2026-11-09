import type { components } from '../api/interno'

type Calendario = components['schemas']['CalendarioDto']

const DIAS: components['schemas']['DiaDaSemana'][] = [
    'DOMINGO',
    'SEGUNDA',
    'TERCA',
    'QUARTA',
    'QUINTA',
    'SEXTA',
    'SABADO',
]

/** Data (`YYYY-MM-DD`), dia da semana e minuto do dia de um instante no fuso. */
function noFuso(instante: Date, fuso: string): { data: string; dia: number; minuto: number } {
    const partes = Object.fromEntries(
        new Intl.DateTimeFormat('en-US', {
            timeZone: fuso,
            hourCycle: 'h23',
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            weekday: 'short',
            hour: '2-digit',
            minute: '2-digit',
        })
            .formatToParts(instante)
            .map((p) => [p.type, p.value]),
    ) as Record<string, string>
    const dia = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].indexOf(partes.weekday!)
    return {
        data: `${partes.year}-${partes.month}-${partes.day}`,
        dia,
        minuto: Number(partes.hour) * 60 + Number(partes.minute),
    }
}

const minutos = (hhmm: string) => {
    const [h, m] = hhmm.split(':').map(Number)
    return h! * 60 + m!
}

/**
 * Expediente da empresa agora (ADR 0003, calendário do tenant): aberto e há quantos minutos começou. Fora do
 * expediente, em dia não útil ou em feriado, `null` (F12-14).
 */
export function expedienteAgora(calendario: Calendario, agora = new Date()): { minutosDesdeInicio: number } | null {
    const { data, dia, minuto } = noFuso(agora, calendario.fusoHorario)
    if (calendario.feriados.includes(data)) return null
    const janela = calendario.expediente.find((j) => j.diaSemana === DIAS[dia])
    if (!janela) return null
    const inicio = minutos(janela.inicio)
    const fim = minutos(janela.fim)
    return minuto >= inicio && minuto < fim ? { minutosDesdeInicio: minuto - inicio } : null
}
