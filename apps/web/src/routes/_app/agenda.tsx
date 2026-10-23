import { createFileRoute } from '@tanstack/react-router'
import type { FiltrosAgenda } from '@/features/agenda/consultas'
import { TelaAgenda } from '@/features/agenda/TelaAgenda'
import { segundaDaSemana } from '@/shared/format/datas'

/** `?inicio=` vira sempre uma segunda-feira, para a janela não começar no meio da semana. */
export const Route = createFileRoute('/_app/agenda')({
    validateSearch: (busca: Record<string, unknown>): FiltrosAgenda => ({
        inicio:
            typeof busca.inicio === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(busca.inicio)
                ? segundaDaSemana(busca.inicio)
                : undefined,
        faixas: busca.faixas === 'cliente' ? 'cliente' : undefined,
        vista: busca.vista === 'plano' ? 'plano' : undefined,
    }),
    component: Agenda,
})

function Agenda() {
    const { perfil } = Route.useRouteContext()
    return <TelaAgenda perfil={perfil} filtros={Route.useSearch()} />
}
