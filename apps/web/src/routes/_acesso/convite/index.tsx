import { createFileRoute } from '@tanstack/react-router'
import { AceiteConvite } from '@/features/sessao/AceiteConvite'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { lerTokenDeLink } from '@/shared/sessao/tokenDaUrl'

export const Route = createFileRoute('/_acesso/convite/')({
    component: () => (
        <CapaAcesso titulo="Convite para a equipe">
            <AceiteConvite token={lerTokenDeLink('convite')} />
        </CapaAcesso>
    ),
})
