import { createFileRoute } from '@tanstack/react-router'
import { Demonstracao } from '@/nebrys/Demonstracao'

export const Route = createFileRoute('/nebrys/_operacao/demonstracao')({ component: Demonstracao })
