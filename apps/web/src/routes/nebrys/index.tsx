import { createFileRoute, redirect } from '@tanstack/react-router'
import { EntradaNebrys } from '@/nebrys/EntradaNebrys'
import { sessaoOperacao } from '@/shared/sessao/sessaoOperacao'

/** Entrada da área: sem sessão de operação, pede e-mail e senha; com ela, segue para as empresas. */
export const Route = createFileRoute('/nebrys/')({
    beforeLoad: () => {
        if (sessaoOperacao()) throw redirect({ to: '/nebrys/empresas', replace: true })
    },
    component: EntradaNebrys,
})
