import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { CampoSelecao } from './CampoSelecao'

const pessoas = [
    { id: 'u1', rotulo: 'Ana Prado' },
    { id: 'u2', rotulo: 'João Ávila' },
    { id: 'u3', rotulo: 'Rafael Moura' },
]

function Campo({ inicial = null as string | null }) {
    const [valor, setValor] = useState<string | null>(inicial)
    return (
        <>
            <CampoSelecao
                rotulo="Responsável"
                opcoes={pessoas}
                valor={valor}
                aoMudar={setValor}
                vazio="Ninguém"
                isRequired
            />
            <p data-testid="valor">{valor ?? 'nenhum'}</p>
        </>
    )
}

describe('campo de seleção com busca', () => {
    it('filtra sem diferenciar acento nem maiúscula e escolhe', async () => {
        render(<Campo />)
        const usuario = userEvent.setup()
        await usuario.type(screen.getByRole('combobox', { name: 'Responsável' }), 'avi')
        const opcoes = await screen.findAllByRole('option')
        expect(opcoes.map((o) => o.textContent)).toEqual(['João Ávila'])
        await usuario.click(opcoes[0]!)
        expect(screen.getByTestId('valor')).toHaveTextContent('u2')
        expect(screen.getByRole('combobox', { name: 'Responsável' })).toHaveValue('João Ávila')
    })

    it('Esc volta ao valor anterior e a opção vazia limpa', async () => {
        render(<Campo inicial="u3" />)
        const usuario = userEvent.setup()
        const campo = screen.getByRole('combobox', { name: 'Responsável' })
        await usuario.clear(campo)
        await usuario.type(campo, 'ana')
        await usuario.keyboard('{Escape}')
        expect(campo).toHaveValue('Rafael Moura')
        expect(screen.getByTestId('valor')).toHaveTextContent('u3')

        await usuario.clear(campo)
        await usuario.type(campo, 'ningu')
        await usuario.click(await screen.findByRole('option', { name: 'Ninguém' }))
        expect(screen.getByTestId('valor')).toHaveTextContent('nenhum')
    })

    it('marca o obrigatório com * fora do nome do campo', () => {
        render(<Campo />)
        expect(screen.getByRole('combobox', { name: 'Responsável' })).toBeRequired()
        expect(screen.getByText('*')).toHaveAttribute('aria-hidden', 'true')
    })
})
