import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ContextoGravacao } from '../sessao/gravacao'
import { BotaoTexto } from './Botao'

/** `BotaoTexto` com e sem `grava`, com a empresa só leitura e fora dela (#191). */

const MOTIVO = 'A empresa está só leitura.'

function tela(podeGravar: boolean, grava: boolean, aoApertar: () => void) {
    render(
        <ContextoGravacao.Provider value={{ podeGravar, idMotivo: 'motivo' }}>
            <p id="motivo">{MOTIVO}</p>
            <BotaoTexto grava={grava} onPress={aoApertar}>
                Editar
            </BotaoTexto>
        </ContextoGravacao.Provider>,
    )
    return screen.getByRole('button', { name: 'Editar' })
}

describe('BotaoTexto', () => {
    it('BotaoTexto_GravaEmpresaSomenteLeitura_IndisponivelComAExplicacaoESemChamar', async () => {
        const aoApertar = vi.fn()
        const botao = tela(false, true, aoApertar)
        expect(botao).toBeDisabled()
        expect(botao).toHaveAccessibleDescription(MOTIVO)
        await userEvent.setup().click(botao)
        expect(aoApertar).not.toHaveBeenCalled()
    })

    it('BotaoTexto_GravaEmpresaQueGrava_DisponivelEChama', async () => {
        const aoApertar = vi.fn()
        const botao = tela(true, true, aoApertar)
        expect(botao).toBeEnabled()
        expect(botao).not.toHaveAccessibleDescription()
        await userEvent.setup().click(botao)
        expect(aoApertar).toHaveBeenCalledOnce()
    })

    it('BotaoTexto_SemGravaEmpresaSomenteLeitura_DisponivelEChama', async () => {
        const aoApertar = vi.fn()
        const botao = tela(false, false, aoApertar)
        expect(botao).toBeEnabled()
        expect(botao).not.toHaveAccessibleDescription()
        await userEvent.setup().click(botao)
        expect(aoApertar).toHaveBeenCalledOnce()
    })

    it('BotaoTexto_SemGravaEmpresaQueGrava_DisponivelEChama', async () => {
        const aoApertar = vi.fn()
        const botao = tela(true, false, aoApertar)
        expect(botao).toBeEnabled()
        expect(botao).not.toHaveAccessibleDescription()
        await userEvent.setup().click(botao)
        expect(aoApertar).toHaveBeenCalledOnce()
    })
})
