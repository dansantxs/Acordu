import { describe, expect, it } from 'vitest'
import { fraseAuditoria, rotuloAcao, type Registro } from './acoes'

const registro = (acao: string, dados: unknown) =>
    ({
        id: '01',
        ocorridoEm: '2026-11-26T13:00:00Z',
        acao,
        usuario: null,
        alvoTipo: 'empresa',
        alvoId: 't1',
        dados,
        ip: null,
    }) as unknown as Registro

describe('trilha do recebimento por e-mail (F15-93)', () => {
    it('tem rótulo no filtro', () => {
        expect(rotuloAcao('configuracao.recebimento-email')).toBe('Recebimento por e-mail')
    })

    it('diz se ligou ou desligou pelo depois', () => {
        const ligou = registro('configuracao.recebimento-email', {
            antes: { recebimentoEmail: false },
            depois: { recebimentoEmail: true },
        })
        const desligou = registro('configuracao.recebimento-email', {
            antes: { recebimentoEmail: true },
            depois: { recebimentoEmail: false },
        })

        expect(fraseAuditoria(ligou, new Map())).toBe('ligou o recebimento por e-mail')
        expect(fraseAuditoria(desligou, new Map())).toBe('desligou o recebimento por e-mail')
    })
})
