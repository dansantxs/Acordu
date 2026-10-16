import { afterEach, describe, expect, it, vi } from 'vitest'
import { buscarCep } from './viaCep'

describe('ViaCEP', () => {
    afterEach(() => vi.unstubAllGlobals())

    it('traduz a resposta e não manda cookie nem Referer', async () => {
        const buscar = vi.fn(async () =>
            Response.json({ logradouro: 'Rua das Flores', bairro: 'Centro', localidade: 'Bastos', uf: 'SP' }),
        )
        vi.stubGlobal('fetch', buscar)
        expect(await buscarCep('19700-000')).toEqual({
            logradouro: 'Rua das Flores',
            bairro: 'Centro',
            cidade: 'Bastos',
            uf: 'SP',
        })
        expect(buscar).toHaveBeenCalledWith(
            'https://viacep.com.br/ws/19700000/json/',
            expect.objectContaining({ credentials: 'omit', referrerPolicy: 'no-referrer' }),
        )
    })

    it.each([
        ['CEP incompleto', '1970', () => Response.json({})],
        ['CEP inexistente', '00000000', () => Response.json({ erro: 'true' })],
        ['fora do ar', '19700000', () => new Response(null, { status: 502 })],
        [
            'sem rede',
            '19700000',
            () => {
                throw new TypeError('Failed to fetch')
            },
        ],
    ])('%s → nulo', async (_, cep, resposta) => {
        vi.stubGlobal(
            'fetch',
            vi.fn(async () => resposta()),
        )
        expect(await buscarCep(cep)).toBeNull()
    })
})
