import { afterEach, describe, expect, it, vi } from 'vitest'
import { respostaDaReceita } from '@/testes/receita'
import { buscarCnpj, formaDeTitulo } from './cnpj'

describe('BrasilAPI (CNPJ)', () => {
    afterEach(() => {
        vi.unstubAllGlobals()
        vi.restoreAllMocks()
    })

    it('traduz a resposta, sem cookie nem Referer e só o CNPJ na URL (F10-20)', async () => {
        const buscar = vi.fn(async () => Response.json(respostaDaReceita))
        vi.stubGlobal('fetch', buscar)
        expect(await buscarCnpj('19131243000197')).toEqual({
            tipo: 'achou',
            empresa: {
                razaoSocial: 'OPEN KNOWLEDGE BRASIL',
                nomeFantasia: '',
                situacao: 'ATIVA',
                situacaoDesde: '2013-10-03',
                endereco: {
                    cep: '01311-902',
                    logradouro: 'Avenida Paulista 37',
                    numero: '37',
                    complemento: 'Andar 4',
                    bairro: 'Bela Vista',
                    cidade: 'Sao Paulo',
                    uf: 'SP',
                },
                telefone: '(11) 2385-1939',
                email: '',
            },
        })
        expect(buscar).toHaveBeenCalledWith(
            'https://brasilapi.com.br/api/cnpj/v1/19131243000197',
            expect.objectContaining({ credentials: 'omit', referrerPolicy: 'no-referrer' }),
        )
    })

    it('espera no máximo 4 s, como o ViaCEP (F10-17)', async () => {
        const tempo = vi.spyOn(AbortSignal, 'timeout')
        vi.stubGlobal(
            'fetch',
            vi.fn(async () => Response.json(respostaDaReceita)),
        )
        await buscarCnpj('19131243000197')
        expect(tempo).toHaveBeenCalledWith(4_000)
    })

    it('"S/N" vira número vazio, e endereço sem o mínimo não vem', async () => {
        vi.stubGlobal(
            'fetch',
            vi.fn(async () => Response.json({ ...respostaDaReceita, numero: 'S/N' })),
        )
        const comSn = await buscarCnpj('19131243000197')
        expect(comSn.tipo === 'achou' && comSn.empresa.endereco?.numero).toBe('')

        vi.stubGlobal(
            'fetch',
            vi.fn(async () => Response.json({ ...respostaDaReceita, uf: 'EX', email: 'CONTATO@OK.ORG.BR' })),
        )
        const exterior = await buscarCnpj('19131243000197')
        expect(exterior.tipo === 'achou' && exterior.empresa.endereco).toBeNull()
        expect(exterior.tipo === 'achou' && exterior.empresa.email).toBe('contato@ok.org.br')
    })

    it('404 é CNPJ sem cadastro (F10-15)', async () => {
        vi.stubGlobal(
            'fetch',
            vi.fn(async () => Response.json({ message: 'CNPJ não encontrado.' }, { status: 404 })),
        )
        expect(await buscarCnpj('11222333000181')).toEqual({ tipo: 'nao-encontrado' })
    })

    it.each([
        ['fora do ar (F10-16)', () => new Response(null, { status: 503 })],
        ['limite atingido (F10-16)', () => new Response(null, { status: 429 })],
        ['outro 4xx, como um alfanumérico que ele não conhece (F10-16)', () => new Response(null, { status: 400 })],
        [
            'CORS recusado ou sem rede (F10-16)',
            () => {
                throw new TypeError('Failed to fetch')
            },
        ],
        [
            'tempo esgotado (F10-17)',
            () => {
                throw new DOMException('Tempo esgotado', 'TimeoutError')
            },
        ],
        ['JSON estranho (F10-18)', () => Response.json(['nada'])],
        ['sem razão social (F10-18)', () => Response.json({ ...respostaDaReceita, razao_social: null })],
        ['não é JSON (F10-18)', () => new Response('<html>', { status: 200 })],
    ])('%s → falhou', async (_, resposta) => {
        vi.stubGlobal(
            'fetch',
            vi.fn(async () => resposta()),
        )
        expect(await buscarCnpj('11222333000181')).toEqual({ tipo: 'falhou' })
    })

    it.each([
        ['RUA DAS FLORES', 'Rua das Flores'],
        ['  JARDIM  DE  ALAH ', 'Jardim de Alah'],
        ['DE PAULA E SILVA', 'De Paula e Silva'],
        ['', ''],
    ])('forma de título: %s → %s', (entrada, saida) => expect(formaDeTitulo(entrada)).toBe(saida))
})
