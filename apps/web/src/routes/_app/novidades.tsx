import { createFileRoute } from '@tanstack/react-router'
import { TelaNovidades } from '@/features/novidades/TelaNovidades'

/** Notas de versão da equipe (M4), para todos. */
export const Route = createFileRoute('/_app/novidades')({
    component: TelaNovidades,
})
