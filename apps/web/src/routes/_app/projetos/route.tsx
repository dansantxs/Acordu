import { createFileRoute, Outlet } from '@tanstack/react-router'
import { ABAS_PROJETO, type AbaProjeto, type FiltrosProjetos } from '@/features/projetos/consultas'
import { TelaProjetos } from '@/features/projetos/TelaProjetos'

const id = (v: unknown) => (typeof v === 'string' && /^[0-9a-f-]{36}$/i.test(v) ? v : undefined)

/** Lista com o detalhe em painel por cima (convenções §4). */
export const Route = createFileRoute('/_app/projetos')({
    validateSearch: (busca: Record<string, unknown>): FiltrosProjetos => ({
        situacao: Object.keys(ABAS_PROJETO).includes(busca.situacao as string)
            ? (busca.situacao as AbaProjeto)
            : undefined,
        cliente: id(busca.cliente),
        pagina: Number.isInteger(Number(busca.pagina)) && Number(busca.pagina) > 1 ? Number(busca.pagina) : undefined,
        novo: busca.novo === true || busca.novo === 1 || busca.novo === '1' || busca.novo === 'true' ? true : undefined,
    }),
    component: Projetos,
})

function Projetos() {
    const { perfil } = Route.useRouteContext()
    return (
        <>
            <TelaProjetos perfil={perfil} filtros={Route.useSearch()} />
            <Outlet />
        </>
    )
}
