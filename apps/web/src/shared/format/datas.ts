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
