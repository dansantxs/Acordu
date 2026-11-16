/**
 * CNPJ válido e novo a cada execução, para o cadastro da fumaça e da demonstração do manual: um teste por documento
 * (RF-AUT-09, spec do F14, F14g), e localmente o banco fica entre uma execução e outra. Os 12 primeiros dígitos saem do
 * instante e de um sorteio; os dois verificadores, pela regra da Receita.
 */
export function cnpjNovo(): string {
    const base = `${Date.now()}${Math.floor(Math.random() * 100)}`.padStart(12, '0').slice(-12)
    const digito = (texto: string, pesos: number[]) => {
        const resto = pesos.reduce((soma, peso, i) => soma + Number(texto[i]) * peso, 0) % 11
        return resto < 2 ? 0 : 11 - resto
    }
    const d1 = digito(base, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2])
    const d2 = digito(`${base}${d1}`, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2])
    return `${base}${d1}${d2}`
}
