import { createFileRoute } from '@tanstack/react-router'
import { PaginaLegal } from '@/features/legal/PaginaLegal'
import { exigirTextosLegaisPublicados } from '@/features/legal/publicacao'

/** Política de privacidade (F14-78): página pública, fora das guardas de sessão; escondida até a publicação (#194). */
export const Route = createFileRoute('/privacidade')({
    beforeLoad: exigirTextosLegaisPublicados,
    component: () => <PaginaLegal tipo="privacidade" />,
})
