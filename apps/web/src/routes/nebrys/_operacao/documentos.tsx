import { createFileRoute } from '@tanstack/react-router'
import { LiberacaoDocumento } from '@/nebrys/LiberacaoDocumento'

export const Route = createFileRoute('/nebrys/_operacao/documentos')({ component: LiberacaoDocumento })
