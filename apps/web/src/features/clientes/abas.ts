export const ABAS_CLIENTE = ['resumo', 'aberto', 'contratos', 'contatos', 'arquivos', 'movimentos'] as const
export type AbaCliente = (typeof ABAS_CLIENTE)[number]
export const ROTULO_ABA: Record<AbaCliente, string> = {
    resumo: 'Resumo',
    aberto: 'Em aberto',
    contratos: 'Contratos',
    contatos: 'Contatos e portal',
    arquivos: 'Arquivos',
    movimentos: 'Movimentos',
}
