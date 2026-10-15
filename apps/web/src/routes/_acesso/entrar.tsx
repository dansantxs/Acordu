import { createFileRoute } from '@tanstack/react-router'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { FormularioEntrar } from '@/features/sessao/FormularioEntrar'
import { seguirSeJaEntrou, validarVoltar } from '@/features/sessao/rotas'

export const Route = createFileRoute('/_acesso/entrar')({
    validateSearch: validarVoltar,
    beforeLoad: ({ search }) => seguirSeJaEntrou(search.voltar),
    component: Entrar,
})

function Entrar() {
    const { voltar } = Route.useSearch()
    return (
        <CapaAcesso titulo="Entrar" subtitulo="Bom te ver de novo.">
            <FormularioEntrar voltar={voltar} />
        </CapaAcesso>
    )
}
