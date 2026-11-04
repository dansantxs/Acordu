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

/**
 * Desafio do Cloudflare Turnstile no cadastro (RNF-SEG-10; spec do F9, F9-39 a F9-41): entrega o token (ou nulo, quando
 * vence ou dá erro) e é renovado sempre que `versao` muda, porque o token só vale uma vez.
 */
export function DesafioTurnstile({
    chave,
    versao,
    aoMudar,
}: {
    chave: string
    versao: number
    aoMudar: (token: string | null) => void
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
                    action: 'registro',
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
    }, [chave])

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
