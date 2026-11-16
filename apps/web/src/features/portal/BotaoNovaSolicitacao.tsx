import { Link } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import { useGravacao } from '@/shared/sessao/gravacao'
import { Botao } from '@/shared/ui/Botao'

/**
 * "Nova solicitação" do portal: link para o formulário ou, com o envio indisponível (F14-28), um botão indisponível
 * ligado à mensagem neutra da casca. Um link não fica desabilitado; o botão sim, e o leitor de tela diz por quê.
 */
export function BotaoNovaSolicitacao({ className, children }: { className: string; children: ReactNode }) {
    const { podeGravar } = useGravacao()
    if (!podeGravar)
        return (
            <Botao grava variante="secundario">
                {children}
            </Botao>
        )
    return (
        <Link to="/portal/nova" className={className}>
            {children}
        </Link>
    )
}
