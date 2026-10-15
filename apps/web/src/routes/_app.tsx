import { createFileRoute, redirect, type ErrorComponentProps } from '@tanstack/react-router'
import { Casca } from '@/features/casca/Casca'
import { ErroApi } from '@/shared/api/problema'
import { ErroTela, NaoEncontrada } from '@/shared/ui/ErroTela'
import { TelaSemConexao } from '@/shared/ui/TelaSemConexao'
import { consultaPerfil, ehContato } from '@/shared/sessao/perfil'
import { aguardarSessao } from '@/shared/sessao/sessao'
import { SessaoIndisponivel } from '@/shared/sessao/SessaoIndisponivel'

/**
 * Guarda do app da equipe: sem sessão vai para o login (lembrando para onde voltar); contato de cliente vai para o
 * portal. O perfil fica no contexto das rotas filhas, e a casca (capa lateral, paleta) envolve todas.
 */
export const Route = createFileRoute('/_app')({
    beforeLoad: async ({ context, location }) => {
        const estado = await aguardarSessao()
        if (estado.situacao === 'indisponivel') throw new SessaoIndisponivel()
        if (estado.situacao !== 'ativa') throw redirect({ to: '/entrar', search: { voltar: location.href } })
        try {
            const perfil = await context.consultas.ensureQueryData(consultaPerfil)
            if (ehContato(perfil)) throw redirect({ to: '/portal' })
            return { perfil }
        } catch (erro) {
            if (erro instanceof ErroApi && erro.status === 401) {
                throw redirect({ to: '/entrar', search: { voltar: location.href } })
            }
            throw erro
        }
    },
    component: AppDaEquipe,
    notFoundComponent: NaoEncontrada,
    errorComponent: ErroDoApp,
})

function AppDaEquipe() {
    const { perfil } = Route.useRouteContext()
    return <Casca perfil={perfil} />
}

/** Sessão que não pôde ser conferida vira a tela de tentar de novo; o resto segue a tela de erro padrão. */
function ErroDoApp(props: ErrorComponentProps) {
    return props.error instanceof SessaoIndisponivel ? <TelaSemConexao /> : <ErroTela {...props} />
}
