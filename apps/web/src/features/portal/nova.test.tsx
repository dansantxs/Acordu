import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comContato, json } from '@/testes/app'

const detalhe = {
    id: 's9',
    numero: 1050,
    titulo: 'Site fora do ar',
    descricao: null,
    statusExterno: 'RECEBIDA',
    abertaEm: '2026-10-23T12:00:00Z',
    responsavel: null,
    sla: null,
    tarefasVisiveis: [],
    tempoVisivelMinutos: 0,
    podeAprovar: false,
}

describe('portal: nova solicitação', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('abre pela lista, manda a urgência escolhida e vai para o detalhe', async () => {
        const chamadas = comContato({
            '/api/v1/portal/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 20, total: 0 }),
            'POST /api/v1/portal/solicitacoes': () => json(detalhe, 201),
            'GET /api/v1/portal/solicitacoes/s9': () => json(detalhe),
            'GET /api/v1/portal/solicitacoes/s9/interacoes': () => json([]),
        })
        const roteador = await abrir('/portal')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('link', { name: /Nova solicitação/ }))
        await usuario.type(await screen.findByRole('textbox', { name: /Assunto/ }), 'Site fora do ar')
        await usuario.type(screen.getByRole('textbox', { name: /O que está acontecendo/ }), 'Desde as 9h.')
        expect(screen.getByRole('radio', { name: /Média/ })).toBeChecked()
        await usuario.click(screen.getByRole('radio', { name: /Urgente/ }))
        await usuario.click(screen.getByRole('button', { name: 'Abrir solicitação' }))

        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/portal/solicitacoes/s9'))
        expect(await screen.findByText('Solicitação #1050 aberta. A equipe já foi avisada.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/portal/solicitacoes')?.corpo).toEqual(
            {
                titulo: 'Site fora do ar',
                descricao: 'Desde as 9h.',
                prioridade: 'URGENTE',
            },
        )
    })

    it('com anexo vai em multipart; arquivo grande é recusado antes', async () => {
        const chamadas = comContato({
            'POST /api/v1/portal/solicitacoes': () => json(detalhe, 201),
            'GET /api/v1/portal/solicitacoes/s9': () => json(detalhe),
            'GET /api/v1/portal/solicitacoes/s9/interacoes': () => json([]),
        })
        await abrir('/portal/nova')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByRole('textbox', { name: /Assunto/ }), 'Logo nova')
        const entrada = document.querySelector<HTMLInputElement>('input[type="file"]')!
        await usuario.upload(entrada, new File([new Uint8Array(21 * 1024 * 1024)], 'enorme.zip'))
        expect(await screen.findByText(/enorme\.zip: acima de 20 MB/)).toBeInTheDocument()
        await usuario.upload(entrada, new File(['x'], 'logo.png', { type: 'image/png' }))
        await usuario.click(screen.getByRole('button', { name: 'Abrir solicitação' }))
        await vi.waitFor(() =>
            expect(
                chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/portal/solicitacoes')?.corpo,
            ).toBeTruthy(),
        )
        const corpo = chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/portal/solicitacoes')!
            .corpo as FormData
        expect([corpo.get('titulo'), corpo.get('prioridade'), corpo.getAll('anexos').length]).toEqual([
            'Logo nova',
            'MEDIA',
            1,
        ])
    })
})
