/** Datas sem hora (`YYYY-MM-DD`, como a API manda prazo e dia planejado). O "hoje" é o do fuso do tenant. */
export function hojeNoFuso(fuso: string, agora = new Date()): string {
    return new Intl.DateTimeFormat('en-CA', { timeZone: fuso }).format(agora)
}

/** "2026-10-01" → "01/10"; com ano diferente do de referência, "01/10/25". */
export function dataCurta(data: string, referencia?: string): string {
    const [ano, mes, dia] = data.split('-')
    const mesmoAno = !referencia || referencia.slice(0, 4) === ano
    return mesmoAno ? `${dia}/${mes}` : `${dia}/${mes}/${ano!.slice(2)}`
}

/** Soma dias a uma data `YYYY-MM-DD` (sem fuso: é aritmética de calendário). */
export function somarDias(data: string, dias: number): string {
    const d = new Date(`${data}T12:00:00Z`)
    d.setUTCDate(d.getUTCDate() + dias)
    return d.toISOString().slice(0, 10)
}

/** Segunda-feira da semana da data (semana de segunda a domingo). */
export function segundaDaSemana(data: string): string {
    const diaDaSemana = new Date(`${data}T12:00:00Z`).getUTCDay()
    return somarDias(data, -((diaDaSemana + 6) % 7))
}

/** "segunda, 20/10" para cabeçalho de dia. */
export function diaPorExtenso(data: string): string {
    const d = new Date(`${data}T12:00:00Z`)
    const semana = new Intl.DateTimeFormat('pt-BR', { weekday: 'long', timeZone: 'UTC' }).format(d)
    return `${semana}, ${dataCurta(data)}`
}
