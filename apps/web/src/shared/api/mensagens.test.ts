import { describe, expect, it } from 'vitest'
import { mensagemDeErro } from './mensagens'

describe('mensagemDeErro', () => {
    it.each(['Failed to fetch', 'NetworkError when attempting to fetch resource.', 'Load failed'])(
        'falha de rede (%s) é "sem conexão"',
        (texto) => expect(mensagemDeErro(new TypeError(texto))).toMatch(/^Sem conexão com o servidor/),
    )

    it.each([
        "Cannot read properties of undefined (reading 'find')",
        "Cannot read properties of undefined (reading 'refetch')",
        "Failed to execute 'fetch' on 'Window': Invalid value",
    ])('outro TypeError (%s) é defeito, não falta de internet', (texto) =>
        expect(mensagemDeErro(new TypeError(texto))).toBe('Algo deu errado. Tente de novo.'),
    )
})
