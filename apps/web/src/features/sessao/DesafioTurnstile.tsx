import { useEffect, useRef, useState } from 'react'
import { Alerta } from '@/shared/ui/Alerta'

/** O que o script do Cloudflare põe em `window.turnstile` (modo explícito). */
export interface ApiTurnstile {
    render(
        elemento: HTMLElement,
        opcoes: {
            sitekey: string
            action: string
            language: string
            callback: (token: string) => void
            'expired-callback': () => void
            'error-callback': () => void
        },
    ): string | undefined
    reset(id: string): void
    remove(id: string): void
}

declare global {
    interface Window {
        turnstile?: ApiTurnstile
    }
}

const SCRIPT = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit'
let carregamento: Promise<void> | null = null

/** Carrega o script uma vez por página; falhou, a próxima montagem tenta de novo. */
function carregarTurnstile(): Promise<void> {
    if (window.turnstile) return Promise.resolve()
    carregamento ??= new Promise<void>((resolver, rejeitar) => {
        const script = document.createElement('script')
        script.src = SCRIPT
        script.async = true
        script.onload = () => resolver()
        script.onerror = () => {
            carregamento = null
            script.remove()
            rejeitar(new Error('Turnstile não carregou.'))
        }
        document.head.append(script)
    })
    return carregamento
}

/** Ação do widget, a mesma que a API confere em cada rota pública (convenções §3; contrato §2 e §16). */
export type AcaoTurnstile = 'registro' | 'lista-espera'

/**
 * Desafio do Cloudflare Turnstile no cadastro e na lista de espera (RNF-SEG-10; spec do F9, F9-39 a F9-41; F14-54):
 * entrega o token (ou nulo, quando vence ou dá erro) e é renovado sempre que `versao` muda, porque o token só vale uma
 * vez. `acao` é a da rota que vai receber o token: o de uma não vale na outra.
 */
export function DesafioTurnstile({
    chave,
    versao,
    aoMudar,
    acao = 'registro',
}: {
    chave: string
    versao: number
    aoMudar: (token: string | null) => void
    acao?: AcaoTurnstile
}) {
    const elemento = useRef<HTMLDivElement>(null)
    const widget = useRef<string | null>(null)
    const retorno = useRef(aoMudar)
    const [falhou, setFalhou] = useState(false)

    useEffect(() => {
        retorno.current = aoMudar
    }, [aoMudar])

    useEffect(() => {
        let ativo = true
        carregarTurnstile()
            .then(() => {
                if (!ativo || !elemento.current || !window.turnstile) return
                const id = window.turnstile.render(elemento.current, {
                    sitekey: chave,
                    action: acao,
                    language: 'pt-br',
                    callback: (token) => retorno.current(token),
                    'expired-callback': () => retorno.current(null),
                    'error-callback': () => retorno.current(null),
                })
                widget.current = id ?? null
                if (!id) setFalhou(true)
            })
            .catch(() => ativo && setFalhou(true))
        return () => {
            ativo = false
            if (widget.current) window.turnstile?.remove(widget.current)
            widget.current = null
        }
    }, [chave, acao])

    useEffect(() => {
        if (versao === 0 || !widget.current) return
        window.turnstile?.reset(widget.current)
        retorno.current(null)
    }, [versao])

    return (
        <div>
            <div ref={elemento} />
            {falhou && <Alerta>Não deu para carregar a verificação. Confira a conexão e recarregue a página.</Alerta>}
        </div>
    )
}
