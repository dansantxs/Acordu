import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comSessao, json, perfil, semSessao, servidor } from '@/testes/app'

describe('telas de conta', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        sessionStorage.clear()
    })
    afterEach(() => vi.unstubAllGlobals())

    it('o link do convite sai da URL e a conta nova entra na equipe', async () => {
        const chamadas = servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/convite/consulta': () =>
                json({ nomeTenant: 'Prado Consultoria', email: 'bia@exemplo.com', papel: 'Técnico', temConta: false }),
            '/api/v1/auth/convite': () => comSessao(),
            '/api/v1/auth/eu': () => json(perfil),
        })
        const roteador = await abrir('/convite/tok-convite')
        expect(await screen.findByText('Prado Consultoria')).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe('/convite')

        const usuario = userEvent.setup()
        await usuario.type(screen.getByLabelText('Seu nome'), 'Bia')
        await usuario.type(screen.getByLabelText('Crie uma senha'), 'umasenhaboa')
        await usuario.click(screen.getByRole('button', { name: 'Entrar na equipe' }))
        expect(await screen.findByRole('heading', { level: 1, name: /, Ana$/ })).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho === '/api/v1/auth/convite')?.corpo).toEqual({
            token: 'tok-convite',
            nome: 'Bia',
            senha: 'umasenhaboa',
        })
        expect(sessionStorage.getItem('acordu.link.convite')).toBeNull()
    })

    it('convite de quem já tem conta pede só a senha', async () => {
        servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/convite/consulta': () =>
                json({ nomeTenant: 'Prado Consultoria', email: 'ana@exemplo.com', papel: 'Gestor', temConta: true }),
        })
        await abrir('/convite/tok')
        expect(await screen.findByLabelText('Senha da sua conta')).toBeInTheDocument()
        expect(screen.queryByLabelText('Seu nome')).not.toBeInTheDocument()
    })

    it('convite vencido explica e não mostra formulário', async () => {
        servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/convite/consulta': () => json({ status: 401 }, 401),
        })
        await abrir('/convite/velho')
        expect(await screen.findByRole('alert')).toHaveTextContent('Este convite não vale mais')
    })

    it('esqueci a senha responde igual, tenha conta ou não', async () => {
        servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/senha/esqueci': () => new Response(null, { status: 202 }),
        })
        await abrir('/conta/senha')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByLabelText('E-mail da conta'), 'quem@exemplo.com')
        await usuario.click(screen.getByRole('button', { name: 'Enviar link' }))
        expect(await screen.findByRole('status')).toHaveTextContent('Se existir uma conta com esse e-mail')
    })

    it('senha nova: confere a repetição e manda o token do link', async () => {
        const chamadas = servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/senha/redefinir': () => new Response(null, { status: 204 }),
        })
        const roteador = await abrir('/conta/senha/tok-senha')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByLabelText('Senha nova'), 'senhanova123')
        await usuario.type(screen.getByLabelText('Repita a senha nova'), 'outracoisa1')
        await usuario.click(screen.getByRole('button', { name: 'Trocar senha' }))
        expect(await screen.findByText('As senhas não são iguais.')).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe('/conta/senha/nova')

        await usuario.clear(screen.getByLabelText('Repita a senha nova'))
        await usuario.type(screen.getByLabelText('Repita a senha nova'), 'senhanova123')
        await usuario.click(screen.getByRole('button', { name: 'Trocar senha' }))
        expect(await screen.findByRole('status')).toHaveTextContent('Senha trocada')
        expect(chamadas.find((c) => c.caminho === '/api/v1/auth/senha/redefinir')?.corpo).toEqual({
            token: 'tok-senha',
            novaSenha: 'senhanova123',
        })
    })

    it('senha nova com link recusado oferece pedir outro', async () => {
        servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/senha/redefinir': () => json({ status: 401 }, 401),
        })
        await abrir('/conta/senha/usado')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByLabelText('Senha nova'), 'senhanova123')
        await usuario.type(screen.getByLabelText('Repita a senha nova'), 'senhanova123')
        await usuario.click(screen.getByRole('button', { name: 'Trocar senha' }))
        expect(await screen.findByRole('link', { name: 'Pedir um link novo' })).toBeInTheDocument()
    })

    it('confirma o e-mail ao abrir o link, uma vez só', async () => {
        const chamadas = servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/confirmacao-email': () => new Response(null, { status: 204 }),
        })
        await abrir('/conta/confirmacao/tok-email')
        expect(await screen.findByRole('status')).toHaveTextContent('E-mail confirmado')
        expect(chamadas.filter((c) => c.caminho === '/api/v1/auth/confirmacao-email')).toHaveLength(1)
    })

    it('no app, lembra de confirmar o e-mail e reenvia o link', async () => {
        const chamadas = servidor({
            '/api/v1/auth/refresh': comSessao,
            '/api/v1/auth/eu': () => json({ ...perfil, emailConfirmado: false }),
            '/api/v1/auth/confirmacao-email/reenvio': () => new Response(null, { status: 202 }),
        })
        await abrir('/')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Reenviar link' }))
        await waitFor(() => expect(screen.getByText(/Enviamos um link novo/)).toBeInTheDocument())
        expect(chamadas.some((c) => c.caminho === '/api/v1/auth/confirmacao-email/reenvio')).toBe(true)
    })
})
