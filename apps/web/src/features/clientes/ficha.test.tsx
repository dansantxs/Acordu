import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mesAtual } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json } from '@/testes/app'

const C = '0199f0a0-0000-7000-8000-0000000000c1'
const K = '0199f0a0-0000-7000-8000-0000000000e1'
const mes = mesAtual('America/Sao_Paulo')
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })
const cliente = {
    id: C,
    nome: 'Padaria Bom Grão',
    documento: null,
    observacoes: null,
    ativo: true,
    versao: '1',
    razaoSocial: null,
    inscricaoEstadual: null,
    email: null,
    telefone: null,
    endereco: null,
    responsavel: null,
    etiquetas: [],
    contatos: [],
}
const resumoContrato = {
    id: K,
    clienteId: C,
    clienteNome: 'Padaria Bom Grão',
    tipo: 'BANCO_HORAS',
    inicio: '2026-01-01',
    fim: null,
    franquiaMinutos: 1800,
    status: 'ATIVO',
}

const app = (permissoes: string[]) =>
    comPerfil(permissoes, {
        'GET /api/v1/equipe': () => json([]),
        [`GET /api/v1/clientes/${C}`]: () => json(cliente),
        'GET /api/v1/contratos': (_, url) =>
            pagina(
                url.searchParams.get('status') === 'ATIVO'
                    ? [resumoContrato]
                    : [
                          resumoContrato,
                          {
                              ...resumoContrato,
                              id: 'k2',
                              tipo: 'MENSAL_FIXO',
                              franquiaMinutos: null,
                              status: 'ENCERRADO',
                          },
                      ],
            ),
        [`GET /api/v1/contratos/${K}`]: () =>
            json({ ...resumoContrato, sla: [], periodosFechados: [], versao: '1', consumoAtual: null }),
        [`GET /api/v1/contratos/${K}/consumo`]: () =>
            json({
                contratoId: K,
                tipo: 'BANCO_HORAS',
                periodo: mes,
                franquiaMinutos: 1800,
                consumidoMinutos: 180,
                naoFaturavelMinutos: 0,
                ajustesMinutos: 0,
                consumidoAcumuladoMinutos: 720,
                saldoMinutos: 1080,
                percentual: 40,
                periodoFechado: false,
            }),
        'GET /api/v1/solicitacoes': () =>
            pagina([{ id: 's1', numero: 12, titulo: 'Impressora parou', status: 'ABERTA' }]),
        'GET /api/v1/tarefas': () => pagina([{ id: 't1', titulo: 'Trocar cabo', status: 'FAZENDO', prazo: null }]),
    })

const TUDO = ['cliente.ver', 'contrato.ver', 'contrato.gerenciar', 'solicitacao.ver', 'tarefa.ver']

describe('ficha do cliente', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('o resumo mostra a régua dos contratos ativos', async () => {
        const chamadas = app(TUDO)
        await abrir(`/clientes/${C}`)
        expect(await screen.findByText('12h usadas de 30h')).toBeInTheDocument()
        expect(screen.getByRole('link', { name: 'Banco de horas' })).toHaveAttribute('href', `/contratos/${K}`)
        expect(chamadas.find((c) => c.caminho === '/api/v1/contratos')?.consulta.get('clienteId')).toBe(C)
    })

    it('em aberto lista solicitações e tarefas pendentes do cliente', async () => {
        const chamadas = app(TUDO)
        await abrir(`/clientes/${C}?secao=aberto`)
        expect(await screen.findByRole('link', { name: /Impressora parou/ })).toHaveAttribute(
            'href',
            '/solicitacoes/s1',
        )
        expect(await screen.findByRole('link', { name: 'Trocar cabo' })).toHaveAttribute('href', '/tarefas/t1')
        const tarefas = chamadas.find((c) => c.caminho === '/api/v1/tarefas')!.consulta
        expect([tarefas.get('clienteId'), tarefas.get('status')]).toEqual([C, 'A_FAZER,FAZENDO'])
    })

    it('a aba contratos lista todas as situações e abre o cadastro com o cliente', async () => {
        app(TUDO)
        await abrir(`/clientes/${C}?secao=contratos`)
        const usuario = userEvent.setup()
        expect(await screen.findByRole('link', { name: 'Mensal fixo' })).toBeInTheDocument()
        expect(screen.getByText('Encerrado')).toBeInTheDocument()
        await usuario.click(screen.getByRole('button', { name: 'Novo contrato' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo contrato' })
        expect(await within(janela).findByRole('combobox', { name: 'Cliente' })).toHaveValue('Padaria Bom Grão')
    })

    it('sem contrato.ver, não há aba de contratos nem régua', async () => {
        app(['cliente.ver'])
        await abrir(`/clientes/${C}`)
        expect(await screen.findByRole('tab', { name: 'Contatos e portal' })).toBeInTheDocument()
        expect(screen.queryByRole('tab', { name: 'Contratos' })).not.toBeInTheDocument()
        expect(screen.queryByText('Contratos ativos')).not.toBeInTheDocument()
    })
})

describe('ficha do cliente: falar no WhatsApp (#208)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    const contato = (id: string, nome: string, telefone: string | null) => ({
        id,
        nome,
        email: `${id}@bomgrao.com`,
        telefone,
        cargo: null,
        ativo: true,
        temConta: false,
        acessoPortal: null,
    })
    const comTelefones = (telefone: string | null) =>
        comPerfil(['cliente.ver'], {
            'GET /api/v1/equipe': () => json([]),
            [`GET /api/v1/clientes/${C}`]: () =>
                json({
                    ...cliente,
                    telefone,
                    contatos: [
                        contato('k1', 'Marta Souza', '(14) 99999-0000'),
                        contato('k2', 'Rui Lima', null),
                        contato('k3', 'Lia Prado', '1234'),
                    ],
                }),
            'GET /api/v1/contratos': () => pagina([]),
        })

    it('LinkWhatsApp_ClienteComTelefone_AbreOWaMeNumaAbaNova', async () => {
        comTelefones('(14) 3222-0101')
        await abrir(`/clientes/${C}`)
        const link = await screen.findByRole('link', {
            name: 'Falar no WhatsApp com Padaria Bom Grão (abre em outra aba)',
        })
        expect(link).toHaveAttribute('href', 'https://wa.me/551432220101')
        expect(link).toHaveAttribute('target', '_blank')
        expect(link).toHaveAttribute('rel', 'noopener noreferrer')
    })

    it('LinkWhatsApp_ClienteSemTelefone_NaoAparece', async () => {
        comTelefones(null)
        await abrir(`/clientes/${C}`)
        expect(await screen.findByText('Razão social')).toBeInTheDocument()
        expect(screen.queryByRole('link', { name: /WhatsApp/ })).not.toBeInTheDocument()
    })

    it('LinkWhatsApp_ClienteComTelefoneInvalido_NaoAparece', async () => {
        comTelefones('99-0000')
        await abrir(`/clientes/${C}`)
        expect(await screen.findByText('(99) 0000')).toBeInTheDocument()
        expect(screen.queryByRole('link', { name: /WhatsApp/ })).not.toBeInTheDocument()
    })

    it('LinkWhatsApp_ContatosNaFicha_SoQuemTemTelefoneQueServe', async () => {
        comTelefones(null)
        await abrir(`/clientes/${C}?secao=contatos`)
        const link = await screen.findByRole('link', { name: 'Falar no WhatsApp com Marta Souza (abre em outra aba)' })
        expect(link).toHaveAttribute('href', 'https://wa.me/5514999990000')
        expect(link).toHaveAttribute('target', '_blank')
        expect(screen.getAllByRole('link', { name: /WhatsApp/ })).toHaveLength(1)
    })
})
