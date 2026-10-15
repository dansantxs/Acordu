import { createFileRoute } from '@tanstack/react-router'
import { retirarTokenDaUrl } from '@/features/sessao/rotas'

export const Route = createFileRoute('/_acesso/conta/confirmacao/$token')({
    beforeLoad: ({ params }) => {
        throw retirarTokenDaUrl('confirmacao', params.token, '/conta/confirmacao')
    },
})
