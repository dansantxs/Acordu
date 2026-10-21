/** Abas da ficha do cliente (decisão 1 do F4), no endereço como `?aba=`; "resumo" é o padrão e não aparece. */
export const ABAS_CLIENTE = ['resumo', 'aberto', 'contratos', 'contatos'] as const

export type AbaCliente = (typeof ABAS_CLIENTE)[number]

export const ROTULO_ABA: Record<AbaCliente, string> = {
    resumo: 'Resumo',
    aberto: 'Em aberto',
    contratos: 'Contratos',
    contatos: 'Contatos',
}
