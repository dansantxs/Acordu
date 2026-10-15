import { createFileRoute } from '@tanstack/react-router'
import { NaoEncontrada } from '@/shared/ui/ErroTela'

// Endereço que nenhuma rota atende: cai aqui (e não no 404 da raiz) para continuar dentro da casca, com a guarda.
export const Route = createFileRoute('/_app/$')({ component: NaoEncontrada })
