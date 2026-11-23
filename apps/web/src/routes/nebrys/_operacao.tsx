import { createFileRoute, redirect } from '@tanstack/react-router'
import { CascaNebrys, NaoEncontradaNebrys } from '@/nebrys/CascaNebrys'
import { sessaoOperacao } from '@/shared/sessao/sessaoOperacao'

/**
 * Guarda das telas da área (F14-64): sem sessão de operação (nunca entrou, o prazo acabou, saiu), nada da área aparece
 * e o endereço volta para a entrada, sem lembrar para onde ia.
 */
export const Route = createFileRoute('/nebrys/_operacao')({
    beforeLoad: () => {
        if (!sessaoOperacao()) throw redirect({ to: '/nebrys', replace: true })
    },
    component: CascaNebrys,
    notFoundComponent: NaoEncontradaNebrys,
})
