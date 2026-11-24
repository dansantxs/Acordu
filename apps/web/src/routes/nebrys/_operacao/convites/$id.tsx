import { createFileRoute } from '@tanstack/react-router'
import { DetalheConvite } from '@/nebrys/DetalheConvite'

export const Route = createFileRoute('/nebrys/_operacao/convites/$id')({ component: Convite })

function Convite() {
    const { id } = Route.useParams()
    return <DetalheConvite key={id} id={id} />
}
