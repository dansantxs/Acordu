import { createFileRoute } from '@tanstack/react-router'
import { NovaSolicitacaoPortal } from '@/features/portal/NovaSolicitacaoPortal'

export const Route = createFileRoute('/portal/_contato/nova')({ component: NovaSolicitacaoPortal })
