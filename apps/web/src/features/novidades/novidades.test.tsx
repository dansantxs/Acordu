import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comContato, comPerfil, json } from '@/testes/app'
import { NOTAS, notasDo } from './notas'
import { marcarVistas, reiniciarVistasParaTestes } from './vistas'

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
        expect(localStorage.getItem('acordu.novidades.equipe')).toMatch(
            new RegExp(`^${notasDo('equipe')[0]!.data}\\|\\d{3}$`),
        )
    })

    it('nota nova no mesmo dia de uma já vista acende o ponto de novo', async () => {
        comPerfil([])
        marcarVistas('equipe')
        const extra = { ...notasDo('equipe')[0]!, titulo: 'Mais uma do dia' }
        NOTAS.unshift(extra)
        try {
            await abrir('/ajuda')
            const deNovo = await screen.findByRole('navigation', { name: 'Principal' })
            expect(await within(deNovo).findByRole('link', { name: 'Novidades, há novidades' })).toBeInTheDocument()
        } finally {
            NOTAS.splice(NOTAS.indexOf(extra), 1)
        }
    })

    it('"já vi" guardado no formato antigo (só a data) acende o ponto uma vez', async () => {
        localStorage.setItem('acordu.novidades.equipe', notasDo('equipe')[0]!.data)
        comPerfil([])
        await abrir('/ajuda')
        const menu = await screen.findByRole('navigation', { name: 'Principal' })
        expect(within(menu).getByRole('link', { name: 'Novidades, há novidades' })).toBeInTheDocument()
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
