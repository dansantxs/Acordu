import { createFileRoute } from '@tanstack/react-router'
import { PainelProjeto } from '@/features/projetos/PainelProjeto'

export const Route = createFileRoute('/_app/projetos/$id')({ component: Projeto })

function Projeto() {
    const { perfil } = Route.useRouteContext()
    const { id } = Route.useParams()
    return <PainelProjeto key={id} perfil={perfil} id={id} />
}
