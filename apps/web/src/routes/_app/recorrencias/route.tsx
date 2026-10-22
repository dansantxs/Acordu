import { createFileRoute, Outlet } from '@tanstack/react-router'
import { ABAS_RECORRENCIA, type AbaRecorrencia, type FiltrosRecorrencias } from '@/features/recorrencias/consultas'
import { TelaRecorrencias } from '@/features/recorrencias/TelaRecorrencias'

const id = (v: unknown) => (typeof v === 'string' && /^[0-9a-f-]{36}$/i.test(v) ? v : undefined)

/** Lista com o detalhe em painel por cima (convenções §4). */
export const Route = createFileRoute('/_app/recorrencias')({
    validateSearch: (busca: Record<string, unknown>): FiltrosRecorrencias => ({
        status:
            Object.keys(ABAS_RECORRENCIA).includes(busca.status as string) && busca.status !== 'ativas'
                ? (busca.status as AbaRecorrencia)
                : undefined,
        cliente: id(busca.cliente),
        pagina: Number.isInteger(Number(busca.pagina)) && Number(busca.pagina) > 1 ? Number(busca.pagina) : undefined,
        nova: busca.nova === true || busca.nova === 1 || busca.nova === '1' || busca.nova === 'true' ? true : undefined,
    }),
    component: Recorrencias,
})

function Recorrencias() {
    const { perfil } = Route.useRouteContext()
    return (
        <>
            <TelaRecorrencias perfil={perfil} filtros={Route.useSearch()} />
            <Outlet />
        </>
    )
}
