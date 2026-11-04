import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, perfil, type Rotas } from '@/testes/app'

const DONO = { id: 'p1', nome: 'Dono' }
const MEMBRO = { id: 'p2', nome: 'Membro' }
const papeis = [
    { ...DONO, tipo: 'INTERNO', padrao: true, permissoes: ['cliente.ver', 'papel.gerenciar'], pessoas: 1 },
    { ...MEMBRO, tipo: 'INTERNO', padrao: true, permissoes: ['cliente.ver'], pessoas: 1 },
    { id: 'p3', nome: 'Cliente', tipo: 'CLIENTE', padrao: true, permissoes: ['portal.solicitacao.ver'], pessoas: 0 },
    { id: 'p4', nome: 'Financeiro', tipo: 'INTERNO', padrao: false, permissoes: ['contrato.ver'], pessoas: 0 },
]
const pessoas = [
    {
        usuarioId: 'u1',
        nome: 'Ana Prado',
        email: 'ana@prado.com',
        papel: DONO,
        dono: true,
        capacidadeSemanalMinutos: 2400,
    },
    {
        usuarioId: 'u2',
        nome: 'Rafael Moura',
        email: 'rafael@prado.com',
        papel: MEMBRO,
        dono: false,
        capacidadeSemanalMinutos: 2400,
    },
]
const TUDO = ['usuario.convidar', 'usuario.remover', 'papel.gerenciar']

const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/usuarios': () => json(pessoas),
        'GET /api/v1/convites': () =>
            json([
                {
                    id: 'c1',
                    email: 'bia@prado.com',
                    papel: MEMBRO,
                    criadoEm: '2026-10-20T12:00:00Z',
                    expiraEm: '2026-10-27T12:00:00Z',
                },
            ]),
        'GET /api/v1/papeis': () => json(papeis),
        'GET /api/v1/papeis/permissoes': () =>
            json(['cliente.ver', 'cliente.gerenciar', 'contrato.ver', 'auditoria.ver']),
        ...rotas,
    })

describe('equipe e papéis', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('convida como Membro por padrão e cancela convite pendente', async () => {
        const chamadas = app(TUDO, {
            'POST /api/v1/convites': (corpo) =>
                json(
                    {
                        id: 'c2',
                        email: (corpo as { email: string }).email,
                        papel: MEMBRO,
                        criadoEm: '',
                        expiraEm: null,
                    },
                    202,
                ),
            'DELETE /api/v1/convites/c1': () => new Response(null, { status: 204 }),
        })
        await abrir('/configuracoes/equipe')
        const usuario = userEvent.setup()
        const lista = await screen.findByRole('list', { name: 'Pessoas' })
        expect(within(lista).getByText('(você)')).toBeInTheDocument()
        // Ninguém remove a si mesmo pela lista.
        expect(within(lista).queryByRole('button', { name: 'Remover Ana Prado' })).not.toBeInTheDocument()
        expect(await screen.findByText('bia@prado.com')).toBeInTheDocument()

        await usuario.click(screen.getByRole('button', { name: 'Convidar pessoa' }))
        const janela = await screen.findByRole('dialog', { name: 'Convidar pessoa' })
        expect(within(janela).getByRole('combobox', { name: /Papel/ })).toHaveValue('Membro')
        await usuario.type(within(janela).getByRole('textbox', { name: /E-mail/ }), 'caio@prado.com')
        await usuario.click(within(janela).getByRole('button', { name: 'Enviar convite' }))
        expect(await screen.findByText('Convite enviado para caio@prado.com.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho === '/api/v1/convites' && c.metodo === 'POST')?.corpo).toEqual({
            email: 'caio@prado.com',
            papelId: 'p2',
        })

        await usuario.click(screen.getByRole('button', { name: 'Cancelar o convite de bia@prado.com' }))
        expect(await screen.findByText('Convite de bia@prado.com cancelado.')).toBeInTheDocument()
    })

    it('sem papel.gerenciar, o convite não oferece o papel Dono', async () => {
        app(['usuario.convidar'])
        await abrir('/configuracoes/equipe')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Convidar pessoa' }))
        const janela = await screen.findByRole('dialog', { name: 'Convidar pessoa' })
        await usuario.click(within(janela).getByRole('combobox', { name: /Papel/ }))
        const opcoes = (await screen.findAllByRole('option')).map((o) => o.textContent)
        expect(opcoes).toContain('Financeiro')
        expect(opcoes).not.toContain('Dono')
    })

    it('troca papel, capacidade mostra o que vai ficar e remover pede confirmação', async () => {
        const chamadas = app(TUDO, {
            'PATCH /api/v1/usuarios/u2/papel': () => json({ ...pessoas[1], papel: { id: 'p4', nome: 'Financeiro' } }),
            'PUT /api/v1/usuarios/u2/capacidade': (corpo) => json({ ...pessoas[1], ...(corpo as object) }),
            'DELETE /api/v1/usuarios/u2': () => new Response(null, { status: 204 }),
        })
        await abrir('/configuracoes/equipe')
        const usuario = userEvent.setup()

        await usuario.click(await screen.findByRole('button', { name: 'Trocar o papel de Rafael Moura' }))
        let janela = await screen.findByRole('dialog', { name: 'Papel de Rafael Moura' })
        await usuario.click(within(janela).getByRole('combobox', { name: /Papel/ }))
        await usuario.click(await screen.findByRole('option', { name: /Financeiro/ }))
        await usuario.click(within(janela).getByRole('button', { name: 'Salvar' }))
        expect(
            await screen.findByText('Rafael Moura agora é Financeiro. Vale a partir do próximo acesso.'),
        ).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'PATCH')?.corpo).toEqual({ papelId: 'p4' })

        await usuario.click(screen.getByRole('button', { name: 'Capacidade de Rafael Moura' }))
        janela = await screen.findByRole('dialog', { name: 'Capacidade de Rafael Moura' })
        const horas = within(janela).getByLabelText('Horas por semana')
        await usuario.clear(horas)
        await usuario.type(horas, '30')
        expect(within(janela).getByText(/Fica 30min por semana/)).toBeInTheDocument()
        await usuario.type(horas, 'h')
        await usuario.click(within(janela).getByRole('button', { name: 'Salvar' }))
        expect(await screen.findByText('Rafael Moura: 30h por semana.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'PUT')?.corpo).toEqual({ capacidadeSemanalMinutos: 1800 })

        await usuario.click(screen.getByRole('button', { name: 'Remover Rafael Moura' }))
        const confirmacao = await screen.findByRole('dialog', { name: 'Remover Rafael Moura da equipe?' })
        await usuario.click(within(confirmacao).getByRole('button', { name: 'Remover da equipe' }))
        expect(await screen.findByText('Rafael Moura saiu da equipe.')).toBeInTheDocument()
    })

    it('só papel.gerenciar: a seção explica que a lista pede outra permissão', async () => {
        app(['papel.gerenciar'])
        await abrir('/configuracoes/equipe')
        expect(await screen.findByText('A lista da equipe pede outra permissão')).toBeInTheDocument()
    })

    it('papel padrão só se vê; sob medida nasce copiando outro e salva as permissões marcadas', async () => {
        const chamadas = app(TUDO, {
            'POST /api/v1/papeis': (corpo) =>
                json({ id: 'p5', tipo: 'INTERNO', padrao: false, pessoas: 0, ...(corpo as object) }, 201),
        })
        await abrir('/configuracoes/papeis')
        const usuario = userEvent.setup()
        const lista = await screen.findByRole('list', { name: 'Papéis' })
        expect(within(lista).queryByRole('button', { name: 'Editar Dono' })).not.toBeInTheDocument()
        await usuario.click(within(lista).getByRole('button', { name: 'Ver as permissões de Dono' }))
        const ver = await screen.findByRole('dialog', { name: 'Dono' })
        expect(within(ver).getByText('Criar papéis, trocar o papel e a capacidade das pessoas')).toBeInTheDocument()
        await usuario.click(within(ver).getByRole('button', { name: 'Fechar' }))

        await usuario.click(screen.getByRole('button', { name: 'Novo papel' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo papel' })
        await usuario.type(within(janela).getByRole('textbox', { name: /Nome/ }), 'Atendimento')
        await usuario.click(within(janela).getByRole('combobox', { name: /Começar com/ }))
        await usuario.click(await screen.findByRole('option', { name: 'Membro' }))
        expect(await screen.findByRole('checkbox', { name: 'Ver clientes' })).toBeChecked()
        await usuario.click(within(janela).getByRole('button', { name: 'Marcar tudo em Contratos' }))
        await usuario.click(within(janela).getByRole('button', { name: 'Criar papel' }))
        expect(await screen.findByText('Papel "Atendimento" criado.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/papeis')?.corpo).toEqual({
            nome: 'Atendimento',
            permissoes: ['cliente.ver', 'contrato.ver'],
        })
    })

    it('excluir papel em uso explica o que fazer', async () => {
        app(TUDO, {
            'DELETE /api/v1/papeis/p4': () =>
                json({ status: 422, title: 'Regra', detail: 'Papel em uso.', type: 'validacao' }, 422),
        })
        await abrir('/configuracoes/papeis')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Excluir Financeiro' }))
        const confirmacao = await screen.findByRole('dialog', { name: 'Excluir o papel "Financeiro"?' })
        await usuario.click(within(confirmacao).getByRole('button', { name: 'Excluir papel' }))
        expect(await within(confirmacao).findByText(/Troque o papel delas na seção Equipe/)).toBeInTheDocument()
    })

    it('empresa não liberada: convidar fica desabilitado, com a explicação (F9-43)', async () => {
        app(TUDO, {
            '/api/v1/auth/eu': () =>
                json({ ...perfil, permissoes: TUDO, tenant: { ...perfil.tenant, envioLiberado: false } }),
        })
        await abrir('/configuracoes/equipe')
        expect(await screen.findByRole('button', { name: 'Convidar pessoa' })).toBeDisabled()
        expect(screen.getByText('Disponível depois que você confirmar seu e-mail.')).toBeInTheDocument()
    })

    it('mostra a mensagem da API quando o convite é recusado por e-mail não confirmado (F9-44)', async () => {
        app(TUDO, {
            'POST /api/v1/convites': () =>
                json(
                    {
                        type: 'https://acordu.app.br/erros/email-nao-confirmado',
                        status: 422,
                        detail: 'Confirme o e-mail de quem criou a empresa para enviar convites e links.',
                    },
                    422,
                ),
        })
        await abrir('/configuracoes/equipe')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Convidar pessoa' }))
        const janela = await screen.findByRole('dialog', { name: 'Convidar pessoa' })
        await usuario.type(within(janela).getByRole('textbox', { name: /E-mail/ }), 'caio@prado.com')
        await usuario.click(within(janela).getByRole('button', { name: 'Enviar convite' }))
        expect(await within(janela).findByText(/Confirme o e-mail de quem criou a empresa/)).toBeInTheDocument()
    })
})
