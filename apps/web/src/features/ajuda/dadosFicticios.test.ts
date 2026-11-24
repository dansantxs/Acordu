import { describe, expect, it } from 'vitest'

/**
 * O bot do manual lê os dados fictícios do mesmo arquivo que a API (design do F14, D13; convenções §4). As receitas das
 * capturas (`ajuda/capturas.captura.ts`) esperam textos e chaves dele: mudar o arquivo sem mudar as receitas quebra o
 * bot só no workflow; este teste avisa antes.
 */
const [dados] = Object.values(
    import.meta.glob<{
        pessoas: { chave: string; papel: string }[]
        clientes: { chave: string; nome: string }[]
        solicitacoes: { chave: string; titulo: string; passos?: { texto?: string }[] }[]
        projetos: { nome: string }[]
        tarefas: { titulo: string }[]
        apontamentos: { descricao: string }[]
        recorrencias: { titulo: string }[]
    }>('../../../../../src/Acordu.Application/Exemplos/demonstracao.json', { eager: true, import: 'default' }),
)

describe('dados fictícios do bot do manual', () => {
    it('o arquivo da API está ao alcance do bot', () => {
        expect(dados).toBeDefined()
        expect(dados!.pessoas.filter((p) => p.papel === 'Dono')).toHaveLength(1)
    })

    it('tem as chaves que o bot devolve às receitas (padaria, chamado do caixa, entrega)', () => {
        expect(dados!.clientes.map((c) => c.chave)).toContain('padaria')
        expect(dados!.solicitacoes.map((s) => s.chave)).toEqual(expect.arrayContaining(['pdv', 'qrcode']))
    })

    it('tem os textos que as receitas esperam na tela', () => {
        const textos = [
            ...dados!.clientes.map((c) => c.nome),
            ...dados!.solicitacoes.flatMap((s) => [s.titulo, ...(s.passos ?? []).map((p) => p.texto ?? '')]),
            ...dados!.projetos.map((p) => p.nome),
            ...dados!.tarefas.map((t) => t.titulo),
            ...dados!.apontamentos.map((a) => a.descricao),
            ...dados!.recorrencias.map((r) => r.titulo),
        ].join('\n')
        for (const esperado of [
            'Criar a conta de e-mail e o acesso',
            'Trocar a impressora do caixa 2',
            'Caixa não imprime o cupom desde ontem',
            'Parece a cabeça térmica',
            'Catálogo de produtos',
            'Caixa de e-mail criada',
            'Padaria Grão Dourado',
            'Novo site da Casa Verde',
            'Conferir o backup dos processos',
        ])
            expect(textos).toContain(esperado)
    })
})
