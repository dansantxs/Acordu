import { createFileRoute, Outlet } from '@tanstack/react-router'
import { ABAS, type Aba, type FiltrosTarefas } from '@/features/tarefas/consultas'
import { TelaTarefas } from '@/features/tarefas/TelaTarefas'

const texto = (v: unknown) => (typeof v === 'string' && v.trim() ? v.trim() : undefined)
const id = (v: unknown) => (typeof v === 'string' && /^[0-9a-f-]{36}$/i.test(v) ? v : undefined)

/** Lista (e, no F3, quadro) com o detalhe em painel por cima, como as solicitações (convenções §4). */
export const Route = createFileRoute('/_app/tarefas')({
    validateSearch: (busca: Record<string, unknown>): FiltrosTarefas => ({
        modo: busca.modo === 'quadro' ? 'quadro' : undefined,
        aba: Object.keys(ABAS).includes(busca.aba as string) ? (busca.aba as Aba) : undefined,
        busca: texto(busca.busca),
        cliente: id(busca.cliente),
        responsavel:
            busca.responsavel === 'eu' || busca.responsavel === 'ninguem' ? busca.responsavel : id(busca.responsavel),
        pagina: Number.isInteger(Number(busca.pagina)) && Number(busca.pagina) > 1 ? Number(busca.pagina) : undefined,
    }),
    component: Tarefas,
})

function Tarefas() {
    const { perfil } = Route.useRouteContext()
    return (
        <>
            <TelaTarefas perfil={perfil} filtros={Route.useSearch()} />
            <Outlet />
        </>
    )
}
