import { createFileRoute } from '@tanstack/react-router'
import { PainelProjeto } from '@/features/projetos/PainelProjeto'

/** `concluir`: abre direto a confirmação de concluir (sugestão depois da última tarefa, F11-28). */
export const Route = createFileRoute('/_app/projetos/$id')({
    validateSearch: (busca: Record<string, unknown>): { concluir?: true } => ({
        concluir: busca.concluir === true || busca.concluir === 'true' || busca.concluir === 1 ? true : undefined,
    }),
    component: Projeto,
})

function Projeto() {
    const { perfil } = Route.useRouteContext()
    const { id } = Route.useParams()
    const { concluir } = Route.useSearch()
    return <PainelProjeto key={id} perfil={perfil} id={id} abrirConclusao={concluir === true} />
}
