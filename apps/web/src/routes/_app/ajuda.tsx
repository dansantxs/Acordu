import { createFileRoute } from '@tanstack/react-router'
import { TelaAjuda } from '@/features/ajuda/TelaAjuda'

/** Manual (`?topico=` abre já na seção, pelo "?" de cada tela). */
export const Route = createFileRoute('/_app/ajuda')({
    validateSearch: (busca: Record<string, unknown>): { topico?: string } => ({
        topico: typeof busca.topico === 'string' && /^[a-z-]{2,40}$/.test(busca.topico) ? busca.topico : undefined,
    }),
    component: Ajuda,
})

function Ajuda() {
    const { perfil } = Route.useRouteContext()
    const { topico } = Route.useSearch()
    return <TelaAjuda perfil={perfil} secao={topico} />
}
