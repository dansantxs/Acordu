import { createFileRoute } from '@tanstack/react-router'
import type { FiltrosAuditoria } from '@/features/auditoria/consultas'
import { SecaoAuditoria } from '@/features/configuracoes/SecaoAuditoria'
import { SecaoProtegida } from '@/features/configuracoes/TelaConfiguracoes'

const id = (v: unknown) => (typeof v === 'string' && /^[0-9a-f-]{36}$/i.test(v) ? v : undefined)
const data = (v: unknown) => (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v) ? v : undefined)

export const Route = createFileRoute('/_app/configuracoes/auditoria')({
    validateSearch: (busca: Record<string, unknown>): FiltrosAuditoria => ({
        de: data(busca.de),
        ate: data(busca.ate),
        acao: typeof busca.acao === 'string' && /^[a-z_.]{3,60}$/.test(busca.acao) ? busca.acao : undefined,
        quem: id(busca.quem),
        alvo: id(busca.alvo),
        pagina: Number.isInteger(Number(busca.pagina)) && Number(busca.pagina) > 1 ? Number(busca.pagina) : undefined,
    }),
    component: Pagina,
})

function Pagina() {
    const { perfil } = Route.useRouteContext()
    return (
        <SecaoProtegida perfil={perfil} para="/configuracoes/auditoria">
            <SecaoAuditoria perfil={perfil} filtros={Route.useSearch()} />
        </SecaoProtegida>
    )
}
