import { createFileRoute } from '@tanstack/react-router'
import { PaginaLegal } from '@/features/legal/PaginaLegal'

/** Termos de uso (F14-78): página pública, fora das guardas de sessão. */
export const Route = createFileRoute('/termos')({ component: () => <PaginaLegal tipo="termos" /> })
