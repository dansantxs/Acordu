import { createFileRoute } from '@tanstack/react-router'
import { retirarTokenDaUrl } from '@/features/sessao/rotas'

/** O link do e-mail (`/portal/convite/{token}`): guarda o token e tira da URL (segurança §9). */
export const Route = createFileRoute('/portal/convite/$token')({
    beforeLoad: ({ params }) => {
        throw retirarTokenDaUrl('convite-portal', params.token, '/portal/convite')
    },
})
