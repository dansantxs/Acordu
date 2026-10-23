import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comContato, json } from '@/testes/app'

const vazia = () => json({ itens: [], pagina: 1, tamanhoPagina: 20, total: 0 })

describe('portal: ajuda', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('abre pelo menu do portal, com o guia e as capturas', async () => {
        comContato({ 'GET /api/v1/portal/solicitacoes': vazia })
        const roteador = await abrir('/portal')
        await userEvent.setup().click(await screen.findByRole('link', { name: /^Ajuda/ }))
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/portal/ajuda'))
        expect(await screen.findByText('Como usar o portal de Prado Consultoria')).toBeInTheDocument()
        const aprovar = screen.getByRole('region', { name: 'Aprovar uma entrega' })
        expect(within(aprovar).getByRole('img')).toHaveAttribute('src', '/ajuda/portal-detalhe.webp')
        expect(screen.getByRole('region', { name: 'Conferir o contrato' })).toBeInTheDocument()
    })

    it('sem acesso aos contratos, o guia não fala deles', async () => {
        comContato({}, 'PROPRIAS')
        await abrir('/portal/ajuda')
        expect(await screen.findByRole('region', { name: 'Abrir uma solicitação' })).toBeInTheDocument()
        expect(screen.queryByRole('region', { name: 'Conferir o contrato' })).not.toBeInTheDocument()
    })
})
