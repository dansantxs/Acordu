import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { CampoSenha } from './CampoTexto'

describe('CampoSenha', () => {
    it('começa oculta, e o botão mostra e oculta sem perder o que foi digitado', async () => {
        render(<CampoSenha name="senha" rotulo="Senha" autoComplete="current-password" />)
        const usuario = userEvent.setup()
        const campo = screen.getByLabelText('Senha')
        await usuario.type(campo, 'segredo-123')
        expect(campo).toHaveAttribute('type', 'password')
        expect(campo).toHaveAttribute('autocomplete', 'current-password')

        await usuario.click(screen.getByRole('button', { name: 'Mostrar senha' }))
        expect(campo).toHaveAttribute('type', 'text')
        expect(campo).toHaveValue('segredo-123')
        expect(screen.getByRole('button', { name: 'Ocultar senha' })).toHaveAttribute('aria-pressed', 'true')

        await usuario.click(screen.getByRole('button', { name: 'Ocultar senha' }))
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
