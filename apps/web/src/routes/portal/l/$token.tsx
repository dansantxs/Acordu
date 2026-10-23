import { createFileRoute, redirect } from '@tanstack/react-router'
import { api } from '@/shared/api/cliente'
import { ErroApi } from '@/shared/api/problema'
import { abrirSessaoLink, encerrarSessaoLink } from '@/shared/sessao/sessaoLink'

/**
 * O link sem senha do e-mail (`/portal/l/{token}`, RF-POR-08): troca o token por uma sessão restrita à solicitação e
 * vai para `/portal/acompanhar`, sem o token na URL nem no histórico (segurança §9). Só a memória guarda a sessão
 * (decisão 2 do F7).
 */
export const Route = createFileRoute('/portal/l/$token')({
    beforeLoad: async ({ params, context }) => {
        try {
            const { data } = await api.POST('/api/v1/auth/portal/link', { body: { token: params.token } })
            context.consultas.removeQueries({ queryKey: ['portal'] })
            abrirSessaoLink({
                accessToken: data!.accessToken,
                expiraEm: data!.expiraEm,
                solicitacaoId: data!.solicitacaoId,
            })
        } catch (erro) {
            if (!(erro instanceof ErroApi && erro.status === 401)) throw erro
            encerrarSessaoLink('invalido')
        }
        throw redirect({ to: '/portal/acompanhar', replace: true })
    },
})
