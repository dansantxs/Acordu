import { createFileRoute } from '@tanstack/react-router'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { FormularioEsqueci } from '@/features/sessao/FormularioEsqueci'

export const Route = createFileRoute('/_acesso/conta/senha/')({
    component: () => (
        <CapaAcesso titulo="Esqueci a senha" subtitulo="Mandamos um link para você criar uma senha nova.">
            <FormularioEsqueci />
        </CapaAcesso>
    ),
})
