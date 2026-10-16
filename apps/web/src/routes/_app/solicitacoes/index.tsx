import { createFileRoute } from '@tanstack/react-router'
import { ORDENS, VISOES, type FiltrosFila } from '@/features/solicitacoes/consultas'
import { FilaSolicitacoes } from '@/features/solicitacoes/FilaSolicitacoes'
import { PRIORIDADE } from '@/features/solicitacoes/rotulos'

const texto = (v: unknown) => (typeof v === 'string' && v.trim() ? v.trim() : undefined)
const id = (v: unknown) => (typeof v === 'string' && /^[0-9a-f-]{36}$/i.test(v) ? v : undefined)
const de = <T extends string>(v: unknown, opcoes: readonly T[]) => (opcoes.includes(v as T) ? (v as T) : undefined)

export const Route = createFileRoute('/_app/solicitacoes/')({
    validateSearch: (busca: Record<string, unknown>): FiltrosFila => ({
        visao: de(busca.visao, Object.keys(VISOES) as (keyof typeof VISOES)[]),
        busca: texto(busca.busca),
        cliente: id(busca.cliente),
        responsavel: busca.responsavel === 'eu' ? 'eu' : id(busca.responsavel),
        prioridade: de(busca.prioridade, Object.keys(PRIORIDADE) as (keyof typeof PRIORIDADE)[]),
        categoria: id(busca.categoria),
        sla: de(busca.sla, ['EM_RISCO', 'VIOLADO'] as const),
        ordem: de(busca.ordem, Object.keys(ORDENS) as (keyof typeof ORDENS)[]),
        pagina: Number.isInteger(Number(busca.pagina)) && Number(busca.pagina) > 1 ? Number(busca.pagina) : undefined,
    }),
    component: Solicitacoes,
})

function Solicitacoes() {
    const { perfil } = Route.useRouteContext()
    return <FilaSolicitacoes perfil={perfil} filtros={Route.useSearch()} />
}
