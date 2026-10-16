import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { formatarDocumento, formatarTelefone } from '../format/mascaras'
import { CampoMascarado } from './CampoMascarado'

describe('campo com máscara', () => {
    it('formata enquanto se digita e vai formatado no formulário', async () => {
        let enviado = ''
        render(
            <form
                onSubmit={(e) => {
                    e.preventDefault()
                    enviado = String(new FormData(e.currentTarget).get('documento'))
                }}
            >
                <CampoMascarado name="documento" rotulo="CPF ou CNPJ" mascara={formatarDocumento} />
                <button type="submit">Enviar</button>
            </form>,
        )
        const usuario = userEvent.setup()
        const campo = screen.getByLabelText('CPF ou CNPJ')
        await usuario.type(campo, '11222333000181')
        expect(campo).toHaveValue('11.222.333/0001-81')
        await usuario.click(screen.getByRole('button', { name: 'Enviar' }))
        expect(enviado).toBe('11.222.333/0001-81')
    })

    it('apagar no meio mantém o cursor no lugar', async () => {
        render(<CampoMascarado rotulo="Telefone" mascara={formatarTelefone} defaultValue="18999990000" />)
        const campo = screen.getByLabelText<HTMLInputElement>('Telefone')
        expect(campo).toHaveValue('(18) 99999-0000')
        const usuario = userEvent.setup()
        // Cursor logo depois do primeiro "9" (posição 6) e apaga para trás.
        await usuario.pointer({ target: campo, offset: 6, keys: '[MouseLeft]' })
        await usuario.keyboard('{Backspace}')
        expect(campo).toHaveValue('(18) 9999-0000')
        // Depois dos mesmos 2 dígitos ("18"): continuar digitando entra no lugar do que foi apagado.
        expect(campo.selectionStart).toBe(3)
        await usuario.keyboard('8')
        expect(campo).toHaveValue('(18) 89999-0000')
    })
})
