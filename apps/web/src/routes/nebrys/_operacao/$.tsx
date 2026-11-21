import { createFileRoute } from '@tanstack/react-router'
import { NaoEncontradaNebrys } from '@/nebrys/CascaNebrys'

// Endereço da área que nenhuma rota atende: passa pela guarda (sem sessão, entrada) e cai aqui, dentro da área.
export const Route = createFileRoute('/nebrys/_operacao/$')({ component: NaoEncontradaNebrys })
