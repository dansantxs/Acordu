import { createFileRoute, Outlet, redirect } from '@tanstack/react-router'
import { CascaPortal } from '@/features/portal/CascaPortal'
import { consultaEu } from '@/features/portal/consultas'
import { ErroApi } from '@/shared/api/problema'
import { consultaPerfil, ehContato } from '@/shared/sessao/perfil'
import { aguardarSessao } from '@/shared/sessao/sessao'
import { encerrarSessaoLink, sessaoLink } from '@/shared/sessao/sessaoLink'
import { SessaoIndisponivel } from '@/shared/sessao/SessaoIndisponivel'
import { NaoEncontrada } from '@/shared/ui/ErroTela'
import { TelaSemConexao } from '@/shared/ui/TelaSemConexao'

/**
 * Guarda do portal com conta (F7a): sem sessão vai para o login, lembrando para onde voltar; quem é da equipe vai para
 * o app. O perfil e o `GET /portal/eu` (prestador, escopo, contratos) ficam no contexto das rotas filhas.
 */
export const Route = createFileRoute('/portal/_contato')({
    beforeLoad: async ({ context, location }) => {
        // Entrar com conta substitui a sessão por link desta aba (as consultas do portal trocariam de cliente).
        if (sessaoLink()) {
            encerrarSessaoLink('saiu')
            context.consultas.removeQueries({ queryKey: ['portal'] })
        }
        const estado = await aguardarSessao()
        if (estado.situacao === 'indisponivel') throw new SessaoIndisponivel()
        if (estado.situacao !== 'ativa') throw redirect({ to: '/entrar', search: { voltar: location.href } })
        try {
            const perfil = await context.consultas.ensureQueryData(consultaPerfil)
            if (!ehContato(perfil)) throw redirect({ to: '/' })
            const eu = await context.consultas.ensureQueryData(consultaEu)
            return { perfil, eu }
        } catch (erro) {
            if (erro instanceof ErroApi && erro.status === 401) {
                throw redirect({ to: '/entrar', search: { voltar: location.href } })
            }
            throw erro
        }
    },
    component: Portal,
    notFoundComponent: NaoEncontrada,
    errorComponent: TelaSemConexao,
})

function Portal() {
    const { eu } = Route.useRouteContext()
    return (
        <CascaPortal eu={eu}>
            <Outlet />
        </CascaPortal>
    )
}
