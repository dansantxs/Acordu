import { createFileRoute } from '@tanstack/react-router'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { FormularioNovaSenha } from '@/features/sessao/FormularioNovaSenha'
import { lerTokenDeLink } from '@/shared/sessao/tokenDaUrl'

export const Route = createFileRoute('/_acesso/conta/senha/nova')({
    component: () => (
        <CapaAcesso titulo="Senha nova" subtitulo="Ao trocar, as sessões abertas em outros aparelhos são encerradas.">
            <FormularioNovaSenha token={lerTokenDeLink('senha')} />
        </CapaAcesso>
    ),
})
