import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { hojeNoFuso } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, type Rotas } from '@/testes/app'

const ano = Number(hojeNoFuso('America/Sao_Paulo').slice(0, 4))
const sla = [
    { prioridade: 'URGENTE', primeiraRespostaMinutos: 60, resolucaoMinutos: 240 },
    { prioridade: 'ALTA', primeiraRespostaMinutos: 120, resolucaoMinutos: 960 },
    { prioridade: 'MEDIA', primeiraRespostaMinutos: 240, resolucaoMinutos: 1920 },
    { prioridade: 'BAIXA', primeiraRespostaMinutos: 480, resolucaoMinutos: 4800 },
]
const empresa = { nome: 'Estúdio Traço', responderPara: null, slaPadrao: sla, slaPersonalizado: false }
const janela = (diaSemana: string) => ({ diaSemana, inicio: '08:00', fim: '18:00' })
const calendario = {
    fusoHorario: 'America/Sao_Paulo',
    expediente: ['SEGUNDA', 'TERCA', 'QUARTA', 'QUINTA', 'SEXTA'].map(janela),
    feriados: [`${ano}-01-25`],
}
const portal = { nomeTenant: 'Estúdio Traço', nomeExibido: null, diasFechamentoAutomatico: 5, logoUrl: null }
const GERENCIA = ['configuracao.gerenciar']

const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/configuracoes/empresa': () => json(empresa),
        'GET /api/v1/configuracoes/calendario': () => json(calendario),
        'GET /api/v1/configuracoes/portal': () => json(portal),
        'GET /api/v1/categorias': () => json([]),
        ...rotas,
    })

describe('configurações', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('abre a primeira seção do papel e o menu só mostra as permitidas', async () => {
        app(GERENCIA)
        const roteador = await abrir('/configuracoes')
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/configuracoes/empresa'))
        const menu = await screen.findByRole('navigation', { name: 'Seções' })
        expect(
            within(menu)
                .getAllByRole('link')
                .map((l) => l.textContent),
        ).toEqual(['Empresa', 'Calendário', 'Portal', 'Categorias'])
    })

    it('só auditoria: vai direto para ela; seção sem permissão explica', async () => {
        app(['auditoria.ver'])
        const roteador = await abrir('/configuracoes')
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/configuracoes/auditoria'))
        await abrir('/configuracoes/empresa')
        expect(await screen.findByText('Sem acesso a esta seção')).toBeInTheDocument()
    })

    it('empresa salva o responder para e o SLA padrão', async () => {
        const chamadas = app(GERENCIA, {
            'PUT /api/v1/configuracoes/empresa': (corpo) => json({ ...(corpo as object), slaPersonalizado: true }),
        })
        await abrir('/configuracoes/empresa')
        const usuario = userEvent.setup()
        const resposta = await screen.findByRole('textbox', { name: /Responder para/ })
        await usuario.type(resposta, 'atendimento@estudiotraco.com.br')
        const solucao = screen.getByLabelText('Solução Urgente')
        await usuario.clear(solucao)
        await usuario.type(solucao, '6h')
        await usuario.click(screen.getByRole('button', { name: 'Salvar' }))

        expect(await screen.findByText('Dados da empresa salvos.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'PUT')?.corpo).toEqual({
            nome: 'Estúdio Traço',
            responderPara: 'atendimento@estudiotraco.com.br',
            slaPadrao: [{ ...sla[0], resolucaoMinutos: 360 }, sla[1], sla[2], sla[3]],
        })
    })

    it('calendário sugere os feriados nacionais, tira um e salva o expediente', async () => {
        const chamadas = app(GERENCIA, { 'PUT /api/v1/configuracoes/calendario': (corpo) => json(corpo) })
        await abrir('/configuracoes/calendario')
        const usuario = userEvent.setup()

        await usuario.click(await screen.findByRole('checkbox', { name: 'Sábado' }))
        const fimSabado = screen.getByLabelText('Fim de sábado')
        await usuario.clear(fimSabado)
        await usuario.type(fimSabado, '12:00')
        await usuario.click(screen.getByRole('button', { name: `Sugerir feriados nacionais de ${ano}` }))
        expect(await screen.findByText(`10 feriados de ${ano} adicionados. Confira e salve.`)).toBeInTheDocument()
        expect(screen.getByText(/Tiradentes/)).toBeInTheDocument()
        await usuario.click(screen.getByRole('button', { name: `Tirar 21/04/${ano}` }))
        await usuario.click(screen.getByRole('button', { name: 'Salvar calendário' }))

        expect(await screen.findByText('Calendário salvo. Vale para as solicitações novas.')).toBeInTheDocument()
        const corpo = chamadas.find((c) => c.metodo === 'PUT')?.corpo as typeof calendario
        expect(corpo.expediente).toHaveLength(6)
        expect(corpo.expediente[5]).toEqual({ diaSemana: 'SABADO', inicio: '08:00', fim: '12:00' })
        expect(corpo.feriados).toHaveLength(10)
        expect(corpo.feriados).toContain(`${ano}-01-25`)
        expect(corpo.feriados).not.toContain(`${ano}-04-21`)
    })

    it('portal salva o nome e os dias e recusa logotipo grande antes de enviar', async () => {
        const chamadas = app(GERENCIA, {
            'PUT /api/v1/configuracoes/portal': (corpo) => json({ ...portal, ...(corpo as object) }),
            'PUT /api/v1/configuracoes/portal/logo': () => json({ ...portal, logoUrl: 'http://localhost/logo.png' }),
        })
        await abrir('/configuracoes/portal')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByRole('textbox', { name: /Nome no portal/ }), 'Traço Design')
        const dias = screen.getByLabelText(/Fechar a solicitação resolvida/)
        await usuario.clear(dias)
        await usuario.type(dias, '10')
        await usuario.click(screen.getByRole('button', { name: 'Salvar' }))
        expect(await screen.findByText('Portal salvo.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho === '/api/v1/configuracoes/portal' && c.metodo === 'PUT')?.corpo).toEqual(
            {
                nomeExibido: 'Traço Design',
                diasFechamentoAutomatico: 10,
            },
        )

        const entrada = document.querySelector<HTMLInputElement>('input[type="file"]')!
        const grande = new File([new Uint8Array(600 * 1024)], 'logo.png', { type: 'image/png' })
        await usuario.upload(entrada, grande)
        expect(await screen.findByText('O logotipo vai até 512 KB.')).toBeInTheDocument()
        await usuario.upload(entrada, new File([new Uint8Array(10)], 'logo.png', { type: 'image/png' }))
        expect(await screen.findByRole('img', { name: 'Logotipo atual' })).toBeInTheDocument()
        expect(chamadas.filter((c) => c.caminho.endsWith('/portal/logo'))).toHaveLength(1)
    })

    it('categorias: cria, inativa com desfazer e renomeia', async () => {
        let lista = [{ id: 'k1', nome: 'Suporte', ativa: true }]
        const chamadas = app(GERENCIA, {
            'GET /api/v1/categorias': () => json(lista),
            'POST /api/v1/categorias': (corpo) => {
                const nova = { id: 'k2', nome: (corpo as { nome: string }).nome, ativa: true }
                lista = [...lista, nova]
                return json(nova, 201)
            },
            'DELETE /api/v1/categorias/k1': () => {
                lista = lista.map((c) => (c.id === 'k1' ? { ...c, ativa: false } : c))
                return new Response(null, { status: 204 })
            },
            'PATCH /api/v1/categorias/k1': (corpo) => {
                const c = corpo as { nome: string; ativa: boolean | null }
                lista = lista.map((x) => (x.id === 'k1' ? { ...x, nome: c.nome, ativa: c.ativa ?? x.ativa } : x))
                return json(lista.find((x) => x.id === 'k1'))
            },
        })
        await abrir('/configuracoes/categorias')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByRole('textbox', { name: 'Nova categoria' }), 'Ajuste')
        await usuario.click(screen.getByRole('button', { name: 'Adicionar' }))
        expect(await screen.findByText('Categoria "Ajuste" criada.')).toBeInTheDocument()

        await usuario.click(screen.getByRole('button', { name: 'Inativar Suporte' }))
        expect(await screen.findByText('"Suporte" inativada.')).toBeInTheDocument()
        expect(await screen.findByRole('button', { name: 'Ver inativas (1)' })).toBeInTheDocument()
        await usuario.click(screen.getByRole('button', { name: 'Desfazer' }))
        expect(await screen.findByRole('button', { name: 'Renomear Suporte' })).toBeInTheDocument()

        await usuario.click(screen.getByRole('button', { name: 'Renomear Suporte' }))
        const janela = await screen.findByRole('dialog', { name: 'Renomear "Suporte"' })
        const nome = within(janela).getByRole('textbox', { name: /Nome/ })
        await usuario.clear(nome)
        await usuario.type(nome, 'Suporte técnico')
        await usuario.click(within(janela).getByRole('button', { name: 'Salvar' }))
        expect(await screen.findByText('Agora é "Suporte técnico".')).toBeInTheDocument()
        expect(chamadas.filter((c) => c.metodo === 'PATCH').map((c) => c.corpo)).toEqual([
            { nome: 'Suporte', ativa: true },
            { nome: 'Suporte técnico', ativa: null },
        ])
    })
})
