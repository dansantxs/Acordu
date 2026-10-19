import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, perfil, type Rotas } from '@/testes/app'

// Ids no formato real: a rota descarta do endereço o que não é GUID.
const RAFAEL = '0199f0a0-0000-7000-8000-000000000002'
const PADARIA = '0199f0a0-0000-7000-8000-0000000000c1'

const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })
const daquiA = (horas: number) => new Date(Date.now() + horas * 3_600_000).toISOString()

const impressora = {
    id: 's1',
    numero: 42,
    titulo: 'Impressora da recepção',
    cliente: { id: PADARIA, nome: 'Padaria Bom Grão' },
    prioridade: 'URGENTE',
    status: 'EM_ANDAMENTO',
    statusExterno: 'EM_ANDAMENTO',
    responsavel: { id: 'u1', nome: 'Ana Prado' },
    abertaEm: daquiA(-5),
    prazoResolucao: daquiA(-2),
    situacaoSla: 'VIOLADO',
}

const app = (rotas: Rotas = {}) =>
    comPerfil(['solicitacao.ver', 'cliente.ver'], {
        'GET /api/v1/equipe': () =>
            json([
                { id: perfil.usuarioId, nome: 'Ana Prado' },
                { id: RAFAEL, nome: 'Rafael Moura' },
            ]),
        'GET /api/v1/categorias': () => json([]),
        'GET /api/v1/clientes': (_, url) =>
            pagina(
                url.searchParams.get('busca') === 'pada'
                    ? [
                          {
                              id: PADARIA,
                              nome: 'Padaria Bom Grão',
                              documento: null,
                              ativo: true,
                              razaoSocial: null,
                              responsavel: null,
                              etiquetas: [],
                          },
                      ]
                    : [],
            ),
        ...rotas,
    })

const ultimaFila = (chamadas: ReturnType<typeof app>) =>
    chamadas.filter((c) => c.caminho === '/api/v1/solicitacoes').at(-1)

describe('fila de solicitações', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('abre em aberto, com o prazo vencido em destaque', async () => {
        const chamadas = app({ 'GET /api/v1/solicitacoes': () => pagina([impressora]) })
        await abrir('/solicitacoes')
        const linha = (await screen.findByRole('link', { name: 'Impressora da recepção' })).closest('tr')!
        expect(within(linha).getByText('Urgente')).toBeInTheDocument()
        expect(within(linha).getByText('Em andamento')).toBeInTheDocument()
        expect(within(linha).getByText('venceu há 2 h')).toHaveClass('text-vermelho')
        expect(within(linha).getByText('Violado:')).toHaveClass('sr-only')
        expect(ultimaFila(chamadas)?.consulta.get('status')).toBe(
            'ABERTA,EM_ANDAMENTO,AGUARDANDO_CLIENTE,AGUARDANDO_TERCEIRO',
        )
    })

    it('visão, "comigo" e prazo vão para o endereço e para a API', async () => {
        const chamadas = app({ 'GET /api/v1/solicitacoes': () => pagina([]) })
        const roteador = await abrir('/solicitacoes')
        await screen.findByText('Nada em aberto')
        const usuario = userEvent.setup()
        await usuario.click(screen.getByRole('tab', { name: 'Resolvidas' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ visao: 'resolvidas' }))
        await usuario.type(screen.getByRole('combobox', { name: 'Responsável' }), 'comi')
        await usuario.click(await screen.findByRole('option', { name: 'Comigo' }))
        await usuario.type(screen.getByRole('combobox', { name: 'Prazo' }), 'viol')
        await usuario.click(await screen.findByRole('option', { name: 'Violado' }))
        await vi.waitFor(() =>
            expect(roteador.state.location.search).toEqual({ visao: 'resolvidas', responsavel: 'eu', sla: 'VIOLADO' }),
        )
        await vi.waitFor(() => {
            const consulta = ultimaFila(chamadas)?.consulta
            expect(consulta?.get('status')).toBe('RESOLVIDA,FECHADA')
            expect(consulta?.get('responsavelId')).toBe(perfil.usuarioId)
            expect(consulta?.get('situacaoSla')).toBe('VIOLADO')
        })
    })

    it('"sem responsável" pede à API só as de ninguém', async () => {
        const chamadas = app({ 'GET /api/v1/solicitacoes': () => pagina([]) })
        const roteador = await abrir('/solicitacoes')
        await screen.findByText('Nada em aberto')
        const usuario = userEvent.setup()
        await usuario.type(screen.getByRole('combobox', { name: 'Responsável' }), 'sem')
        await usuario.click(await screen.findByRole('option', { name: 'Sem responsável' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ responsavel: 'ninguem' }))
        await vi.waitFor(() => expect(ultimaFila(chamadas)?.consulta.get('semResponsavel')).toBe('true'))
        expect(ultimaFila(chamadas)?.consulta.has('responsavelId')).toBe(false)
    })

    it('o filtro de cliente busca na API enquanto se digita', async () => {
        const chamadas = app({ 'GET /api/v1/solicitacoes': () => pagina([]) })
        const roteador = await abrir('/solicitacoes')
        await screen.findByText('Nada em aberto')
        const usuario = userEvent.setup()
        await usuario.type(screen.getByRole('combobox', { name: 'Cliente' }), 'pada')
        await usuario.click(await screen.findByRole('option', { name: 'Padaria Bom Grão' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ cliente: PADARIA }))
        expect(ultimaFila(chamadas)?.consulta.get('clienteId')).toBe(PADARIA)
        expect(chamadas.some((c) => c.caminho === '/api/v1/clientes' && c.consulta.get('ativo') === 'true')).toBe(true)
    })

    it('a busca vai para o endereço e para a API', async () => {
        const chamadas = app({ 'GET /api/v1/solicitacoes': () => pagina([]) })
        const roteador = await abrir('/solicitacoes')
        await screen.findByText('Nada em aberto')
        await userEvent.setup().type(screen.getByRole('searchbox', { name: 'Buscar solicitações' }), '#42')
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ busca: '#42' }))
        await vi.waitFor(() => expect(ultimaFila(chamadas)?.consulta.get('busca')).toBe('#42'))
    })

    it('filtros desconhecidos no endereço são ignorados', async () => {
        const chamadas = app({ 'GET /api/v1/solicitacoes': () => pagina([]) })
        const roteador = await abrir('/solicitacoes?visao=xyz&prioridade=MAXIMA&sla=OK&ordem=titulo&responsavel=abc')
        await screen.findByText('Nada em aberto')
        expect(roteador.state.location.search).toEqual({})
        expect(ultimaFila(chamadas)?.consulta.get('prioridade')).toBeNull()
    })

    it('com filtro e sem resultado, oferece limpar', async () => {
        app({ 'GET /api/v1/solicitacoes': () => pagina([]) })
        const roteador = await abrir('/solicitacoes?visao=todas&prioridade=ALTA')
        expect(await screen.findByText('Nenhuma solicitação com esses filtros')).toBeInTheDocument()
        await userEvent.setup().click(screen.getByRole('button', { name: 'Limpar filtros' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ visao: 'todas' }))
    })
})
