import { screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { descartarTokenDeLink } from '@/shared/sessao/tokenDaUrl'
import { abrirComoNaProducao, json, semSessao, servidor, type Chamada } from '@/testes/app'

// Bug #222: sem sessão, a primeira carga recebe 401 do refresh. As telas de link já fizeram a consulta delas, e a
// sessão não pode limpar o cache nessa hora (a consulta sumia e a tela ficava em "Carregando…" para sempre).

/** Resposta que só chega quando o teste mandar. */
function atrasada(resposta: () => Response) {
    let responder: () => void = () => undefined
    const promessa = new Promise<Response>((resolver) => {
        responder = () => resolver(resposta())
    })
    return { rota: () => promessa, responder: () => responder() }
}

/**
 * A ordem de produção: a consulta da tela sai, o refresh responde 401 com ela ainda em voo, e só depois ela responde.
 */
function naOrdemDeProducao(caminho: string, resposta: () => Response) {
    const refresh = atrasada(semSessao)
    const consulta = atrasada(resposta)
    const chamadas = servidor({ '/api/v1/auth/refresh': refresh.rota, [caminho]: consulta.rota })
    return {
        chamadas,
        async responder(inicio: Promise<void>, rota: string) {
            await consultaSaiu(chamadas, rota)
            refresh.responder()
            await inicio
            consulta.responder()
        },
    }
}

async function consultaSaiu(chamadas: Chamada[], caminho: string) {
    await waitFor(() => expect(chamadas.some((c) => c.caminho === caminho)).toBe(true))
}

describe('links abertos sem sessão (#222)', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        sessionStorage.clear()
        descartarTokenDeLink('convite-empresa')
        descartarTokenDeLink('convite')
        descartarTokenDeLink('convite-portal')
        descartarTokenDeLink('confirmacao')
    })
    afterEach(() => vi.unstubAllGlobals())

    it('CadastroPorConvite_ConsultaSaiAntesDoRefreshRecusado_MostraOEmailEOFormulario', async () => {
        const caminho = '/api/v1/auth/registro/convite/consulta'
        const api = naOrdemDeProducao(`POST ${caminho}`, () =>
            json({ email: 'ana@estudiotraco.com.br', temConta: false }),
        )
        const { inicio } = abrirComoNaProducao('/cadastro/convite/tok-convite-empresa')
        await api.responder(inicio, caminho)

        expect(await screen.findByText(/A Nebrys convidou/)).toHaveTextContent('ana@estudiotraco.com.br')
        expect(screen.getByLabelText('E-mail')).toHaveValue('ana@estudiotraco.com.br')
        expect(screen.getByLabelText('Seu nome')).toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Criar conta' })).toBeInTheDocument()
        expect(screen.queryByText('Carregando o convite…')).not.toBeInTheDocument()
        expect(api.chamadas.filter((c) => c.caminho === caminho)).toHaveLength(1)
    })

    it('AceiteConvite_ConsultaSaiAntesDoRefreshRecusado_MostraOConvite', async () => {
        const caminho = '/api/v1/auth/convite/consulta'
        const api = naOrdemDeProducao(`POST ${caminho}`, () =>
            json({ nomeTenant: 'Prado Consultoria', email: 'bia@exemplo.com', papel: 'Técnico', temConta: false }),
        )
        const { inicio } = abrirComoNaProducao('/convite/tok-convite')
        await api.responder(inicio, caminho)

        expect(await screen.findByText('Prado Consultoria')).toBeInTheDocument()
        expect(screen.getByLabelText('Seu nome')).toBeInTheDocument()
        expect(screen.queryByText('Carregando o convite…')).not.toBeInTheDocument()
    })

    it('AceiteConvitePortal_ConsultaSaiAntesDoRefreshRecusado_MostraOConvite', async () => {
        const caminho = '/api/v1/auth/portal/convite/consulta'
        const api = naOrdemDeProducao(`POST ${caminho}`, () =>
            json({
                nomePrestador: 'Prado Consultoria',
                nomeContato: 'Marta Souza',
                email: 'marta@bomgrao.com',
                temConta: false,
            }),
        )
        const { inicio } = abrirComoNaProducao('/portal/convite/tok-portal')
        await api.responder(inicio, caminho)

        expect(await screen.findByText(/marta@bomgrao.com/)).toBeInTheDocument()
        expect(screen.queryByText('Carregando o convite…')).not.toBeInTheDocument()
    })

    it('ConfirmacaoEmail_ConsultaSaiAntesDoRefreshRecusado_ConfirmaUmaVezSo', async () => {
        const caminho = '/api/v1/auth/confirmacao-email'
        const api = naOrdemDeProducao(caminho, () => new Response(null, { status: 204 }))
        const { inicio } = abrirComoNaProducao('/conta/confirmacao/tok-email')
        await api.responder(inicio, caminho)

        expect(await screen.findByRole('status')).toHaveTextContent('E-mail confirmado')
        expect(screen.getByRole('link', { name: 'Entrar' })).toBeInTheDocument()
        // Limpar o cache refazia a consulta, e a segunda chamada gastaria um token já usado.
        expect(api.chamadas.filter((c) => c.caminho === caminho)).toHaveLength(1)
    })
})
