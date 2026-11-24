import { createFileRoute } from '@tanstack/react-router'
import { TelaCadastro } from '@/features/sessao/TelaCadastro'
import { seguirSeJaEntrou } from '@/features/sessao/rotas'

/** Cadastro sem convite: no modo aberto, o formulário; no modo convite, a lista de espera no lugar (F14-44). */
export const Route = createFileRoute('/_acesso/cadastro/')({
    beforeLoad: () => seguirSeJaEntrou(),
    component: TelaCadastro,
})
