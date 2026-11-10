import { describe, expect, it } from 'vitest'
import { mensagemDeErro } from './mensagens'
import { ErroApi } from './problema'

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

    it('429 com detalhe (limite de um caso de uso) usa a mensagem da API; sem, a genérica', () => {
        expect(
            mensagemDeErro(
                new ErroApi(429, { title: 'Muitas requisições', detail: 'Muitas sugestões em pouco tempo.' }),
            ),
        ).toBe('Muitas sugestões em pouco tempo.')
        expect(mensagemDeErro(new ErroApi(429, { title: 'Muitas requisições' }))).toBe(
            'Muitas tentativas seguidas. Aguarde um minuto e tente de novo.',
        )
    })
})
