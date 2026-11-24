import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json } from '@/testes/app'

const C = '0199f0a0-0000-7000-8000-0000000000c1'
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })
const resumo = (id: string, nome: string, exemplo: boolean) => ({
    id,
    nome,
    documento: null,
    ativo: true,
    razaoSocial: null,
    responsavel: null,
    etiquetas: [],
    exemplo,
})
const ficha = (exemplo: boolean) => ({
    ...resumo(C, 'Padaria Bom Grão', exemplo),
    observacoes: null,
    versao: '1',
    inscricaoEstadual: null,
    email: null,
    telefone: null,
    endereco: null,
    contatos: [],
})
const AVISO = /Cliente de exemplo: os dados dele são fictícios/

describe('clientes de exemplo (F14-92)', () => {
    beforeEach(() => reiniciarParaTestes())

    it('a lista mostra a etiqueta "exemplo" só no cliente de exemplo', async () => {
        comPerfil(['cliente.ver'], {
            'GET /api/v1/clientes': () =>
                pagina([resumo('c1', 'Padaria Bom Grão', true), resumo('c2', 'Floricultura', false)]),
        })
        await abrir('/clientes')
        const exemplo = (await screen.findByRole('link', { name: 'Padaria Bom Grão' })).closest('tr, li, div')!
        expect(exemplo).toHaveTextContent('exemplo')
        expect(screen.getAllByText('exemplo')).toHaveLength(1)
    })

    it('a ficha avisa que o cliente é de exemplo', async () => {
        comPerfil(['cliente.ver'], { [`GET /api/v1/clientes/${C}`]: () => json(ficha(true)) })
        await abrir(`/clientes/${C}`)
        expect(await screen.findByText(AVISO)).toBeInTheDocument()
    })

    it('a ficha de cliente real não traz o aviso', async () => {
        comPerfil(['cliente.ver'], { [`GET /api/v1/clientes/${C}`]: () => json(ficha(false)) })
        await abrir(`/clientes/${C}`)
        await screen.findByRole('heading', { name: 'Padaria Bom Grão' })
        expect(screen.queryByText(AVISO)).not.toBeInTheDocument()
    })
})
