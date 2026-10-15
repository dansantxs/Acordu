/**
 * Instante relativo curto ("agora", "há 5 min", "há 3 h", "ontem", "12/10"), para listas como o sino. O dia é o do
 * fuso do tenant (convenções §4), não o do navegador.
 */
export function quando(instante: string, fuso: string, agora = new Date()): string {
    const data = new Date(instante)
    const segundos = Math.round((agora.getTime() - data.getTime()) / 1000)
    if (segundos < 60) return 'agora'
    if (segundos < 3600) return `há ${Math.floor(segundos / 60)} min`
    const dia = (d: Date) => new Intl.DateTimeFormat('en-CA', { timeZone: fuso }).format(d)
    if (dia(data) === dia(agora)) return `há ${Math.floor(segundos / 3600)} h`
    const ontem = new Date(agora.getTime() - 86_400_000)
    if (dia(data) === dia(ontem)) return 'ontem'
    return new Intl.DateTimeFormat('pt-BR', { timeZone: fuso, day: '2-digit', month: '2-digit' }).format(data)
}
