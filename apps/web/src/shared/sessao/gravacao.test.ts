import { describe, expect, it } from 'vitest'
import { ErroApi } from '../api/problema'
import { diasRestantesDoTeste, ehRecusaSomenteLeitura, podeGravar, ultimoDiaDoTeste } from './gravacao'

const FUSO = 'America/Sao_Paulo'
const emTeste = (fimDoTeste: string | null, diasFaixaTeste = 7) => ({
    situacao: 'TESTE' as const,
    fimDoTeste,
    fusoHorario: FUSO,
    diasFaixaTeste,
})

describe('situação da empresa no front (F14e)', () => {
    it('PodeGravar_PorSituacao_SoVencidaECanceladaNaoGravam', () => {
        expect(podeGravar({ situacao: 'TESTE' })).toBe(true)
        expect(podeGravar({ situacao: 'ATIVA' })).toBe(true)
        expect(podeGravar({ situacao: 'VENCIDA' })).toBe(false)
        expect(podeGravar({ situacao: 'CANCELADA' })).toBe(false)
    })

    it('DiasRestantesDoTeste_DentroDaJanela_ContaDiasCivisNoFusoAteOUltimoDia', () => {
        // 16/11, 10:00 em São Paulo; o teste vai até 19/11 às 15:00 (fim = cadastro + 30 dias).
        const agora = new Date('2026-11-16T13:00:00Z')
        expect(diasRestantesDoTeste(emTeste('2026-11-19T18:00:00Z'), agora)).toBe(3)
        // Prorrogado até 18/11: grava o início de 19/11 no fuso, e o último dia é 18/11.
        expect(diasRestantesDoTeste(emTeste('2026-11-19T03:00:00Z'), agora)).toBe(2)
        expect(diasRestantesDoTeste(emTeste('2026-11-16T20:00:00Z'), agora)).toBe(0)
        // Exatamente 7 dias antes do fim ainda está na janela.
        expect(diasRestantesDoTeste(emTeste('2026-11-23T13:00:00Z'), agora)).toBe(7)
    })

    it('DiasRestantesDoTeste_ForaDaJanelaOuForaDoTeste_Nulo', () => {
        const agora = new Date('2026-11-16T13:00:00Z')
        expect(diasRestantesDoTeste(emTeste('2026-11-23T13:00:01Z'), agora)).toBeNull()
        expect(diasRestantesDoTeste(emTeste(null), agora)).toBeNull()
        expect(
            diasRestantesDoTeste(
                { situacao: 'ATIVA', fimDoTeste: '2026-11-17T00:00:00Z', fusoHorario: FUSO, diasFaixaTeste: 7 },
                agora,
            ),
        ).toBeNull()
    })

    it('DiasRestantesDoTeste_JanelaDaSessao_UsaOsDiasDoAuthEu', () => {
        // A janela vem do /auth/eu (Empresas__DiasFaixaTeste): com 3 dias, 4 dias antes do fim ainda não há faixa.
        const agora = new Date('2026-11-16T13:00:00Z')
        expect(diasRestantesDoTeste(emTeste('2026-11-20T13:00:00Z', 3), agora)).toBeNull()
        expect(diasRestantesDoTeste(emTeste('2026-11-19T13:00:00Z', 3), agora)).toBe(3)
        expect(diasRestantesDoTeste(emTeste('2026-11-20T13:00:00Z', 10), agora)).toBe(4)
    })

    it('DiasRestantesDoTeste_FimJaPassadoAindaEmTeste_Zero', () => {
        // O diário ainda não venceu a empresa: segue gravável, e a faixa diz que termina hoje.
        expect(diasRestantesDoTeste(emTeste('2026-11-16T12:00:00Z'), new Date('2026-11-16T13:00:00Z'))).toBe(0)
    })

    it('UltimoDiaDoTeste_InicioDoDiaSeguinte_MostraODiaAnterior', () => {
        expect(ultimoDiaDoTeste('2026-12-17T03:00:00Z', FUSO)).toBe('16/12/2026')
        expect(ultimoDiaDoTeste('2026-12-13T15:00:00Z', FUSO)).toBe('13/12/2026')
    })

    it('EhRecusaSomenteLeitura_TiposDaEquipeEDoPortal_Verdadeiro', () => {
        const erro = (tipo: string, status = 422) =>
            new ErroApi(status, { type: `https://acordu.app.br/erros/${tipo}` })
        expect(ehRecusaSomenteLeitura(erro('empresa-somente-leitura'))).toBe(true)
        expect(ehRecusaSomenteLeitura(erro('portal-indisponivel'))).toBe(true)
        expect(ehRecusaSomenteLeitura(erro('transicao-invalida'))).toBe(false)
        expect(ehRecusaSomenteLeitura(erro('sem-permissao', 403))).toBe(false)
        expect(ehRecusaSomenteLeitura(new Error('x'))).toBe(false)
    })
})
