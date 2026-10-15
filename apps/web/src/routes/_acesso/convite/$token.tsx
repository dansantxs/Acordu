import { createFileRoute } from '@tanstack/react-router'
import { retirarTokenDaUrl } from '@/features/sessao/rotas'

export const Route = createFileRoute('/_acesso/convite/$token')({
    beforeLoad: ({ params }) => {
        throw retirarTokenDaUrl('convite', params.token, '/convite')
    },
})
