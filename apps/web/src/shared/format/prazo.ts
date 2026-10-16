/**
 * Prazo relativo para listas ("vence em 3 h", "venceu há 2 dias", "vence 18/10 14:00"), no fuso do tenant. Até 24 h,
 * em horas ou minutos; depois disso, a data, que diz mais do que "em 5 dias".
 */
export function prazoRelativo(instante: string, fuso: string, agora = new Date()): string {
    const diferenca = new Date(instante).getTime() - agora.getTime()
    const minutos = Math.round(Math.abs(diferenca) / 60_000)
    const quanto =
        minutos < 60 ? `${Math.max(minutos, 1)} min` : minutos < 24 * 60 ? `${Math.floor(minutos / 60)} h` : null
    if (diferenca >= 0) {
        if (quanto) return `vence em ${quanto}`
        return `vence ${dataHora(instante, fuso)}`
    }
    if (quanto) return `venceu há ${quanto}`
    const dias = Math.floor(minutos / (24 * 60))
    return `venceu há ${dias} ${dias === 1 ? 'dia' : 'dias'}`
}

export function dataHora(instante: string, fuso: string): string {
    const partes = new Intl.DateTimeFormat('pt-BR', {
        timeZone: fuso,
        day: '2-digit',
        month: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
    }).formatToParts(new Date(instante))
    const valor = (tipo: string) => partes.find((p) => p.type === tipo)?.value ?? ''
    return `${valor('day')}/${valor('month')} ${valor('hour')}:${valor('minute')}`
}
