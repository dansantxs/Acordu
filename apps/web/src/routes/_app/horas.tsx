import { createFileRoute } from '@tanstack/react-router'
import { TelaHoras, type FiltrosHoras } from '@/features/apontamentos/TelaHoras'
import { segundaDaSemana } from '@/shared/format/datas'

const id = (v: unknown) => (typeof v === 'string' && /^[0-9a-f-]{36}$/i.test(v) ? v : undefined)
const data = (v: unknown) => (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v) ? segundaDaSemana(v) : undefined)

export const Route = createFileRoute('/_app/horas')({
    validateSearch: (busca: Record<string, unknown>): FiltrosHoras => ({
        semana: data(busca.semana),
        pessoa: busca.pessoa === 'equipe' ? 'equipe' : id(busca.pessoa),
        cliente: id(busca.cliente),
    }),
    component: Horas,
})

function Horas() {
    const { perfil } = Route.useRouteContext()
    return <TelaHoras perfil={perfil} filtros={Route.useSearch()} />
}
