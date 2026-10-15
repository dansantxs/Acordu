import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { act } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { ErroApi } from '../api/problema'
import { avisar, avisarErro } from './avisos'
import { RegiaoAvisos } from './RegiaoAvisos'

describe('avisos', () => {
    it('oferece desfazer e some ao desfazer', async () => {
        const desfazer = vi.fn()
        render(<RegiaoAvisos />)
        act(() => void avisar('Tarefa concluída.', { desfazer }))
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Desfazer' }))
        expect(desfazer).toHaveBeenCalledOnce()
        expect(screen.queryByText('Tarefa concluída.')).not.toBeInTheDocument()
    })

    it('mostra a mensagem do erro da API', async () => {
        render(<RegiaoAvisos />)
        act(() => void avisarErro(new ErroApi(409, { status: 409, detail: 'Outra pessoa mudou este item.' })))
        expect(await screen.findByText('Outra pessoa mudou este item.')).toBeInTheDocument()
    })
})
