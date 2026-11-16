import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, daquiA, json, servidor } from '@/testes/app'

// Build sem a chave do Turnstile: o cadastro envia sem o desafio.
vi.mock('@/shared/config/ambiente', async (original) => ({
    ...(await original<typeof import('@/shared/config/ambiente')>()),
    CHAVE_TURNSTILE: undefined,
}))

const RECUSA =
    'Este CPF ou CNPJ já teve um período de teste no Acordu. Para testar de novo, fale com a Nebrys: contato@acordu.app.br.'

async function preencher(documento: string) {
    const usuario = userEvent.setup()
    await usuario.type(await screen.findByLabelText('Seu nome'), 'Ana')
    await usuario.type(screen.getByLabelText('E-mail'), 'ana@exemplo.com')
    await usuario.type(screen.getByLabelText('Senha'), 'umasenhaboa')
    await usuario.type(screen.getByLabelText('Nome da empresa'), 'Prado')
    if (documento) await usuario.type(screen.getByLabelText('CPF ou CNPJ'), documento)
    await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))
}

// Spec do F14, documento no cadastro (F14g; RF-AUT-09).
describe('documento no cadastro', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('o campo é obrigatório e explica a regra com o canal da Nebrys', async () => {
        servidor({ '/api/v1/auth/refresh': () => new Response(null, { status: 401 }) })
        await abrir('/cadastro')
        const campo = await screen.findByLabelText('CPF ou CNPJ')
        expect(campo).toBeRequired()
        expect(campo).toHaveAccessibleDescription(
            'Cada CPF ou CNPJ tem um período de teste. Para um novo teste, fale com a Nebrys: contato@acordu.app.br.',
        )
    })

    it('sem o documento, não envia e mostra o erro no campo (F14-56)', async () => {
        const chamadas = servidor({ '/api/v1/auth/refresh': () => new Response(null, { status: 401 }) })
        await abrir('/cadastro')
        await preencher('')
        expect(await screen.findByText('Informe o CPF ou CNPJ.')).toBeInTheDocument()
        expect(screen.getByLabelText('CPF ou CNPJ')).toHaveAttribute('aria-invalid', 'true')
        expect(chamadas.some((c) => c.caminho === '/api/v1/auth/registro')).toBe(false)
    })

    it('com os dígitos verificadores errados, não envia (F14-56)', async () => {
        const chamadas = servidor({ '/api/v1/auth/refresh': () => new Response(null, { status: 401 }) })
        await abrir('/cadastro')
        await preencher('52998224724')
        expect(await screen.findByText('CPF ou CNPJ inválido.')).toBeInTheDocument()
        expect(chamadas.some((c) => c.caminho === '/api/v1/auth/registro')).toBe(false)
    })

    it('manda o documento formatado, com o CNPJ alfanumérico em maiúsculas (F14-58)', async () => {
        const chamadas = servidor({
            '/api/v1/auth/refresh': () => new Response(null, { status: 401 }),
            '/api/v1/auth/registro': () => json({ accessToken: 'a', expiraEm: daquiA(3_600_000) }, 201),
            '/api/v1/auth/eu': () => new Response(null, { status: 401 }),
        })
        await abrir('/cadastro')
        await preencher('12abc34501de35')
        await waitFor(() => expect(chamadas.some((c) => c.caminho === '/api/v1/auth/registro')).toBe(true))
        expect(chamadas.find((c) => c.caminho === '/api/v1/auth/registro')?.corpo).toMatchObject({
            documento: '12.ABC.345/01DE-35',
        })
    })

    it('documento que já teve teste: a recusa da API aparece no campo, com o canal da Nebrys (F14-57)', async () => {
        servidor({
            '/api/v1/auth/refresh': () => new Response(null, { status: 401 }),
            '/api/v1/auth/registro': () =>
                json(
                    {
                        type: 'https://acordu.app.br/erros/validacao',
                        status: 400,
                        detail: RECUSA,
                        errors: { documento: [RECUSA] },
                    },
                    400,
                ),
        })
        await abrir('/cadastro')
        await preencher('529.982.247-25')
        const campo = screen.getByLabelText('CPF ou CNPJ')
        await waitFor(() => expect(campo).toHaveAttribute('aria-invalid', 'true'))
        expect(campo).toHaveAccessibleDescription(expect.stringContaining(RECUSA))
        expect(screen.getAllByText(RECUSA).length).toBeGreaterThan(0)
    })
})
