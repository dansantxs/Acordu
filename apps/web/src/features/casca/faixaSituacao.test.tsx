import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { Tenant } from '@/shared/sessao/gravacao'
import { perfil } from '@/testes/app'
import { FaixaSituacao } from './FaixaSituacao'

/** Limites da janela de aviso de 7 dias (spec do F14: F14-10, F14-11; decisão 3 do G2), com o relógio injetado. */

// 16/11/2026, 10:00 em São Paulo.
const AGORA = new Date('2026-11-16T13:00:00Z')

const emTeste = (fimDoTeste: string): Tenant => ({ ...perfil.tenant, situacao: 'TESTE', fimDoTeste })
const faixa = () => screen.queryByRole('status')

describe('FaixaSituacao: limites da janela de aviso', () => {
    it.each([
        // fim do teste, texto esperado
        ['2026-11-24T13:00:00Z', null], // 8 dias: fora da janela
        ['2026-11-23T13:00:00Z', /Faltam 7 dias para o fim do teste do Acordu/], // 7 dias: dentro
        ['2026-11-17T20:00:00Z', /Falta 1 dia para o fim do teste do Acordu \(vai até 17\/11\/2026\)/], // 1 dia
        ['2026-11-16T20:00:00Z', /O teste do Acordu termina hoje \(16\/11\/2026\)/], // 0 dia
        // Prorrogado até 20/11: grava o início de 21/11 no fuso, e a faixa diz o último dia.
        ['2026-11-21T03:00:00Z', /Faltam 4 dias para o fim do teste do Acordu \(vai até 20\/11\/2026\)/],
    ])('EmTesteComFimEm_%s_%s', (fim, esperado) => {
        render(<FaixaSituacao tenant={emTeste(fim)} agora={AGORA} />)
        if (esperado === null) expect(faixa()).not.toBeInTheDocument()
        else expect(faixa()).toHaveTextContent(esperado)
    })

    it.each([
        // A janela vem da sessão (tenant.diasFaixaTeste, Empresas__DiasFaixaTeste): com 3 dias, 5 dias antes não há faixa.
        [3, '2026-11-21T13:00:00Z', null],
        [3, '2026-11-20T13:00:00Z', null], // 4 dias: fora
        [3, '2026-11-19T13:00:00Z', /Faltam 3 dias para o fim do teste do Acordu/], // 3 dias: dentro
        [10, '2026-11-24T13:00:00Z', /Faltam 8 dias para o fim do teste do Acordu/], // janela maior
    ])('JanelaDaSessao_%s_dias_FimEm_%s', (diasFaixaTeste, fim, esperado) => {
        render(<FaixaSituacao tenant={{ ...emTeste(fim), diasFaixaTeste }} agora={AGORA} />)
        if (esperado === null) expect(faixa()).not.toBeInTheDocument()
        else expect(faixa()).toHaveTextContent(esperado)
    })

    it('EmTesteProrrogadoParaForaDaJanela_SaiDaFaixa', () => {
        // Prorrogar o teste para 30 dias à frente esconde a faixa (F14-11).
        render(<FaixaSituacao tenant={emTeste('2026-12-16T03:00:00Z')} agora={AGORA} />)
        expect(faixa()).not.toBeInTheDocument()
    })

    it('EmpresaAtivaComFimDoTesteProximo_NaoMostraFaixa', () => {
        render(
            <FaixaSituacao
                tenant={{ ...perfil.tenant, situacao: 'ATIVA', fimDoTeste: '2026-11-17T20:00:00Z' }}
                agora={AGORA}
            />,
        )
        expect(faixa()).not.toBeInTheDocument()
    })
})
