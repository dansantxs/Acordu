import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fraseAuditoria, type Registro } from '@/features/auditoria/acoes'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json } from '@/testes/app'

const K = '0199f0a0-0000-7000-8000-0000000000e1'
const U2 = '0199f0a0-0000-7000-8000-0000000000a2'
const registro = (mudancas: Partial<Registro>): Registro => ({
    id: Math.random().toString(36).slice(2),
    ocorridoEm: '2026-10-21T17:02:00Z',
    acao: 'login.sucesso',
    usuario: { id: 'u1', nome: 'Ana Prado' },
    alvoTipo: null,
    alvoId: null,
    dados: null,
    ip: '177.10.2.3',
    ...mudancas,
})
const nomes = new Map([[U2, 'Rafael Moura']])

describe('auditoria', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('monta a frase das ações conhecidas e cai no rótulo nas outras', () => {
        expect(
            fraseAuditoria(
                registro({ acao: 'equipe.papel.troca', alvoId: U2, dados: { de: 'Membro', para: 'Leitor' } }),
                nomes,
            ),
        ).toBe('trocou o papel de Rafael Moura de Membro para Leitor')
        expect(
            fraseAuditoria(
                registro({ acao: 'contrato.ajuste_saldo', dados: { quantidadeMinutos: -120, motivo: 'Correção' } }),
                nomes,
            ),
        ).toBe('descontou 2h no saldo: "Correção"')
        expect(
            fraseAuditoria(
                registro({ acao: 'solicitacao.status', dados: { de: 'ABERTA', para: 'EM_ANDAMENTO' } }),
                nomes,
            ),
        ).toBe('mudou a solicitação de aberta para em andamento')
        expect(
            fraseAuditoria(
                registro({ acao: 'equipe.capacidade', alvoId: 'x', dados: { de: 2400, para: 1800 } }),
                nomes,
            ),
        ).toBe('mudou a capacidade de pessoa que saiu da equipe de 40h para 30h por semana')
        expect(fraseAuditoria(registro({ acao: 'algo.novo' }), nomes)).toBe('algo.novo')
    })

    it('filtra pelo endereço, mostra o que mudou e abre o histórico do item', async () => {
        const chamadas = comPerfil(['auditoria.ver'], {
            'GET /api/v1/equipe': () =>
                json([
                    { id: 'u1', nome: 'Ana Prado' },
                    { id: U2, nome: 'Rafael Moura' },
                ]),
            'GET /api/v1/auditoria': () =>
                json({
                    itens: [
                        registro({
                            acao: 'contrato.edicao',
                            alvoTipo: 'contrato',
                            alvoId: K,
                            dados: {
                                antes: { franquiaMinutos: 1200, fim: null },
                                depois: { franquiaMinutos: 1800, fim: null },
                            },
                        }),
                        registro({
                            usuario: null,
                            acao: 'solicitacao.status',
                            dados: { de: 'RESOLVIDA', para: 'FECHADA' },
                        }),
                    ],
                    pagina: 1,
                    tamanhoPagina: 50,
                    total: 2,
                }),
        })
        const roteador = await abrir('/configuracoes/auditoria?acao=contrato.edicao')
        const lista = await screen.findByRole('list', { name: 'Registros' })
        expect(chamadas.find((c) => c.caminho === '/api/v1/auditoria')?.consulta.get('acao')).toBe('contrato.edicao')
        expect(within(lista).getByText('editou os termos do contrato')).toBeInTheDocument()
        expect(within(lista).getByText('Sistema')).toBeInTheDocument()
        expect(within(lista).getByRole('link', { name: 'Abrir o contrato' })).toHaveAttribute('href', `/contratos/${K}`)
        const usuario = userEvent.setup()

        await usuario.click(within(lista).getAllByRole('button', { name: 'Detalhes' })[0]!)
        expect(within(lista).getByText('franquiaMinutos')).toBeInTheDocument()
        expect(within(lista).queryByText('fim')).not.toBeInTheDocument()

        await usuario.click(within(lista).getByRole('button', { name: 'Histórico deste item' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ acao: 'contrato.edicao', alvo: K }))
        await usuario.click(await screen.findByRole('button', { name: 'Limpar filtros' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({}))
    })
})
