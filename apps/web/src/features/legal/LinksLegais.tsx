import { Link } from '@tanstack/react-router'
import { TEXTOS_LEGAIS_PUBLICADOS } from '@/shared/config/ambiente'

const estiloLink = 'underline-offset-4 hover:text-tinta hover:underline'

/**
 * Links para os textos legais (F14-84), sem pedir aceite (o aceite é do cadastro e do convite da equipe, F14h). Abrem
 * em outra aba para não perder o que foi digitado no formulário ao lado. `soPrivacidade`: onde os termos não cabem
 * (portal e aceite de convite do portal: a relação do contato é com o prestador, decisão 12 do G2). Com os textos
 * ainda não publicados (`TEXTOS_LEGAIS_PUBLICADOS`, #194), não mostra nada.
 */
export function LinksLegais({
    soPrivacidade = false,
    className = '',
}: {
    soPrivacidade?: boolean
    className?: string
}) {
    if (!TEXTOS_LEGAIS_PUBLICADOS) return null
    return (
        <nav aria-label="Textos legais" className={`flex flex-wrap gap-x-4 gap-y-1 text-xs text-suave ${className}`}>
            <Link to="/privacidade" target="_blank" rel="noopener" className={estiloLink}>
                Política de privacidade<span className="sr-only"> (abre em outra aba)</span>
            </Link>
            {!soPrivacidade && (
                <Link to="/termos" target="_blank" rel="noopener" className={estiloLink}>
                    Termos de uso<span className="sr-only"> (abre em outra aba)</span>
                </Link>
            )}
        </nav>
    )
}
