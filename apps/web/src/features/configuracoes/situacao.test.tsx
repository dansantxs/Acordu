import { screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Tenant } from '@/shared/sessao/gravacao'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comSessao, json, perfil, servidor } from '@/testes/app'

/** Situação da conta em Configurações › Empresa (spec do F14, F14-12). */

const sla = ['BAIXA', 'MEDIA', 'ALTA', 'URGENTE'].map((prioridade) => ({
    prioridade,
    primeiraRespostaMinutos: 240,
    resolucaoMinutos: 960,
}))
const empresa = { nome: 'Prado Consultoria', responderPara: null, slaPadrao: sla, slaPersonalizado: false }

function app(tenant: Partial<Tenant>) {
    return servidor({
        '/api/v1/auth/refresh': comSessao,
        '/api/v1/auth/eu': () =>
            json({ ...perfil, permissoes: ['configuracao.gerenciar'], tenant: { ...perfil.tenant, ...tenant } }),
        '/api/v1/notificacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 }),
        'GET /api/v1/configuracoes/empresa': () => json(empresa),
    })
}

async function situacao() {
    await abrir('/configuracoes/empresa')
    return screen.findByRole('region', { name: 'Situação da conta' })
}

/** Pares "rótulo: valor" da lista de definição. */
const dados = (secao: HTMLElement) =>
    Object.fromEntries(
        within(secao)
            .getAllByRole('term')
            .map((t) => [t.textContent, t.nextElementSibling?.textContent]),
    )

describe('situação em Configurações › Empresa (F14-12)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('SecaoEmpresa_EmpresaEmTeste_MostraASituacaoEOFimDoTeste', async () => {
        // A prorrogação até 16/12 grava o início de 17/12 no fuso da empresa: o último dia é 16/12.
        app({ situacao: 'TESTE', fimDoTeste: '2026-12-17T03:00:00Z' })
        const secao = await situacao()
        expect(dados(secao)).toEqual({ Situação: 'Em teste', 'Fim do teste': '16/12/2026' })
        expect(secao).toHaveTextContent('Para ativar ou cancelar a conta, fale com a Nebrys: contato@acordu.app.br.')
    })

    it('SecaoEmpresa_EmpresaAtiva_MostraSoASituacao', async () => {
        app({ situacao: 'ATIVA', fimDoTeste: null })
        expect(dados(await situacao())).toEqual({ Situação: 'Ativa' })
    })

    it('SecaoEmpresa_EmpresaVencida_MostraASituacaoEAsDatasDoSoLeitura', async () => {
        app({
            situacao: 'VENCIDA',
            fimDoTeste: '2026-11-09T15:00:00Z',
            somenteLeituraDesde: '2026-11-10T09:00:00Z',
            expurgoPrevistoEm: '2026-12-10T09:00:00Z',
        })
        expect(dados(await situacao())).toEqual({
            Situação: 'Só leitura (o teste venceu)',
            'Só leitura desde': '10/11/2026',
            'Dados guardados até': '10/12/2026',
        })
    })

    it('SecaoEmpresa_EmpresaCancelada_MostraASituacao', async () => {
        app({
            situacao: 'CANCELADA',
            fimDoTeste: null,
            somenteLeituraDesde: '2026-11-12T09:00:00Z',
            expurgoPrevistoEm: '2026-12-12T09:00:00Z',
        })
        expect(dados(await situacao())['Situação']).toBe('Só leitura (conta cancelada)')
    })

    it('SecaoEmpresa_SemPermissaoDeGerenciar_NaoMostraASituacaoNemControle', async () => {
        servidor({
            '/api/v1/auth/refresh': comSessao,
            '/api/v1/auth/eu': () => json({ ...perfil, papel: 'Membro', permissoes: ['cliente.ver'] }),
            '/api/v1/notificacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 }),
        })
        await abrir('/configuracoes/empresa')
        expect(await screen.findByText('Sem acesso a esta seção')).toBeInTheDocument()
        expect(screen.queryByRole('region', { name: 'Situação da conta' })).not.toBeInTheDocument()
    })

    it('SecaoEmpresa_QualquerSituacao_NaoOfereceTrocarASituacao', async () => {
        app({ situacao: 'TESTE', fimDoTeste: '2026-12-17T03:00:00Z' })
        const secao = await situacao()
        expect(within(secao).queryByRole('button')).not.toBeInTheDocument()
        expect(within(secao).queryByRole('combobox')).not.toBeInTheDocument()
        expect(within(secao).queryByRole('radio')).not.toBeInTheDocument()
        // O resto da seção segue o de antes: o formulário da empresa.
        expect(await screen.findByRole('textbox', { name: /Nome da empresa/ })).toBeInTheDocument()
    })
})
