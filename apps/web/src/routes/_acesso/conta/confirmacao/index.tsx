import { createFileRoute } from '@tanstack/react-router'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { ConfirmacaoEmail } from '@/features/sessao/ConfirmacaoEmail'
import { lerTokenDeLink } from '@/shared/sessao/tokenDaUrl'

export const Route = createFileRoute('/_acesso/conta/confirmacao/')({
    component: () => (
        <CapaAcesso titulo="Confirmação de e-mail">
            <ConfirmacaoEmail token={lerTokenDeLink('confirmacao')} />
        </CapaAcesso>
    ),
})
