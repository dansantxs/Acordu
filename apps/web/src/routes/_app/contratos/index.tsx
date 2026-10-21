import { createFileRoute } from '@tanstack/react-router'
import { ABAS_CONTRATO, type AbaContrato, type FiltrosContratos } from '@/features/contratos/consultas'
import { TelaContratos } from '@/features/contratos/TelaContratos'

const id = (v: unknown) => (typeof v === 'string' && /^[0-9a-f-]{36}$/i.test(v) ? v : undefined)

export const Route = createFileRoute('/_app/contratos/')({
    validateSearch: (busca: Record<string, unknown>): FiltrosContratos => ({
        estado: Object.keys(ABAS_CONTRATO).includes(busca.estado as string) ? (busca.estado as AbaContrato) : undefined,
        cliente: id(busca.cliente),
        pagina: Number.isInteger(Number(busca.pagina)) && Number(busca.pagina) > 1 ? Number(busca.pagina) : undefined,
        novo: busca.novo === true || busca.novo === 1 || busca.novo === '1' || busca.novo === 'true' ? true : undefined,
    }),
    component: Contratos,
})

function Contratos() {
    const { perfil } = Route.useRouteContext()
    return <TelaContratos perfil={perfil} filtros={Route.useSearch()} />
}
