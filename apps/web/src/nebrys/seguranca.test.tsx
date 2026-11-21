import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrirSessaoOperacao, reiniciarSessaoOperacaoParaTestes } from '@/shared/sessao/sessaoOperacao'
import { abrir, daquiA, json, semSessao, servidor } from '@/testes/app'

// Spec do F14, área da Nebrys (F14m): token e documento não ficam em cookie, armazenamento, endereço nem cache.

const TOKEN = 'token-de-operacao-secreto'

describe('área da Nebrys: o que não pode ficar no navegador', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        reiniciarSessaoOperacaoParaTestes()
        localStorage.clear()
        sessionStorage.clear()
    })
    afterEach(() => vi.unstubAllGlobals())

    it('Entrar_TokenNaoVaiParaCookieArmazenamentoNemCache', async () => {
        servidor({
            '/api/v1/auth/refresh': semSessao,
            'POST /api/v1/nebrys/sessao': () =>
                json({ accessToken: TOKEN, expiraEm: daquiA(30 * 60_000), tipo: 'OPERACAO' }),
            '/api/v1/nebrys/empresas': () => json({ itens: [], pagina: 1, tamanhoPagina: 25, total: 0 }),
        })
        const usuario = userEvent.setup()
        const roteador = await abrir('/nebrys')
        await usuario.type(await screen.findByLabelText('E-mail'), 'operacao@nebrys.com.br')
        await usuario.type(screen.getByLabelText('Senha', { exact: true }), 'umasenhaboa')
        await usuario.click(screen.getByRole('button', { name: 'Entrar' }))
        await screen.findByRole('heading', { name: 'Empresas' })

        expect(document.cookie).toBe('')
        expect(localStorage.length).toBe(0)
        expect(sessionStorage.length).toBe(0)
        const consultas = roteador.options.context.consultas
        expect(
            JSON.stringify(
                consultas
                    .getQueryCache()
                    .getAll()
                    .map((q) => q.state.data),
            ),
        ).not.toContain(TOKEN)
        // A senha não fica nas variáveis das mutações guardadas.
        expect(
            JSON.stringify(
                consultas
                    .getMutationCache()
                    .getAll()
                    .map((m) => m.state.variables),
            ),
        ).not.toContain('umasenhaboa')
    })

    it('Liberar_DocumentoNaoFicaNoCacheDeMutacoesNemNasConsultas', async () => {
        abrirSessaoOperacao({ accessToken: TOKEN, expiraEm: daquiA(30 * 60_000) })
        servidor({
            '/api/v1/auth/refresh': semSessao,
            'POST /api/v1/nebrys/documentos/liberacao': () => new Response(null, { status: 204 }),
        })
        const usuario = userEvent.setup()
        const roteador = await abrir('/nebrys/documentos')
        await usuario.type(await screen.findByLabelText('CPF ou CNPJ'), '52998224725')
        await usuario.click(screen.getByRole('button', { name: 'Liberar' }))
        await screen.findByText('Documento liberado. O próximo cadastro com ele vai passar.')

        const consultas = roteador.options.context.consultas
        const guardado = JSON.stringify([
            consultas
                .getMutationCache()
                .getAll()
                .map((m) => m.state.variables),
            consultas
                .getQueryCache()
                .getAll()
                .map((q) => [q.queryKey, q.state.data]),
        ])
        expect(guardado).not.toMatch(/52998224725|529\.982/)
        expect(document.cookie).toBe('')
        expect(localStorage.length).toBe(0)
        expect(sessionStorage.length).toBe(0)
    })

    it('Liberar_400DaApi_MostraOErroNoCampoSemFecharASessao', async () => {
        abrirSessaoOperacao({ accessToken: TOKEN, expiraEm: daquiA(30 * 60_000) })
        servidor({
            '/api/v1/auth/refresh': semSessao,
            'POST /api/v1/nebrys/documentos/liberacao': () =>
                new Response(
                    JSON.stringify({
                        type: 'https://acordu.app.br/erros/validacao',
                        status: 400,
                        title: 'validacao',
                        errors: { documento: ['Documento inválido.'] },
                    }),
                    { status: 400, headers: { 'content-type': 'application/problem+json' } },
                ),
        })
        const usuario = userEvent.setup()
        await abrir('/nebrys/documentos')
        await usuario.type(await screen.findByLabelText('CPF ou CNPJ'), '52998224725')
        await usuario.click(screen.getByRole('button', { name: 'Liberar' }))
        expect(await screen.findByText('Documento inválido.')).toBeInTheDocument()
        expect(screen.getByRole('heading', { name: 'Liberar documento' })).toBeInTheDocument()
    })
})
