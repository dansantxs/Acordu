import { createFileRoute } from '@tanstack/react-router'
import { retirarTokenDaUrl } from '@/features/sessao/rotas'

/** Link do e-mail (`/conta/senha/{token}`): guarda o token e segue para a tela sem ele na URL. */
export const Route = createFileRoute('/_acesso/conta/senha/$token')({
    beforeLoad: ({ params }) => {
        throw retirarTokenDaUrl('senha', params.token, '/conta/senha/nova')
    },
})
