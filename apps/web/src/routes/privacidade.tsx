import { createFileRoute } from '@tanstack/react-router'
import { PaginaLegal } from '@/features/legal/PaginaLegal'

/** Política de privacidade (F14-78): página pública, fora das guardas de sessão. */
export const Route = createFileRoute('/privacidade')({ component: () => <PaginaLegal tipo="privacidade" /> })
