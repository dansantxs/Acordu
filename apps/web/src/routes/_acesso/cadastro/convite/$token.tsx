import { createFileRoute } from '@tanstack/react-router'
import { retirarTokenDaUrl } from '@/features/sessao/rotas'

/** Link do e-mail do convite de empresa (`/cadastro/convite/{token}`, contrato §17): o token sai da URL na hora. */
export const Route = createFileRoute('/_acesso/cadastro/convite/$token')({
    beforeLoad: ({ params }) => {
        throw retirarTokenDaUrl('convite-empresa', params.token, '/cadastro/convite')
    },
})
