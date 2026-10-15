/** "Bom dia" / "Boa tarde" / "Boa noite" pela hora no fuso do tenant. */
export function saudacao(fuso: string, agora = new Date()): string {
    const hora = Number(
        new Intl.DateTimeFormat('en-US', { timeZone: fuso, hour: 'numeric', hourCycle: 'h23' }).format(agora),
    )
    return hora < 12 ? 'Bom dia' : hora < 18 ? 'Boa tarde' : 'Boa noite'
}

/** "Quinta, 15 de outubro", no fuso do tenant. */
export function diaPorExtenso(fuso: string, agora = new Date()): string {
    const texto = new Intl.DateTimeFormat('pt-BR', { timeZone: fuso, weekday: 'long', day: 'numeric', month: 'long' })
        .format(agora)
        .replace('-feira', '')
    return texto.charAt(0).toUpperCase() + texto.slice(1)
}
