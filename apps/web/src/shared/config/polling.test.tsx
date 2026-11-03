import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { hojeNoFuso, segundaDaSemana, somarDias } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json } from '@/testes/app'

// Build de produção do nível 0: VITE_TEMPO_REAL=polling.
vi.mock('@/shared/config/ambiente', async (original) => ({
    ...(await original<typeof import('./ambiente')>()),
    MODO_TEMPO_REAL: 'polling',
    intervaloDePolling: () => 30_000,
}))

const seg = segundaDaSemana(hojeNoFuso('America/Sao_Paulo'))
const intervalos = (roteador: Awaited<ReturnType<typeof abrir>>, chave: string) =>
    roteador.options.context.consultas
        .getQueryCache()
        .findAll({ queryKey: [chave] })
        .flatMap((q) => q.observers.map((o) => o.options.refetchInterval))

describe('F8-20: polling no nível 0', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('a Agenda recarrega a cada 30 s', async () => {
        comPerfil(['tarefa.ver'], {
            'GET /api/v1/agenda': () =>
                json({ de: seg, ate: somarDias(seg, 13), hoje: seg, dias: [], equipe: [], itens: [] }),
            'GET /api/v1/capacidade': () => json({ semana: seg, pessoas: [], semResponsavel: null }),
        })
        const roteador = await abrir('/agenda')
        await screen.findByRole('heading', { level: 1, name: 'Agenda' })
        expect(intervalos(roteador, 'agenda')).toContain(30_000)
    })

    it('o Quadro recarrega a cada 30 s', async () => {
        comPerfil(['tarefa.ver'], {
            'GET /api/v1/tarefas': () => json({ itens: [], pagina: 1, tamanhoPagina: 50, total: 0 }),
            'GET /api/v1/equipe': () => json([]),
        })
        const roteador = await abrir('/tarefas?modo=quadro')
        await screen.findByRole('heading', { level: 1, name: 'Tarefas' })
        expect(intervalos(roteador, 'tarefas')).toContain(30_000)
    })
})
