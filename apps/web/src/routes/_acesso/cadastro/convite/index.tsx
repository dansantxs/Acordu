import { createFileRoute } from '@tanstack/react-router'
import { CadastroPorConvite } from '@/features/sessao/CadastroPorConvite'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { SUBTITULO_CADASTRO } from '@/features/sessao/TelaCadastro'
import { lerTokenDeLink } from '@/shared/sessao/tokenDaUrl'

export const Route = createFileRoute('/_acesso/cadastro/convite/')({
    component: () => (
        <CapaAcesso titulo="Criar conta" subtitulo={SUBTITULO_CADASTRO}>
            <CadastroPorConvite token={lerTokenDeLink('convite-empresa')} />
        </CapaAcesso>
    ),
})
