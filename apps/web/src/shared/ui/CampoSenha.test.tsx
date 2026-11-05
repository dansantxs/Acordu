import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { CampoSenha } from './CampoSenha'

describe('CampoSenha', () => {
    it('começa oculta, e o botão mostra e oculta sem perder o que foi digitado', async () => {
        render(<CampoSenha name="senha" rotulo="Senha" autoComplete="current-password" />)
        const usuario = userEvent.setup()
        const campo = screen.getByLabelText('Senha')
        await usuario.type(campo, 'segredo-123')
        expect(campo).toHaveAttribute('type', 'password')
        expect(campo).toHaveAttribute('autocomplete', 'current-password')

        const olho = screen.getByRole('button', { name: 'Mostrar senha' })
        expect(olho).toHaveAttribute('aria-pressed', 'false')
        await usuario.click(olho)
        expect(campo).toHaveAttribute('type', 'text')
        expect(campo).toHaveValue('segredo-123')
        expect(olho).toHaveAttribute('aria-pressed', 'true')
        // Visível, fora do corretor ortográfico e da maiúscula automática do celular.
        expect(campo).toHaveAttribute('spellcheck', 'false')
        expect(campo).toHaveAttribute('autocapitalize', 'off')

        await usuario.click(olho)
        expect(campo).toHaveAttribute('type', 'password')
    })

    it('o botão está na ordem do Tab, depois do campo', async () => {
        render(<CampoSenha name="senha" rotulo="Senha" />)
        const usuario = userEvent.setup()
        await usuario.tab()
        expect(screen.getByLabelText('Senha')).toHaveFocus()
        await usuario.tab()
        expect(screen.getByRole('button', { name: 'Mostrar senha' })).toHaveFocus()
    })
})
