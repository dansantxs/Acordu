import { describe, expect, it } from 'vitest'
import { mensagemDeErro } from './mensagens'

describe('mensagemDeErro', () => {
    it.each(['Failed to fetch', 'NetworkError when attempting to fetch resource.', 'Load failed'])(
        'falha de rede (%s) é "sem conexão"',
        (texto) => expect(mensagemDeErro(new TypeError(texto))).toMatch(/^Sem conexão com o servidor/),
    )

    it('outro TypeError é defeito, não falta de internet', () => {
        expect(mensagemDeErro(new TypeError("Cannot read properties of undefined (reading 'find')"))).toBe(
            'Algo deu errado. Tente de novo.',
        )
    })
})
