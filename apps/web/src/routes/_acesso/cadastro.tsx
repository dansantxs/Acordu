import { createFileRoute } from '@tanstack/react-router'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { FormularioCadastro } from '@/features/sessao/FormularioCadastro'
import { seguirSeJaEntrou } from '@/features/sessao/rotas'

export const Route = createFileRoute('/_acesso/cadastro')({
    beforeLoad: () => seguirSeJaEntrou(),
    component: () => (
        <CapaAcesso titulo="Criar conta" subtitulo="Você entra como dona ou dono da empresa e convida a equipe depois.">
            <FormularioCadastro />
        </CapaAcesso>
    ),
})
