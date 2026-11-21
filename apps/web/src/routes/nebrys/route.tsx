import { createFileRoute, notFound, Outlet } from '@tanstack/react-router'
import { aguardarSessao } from '@/shared/sessao/sessao'
import { sessaoLink } from '@/shared/sessao/sessaoLink'
import { encerrarSessaoOperacao } from '@/shared/sessao/sessaoOperacao'

/**
 * Área da Nebrys (`/nebrys`, ADR 0014; F14-64). Com a sessão da equipe, do contato do portal ou por link aberta nesta
 * aba, a área não existe: cai no "não encontrado" da raiz, e uma sessão de operação que houvesse acaba (as sessões não
 * se misturam). Nenhuma tela da empresa nem do portal leva até aqui.
 */
export const Route = createFileRoute('/nebrys')({
    beforeLoad: async () => {
        const estado = await aguardarSessao()
        if (estado.situacao === 'ativa' || sessaoLink()) {
            encerrarSessaoOperacao('saiu')
            throw notFound()
        }
    },
    component: Outlet,
})
