import { createFileRoute } from '@tanstack/react-router'
import { AjudaPortal } from '@/features/portal/AjudaPortal'

/** Guia do portal (M3), para todo contato com conta. */
export const Route = createFileRoute('/portal/_contato/ajuda')({
    component: Ajuda,
})

function Ajuda() {
    const { eu } = Route.useRouteContext()
    return <AjudaPortal eu={eu} />
}
