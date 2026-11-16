import { createFileRoute } from '@tanstack/react-router'
import { PaginaLegal } from '@/features/legal/PaginaLegal'
import { exigirTextosLegaisPublicados } from '@/features/legal/publicacao'

/** Termos de uso (F14-78): página pública, fora das guardas de sessão; escondida até a publicação (#194). */
export const Route = createFileRoute('/termos')({
    beforeLoad: exigirTextosLegaisPublicados,
    component: () => <PaginaLegal tipo="termos" />,
})
