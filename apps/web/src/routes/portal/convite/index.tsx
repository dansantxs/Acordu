import { createFileRoute } from '@tanstack/react-router'
import { AceiteConvitePortal } from '@/features/portal/AceiteConvitePortal'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { lerTokenDeLink } from '@/shared/sessao/tokenDaUrl'

export const Route = createFileRoute('/portal/convite/')({
    component: () => (
        <CapaAcesso titulo="Convite para o portal" soPrivacidade>
            <AceiteConvitePortal token={lerTokenDeLink('convite-portal')} />
        </CapaAcesso>
    ),
})
