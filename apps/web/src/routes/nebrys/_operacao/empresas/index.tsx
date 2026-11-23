import { createFileRoute } from '@tanstack/react-router'
import type { FiltrosEmpresas } from '@/nebrys/consultas'
import { ListaEmpresas } from '@/nebrys/ListaEmpresas'
import { SITUACOES } from '@/nebrys/situacao'
import type { SituacaoEmpresa } from '@/nebrys/api'

export const Route = createFileRoute('/nebrys/_operacao/empresas/')({
    validateSearch: (busca: Record<string, unknown>): FiltrosEmpresas => ({
        fase: SITUACOES.includes(busca.fase as SituacaoEmpresa) ? (busca.fase as SituacaoEmpresa) : undefined,
        pagina: Number.isInteger(Number(busca.pagina)) && Number(busca.pagina) > 1 ? Number(busca.pagina) : undefined,
    }),
    component: Empresas,
})

function Empresas() {
    return <ListaEmpresas filtros={Route.useSearch()} />
}
