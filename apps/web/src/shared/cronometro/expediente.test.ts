import { describe, expect, it } from 'vitest'
import { expedienteAgora } from './expediente'

const calendario = {
    fusoHorario: 'America/Sao_Paulo',
    expediente: [
        { diaSemana: 'SEGUNDA' as const, inicio: '08:00', fim: '18:00' },
        { diaSemana: 'TERCA' as const, inicio: '08:00', fim: '18:00' },
    ],
    feriados: ['2026-11-10'],
}
// Segunda, 09/11/2026, horário de São Paulo (UTC-3).
const as = (hora: string, dia = '2026-11-09') => new Date(`${dia}T${hora}:00-03:00`)

describe('expediente agora (F12-14)', () => {
    it('dentro da janela do dia, diz há quanto começou', () => {
        expect(expedienteAgora(calendario, as('10:30'))).toEqual({ minutosDesdeInicio: 150 })
    })

    it.each([
        ['antes de abrir', as('07:59')],
        ['no fechamento', as('18:00')],
        ['dia sem expediente (domingo)', as('10:00', '2026-11-08')],
        ['feriado do calendário', as('10:00', '2026-11-10')],
    ])('%s: fechado', (_, instante) => {
        expect(expedienteAgora(calendario, instante)).toBeNull()
    })

    it('usa o fuso da empresa, não o da máquina', () => {
        // 13:30 em UTC é 10:30 em São Paulo.
        expect(expedienteAgora(calendario, new Date('2026-11-09T13:30:00Z'))).toEqual({ minutosDesdeInicio: 150 })
    })
})
