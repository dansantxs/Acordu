import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comContato, comPerfil, json } from '@/testes/app'
import { notasDo } from './notas'
import { reiniciarVistasParaTestes } from './vistas'

describe('novidades', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        reiniciarVistasParaTestes()
    })
    afterEach(() => vi.unstubAllGlobals())

    it('o ponto no menu some depois de abrir Novidades, e o "já vi" fica no navegador', async () => {
        comPerfil([])
        await abrir('/ajuda')
        const menu = await screen.findByRole('navigation', { name: 'Principal' })
        const item = within(menu).getByRole('link', { name: 'Novidades, há novidades' })
        await userEvent.setup().click(item)
        expect(await screen.findByRole('heading', { level: 1, name: 'Novidades' })).toBeInTheDocument()
        expect(screen.getByRole('heading', { level: 2, name: notasDo('equipe')[0]!.titulo })).toBeInTheDocument()
        expect(await within(menu).findByRole('link', { name: 'Novidades' })).toBeInTheDocument()
        expect(localStorage.getItem('acordu.novidades.equipe')).toBe(notasDo('equipe')[0]!.data)
    })

    it('a equipe não vê as notas do cliente', async () => {
        comPerfil([])
        await abrir('/novidades')
        await screen.findByRole('heading', { level: 1, name: 'Novidades' })
        for (const n of notasDo('cliente')) expect(screen.queryByText(n.titulo)).not.toBeInTheDocument()
    })

    it('no portal, as novidades do cliente ficam no alto da Ajuda e apagam o ponto', async () => {
        comContato({
            'GET /api/v1/portal/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 20, total: 0 }),
        })
        await abrir('/portal')
        const portal = await screen.findByRole('navigation', { name: 'Portal' })
        await userEvent.setup().click(within(portal).getByRole('link', { name: 'Ajuda, há novidades' }))
        const novidades = await screen.findByRole('region', { name: 'Novidades' })
        expect(within(novidades).getByText(notasDo('cliente')[0]!.titulo)).toBeInTheDocument()
        expect(await within(portal).findByRole('link', { name: 'Ajuda' })).toBeInTheDocument()
    })

    it('o portal fica no Café mesmo com outro couro escolhido neste navegador', async () => {
        localStorage.setItem('acordu.couro', 'bordo')
        document.documentElement.dataset.couro = 'bordo'
        comContato({
            'GET /api/v1/portal/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 20, total: 0 }),
        })
        await abrir('/portal')
        await screen.findByRole('navigation', { name: 'Portal' })
        expect(document.documentElement.dataset.couro).toBeUndefined()
        localStorage.removeItem('acordu.couro')
    })
})
