import { Link } from '@tanstack/react-router'
import { Checkbox } from 'react-aria-components'
import { ErroApi } from '@/shared/api/problema'
import { TEXTOS_LEGAIS_PUBLICADOS } from '@/shared/config/ambiente'
import { VERSOES_TEXTOS_LEGAIS } from './versoes'

/** Nome do campo do formulário (não vai à API: a API recebe as versões, `versoesAceitas`). */
export const CAMPO_ACEITE = 'aceiteTextos'

const estiloLink = 'text-acento-texto underline underline-offset-2'

/**
 * Caixa de aceite dos termos de uso e da política de privacidade (RF-AUT-11; F14-79, F14-82), desmarcada e obrigatória
 * para enviar, com os links para as duas páginas ao lado (fora do rótulo, para o clique no link não marcar a caixa).
 * Atrás do mesmo ponto das páginas e dos `LinksLegais` (`TEXTOS_LEGAIS_PUBLICADOS`, D16 do F14): com os textos não
 * publicados, não mostra nada e o envio não depende de aceite (F14-114, F14-117). `erro`: a recusa da API nos campos
 * `versaoTermos` ou `versaoPrivacidade` (F14-80).
 */
export function AceiteTextosLegais({ erro }: { erro?: string | null }) {
    if (!TEXTOS_LEGAIS_PUBLICADOS) return null
    return (
        <div role="group" aria-label="Aceite dos textos legais" className="flex flex-col gap-1 text-sm">
            <Checkbox
                name={CAMPO_ACEITE}
                value="sim"
                isRequired
                isInvalid={Boolean(erro)}
                className="group flex items-start gap-2"
            >
                <span
                    aria-hidden="true"
                    className="mt-0.5 grid size-4 shrink-0 place-items-center rounded border border-linha bg-superficie group-data-[invalid]:border-vermelho group-data-[selected]:border-acento group-data-[selected]:bg-acento group-data-[focus-visible]:ring-2 group-data-[focus-visible]:ring-acento"
                >
                    <svg
                        viewBox="0 0 12 12"
                        className="size-3 text-botao-tinta opacity-0 group-data-[selected]:opacity-100"
                    >
                        <path d="M2.5 6.5 5 9l4.5-6" fill="none" stroke="currentColor" strokeWidth="2" />
                    </svg>
                </span>
                Li e aceito os termos de uso e a política de privacidade do Acordu.
            </Checkbox>
            <p className="pl-6 text-suave">
                Leia os{' '}
                <Link to="/termos" target="_blank" rel="noopener" className={estiloLink}>
                    termos de uso<span className="sr-only"> (abre em outra aba)</span>
                </Link>{' '}
                e a{' '}
                <Link to="/privacidade" target="_blank" rel="noopener" className={estiloLink}>
                    política de privacidade<span className="sr-only"> (abre em outra aba)</span>
                </Link>
                .
            </p>
            {erro && (
                <p role="alert" className="pl-6 text-vermelho">
                    {erro}
                </p>
            )}
        </div>
    )
}

/**
 * As versões que a pessoa aceitou, para o corpo do cadastro e do aceite de convite da equipe (D10): só com os textos
 * publicados e a caixa marcada. Com os textos não publicados, nada vai (a API ignoraria, F14-116).
 */
export function versoesAceitas(dados: FormData) {
    if (!TEXTOS_LEGAIS_PUBLICADOS || dados.get(CAMPO_ACEITE) === null) return {}
    return {
        versaoTermos: VERSOES_TEXTOS_LEGAIS.termos.versao,
        versaoPrivacidade: VERSOES_TEXTOS_LEGAIS.privacidade.versao,
    }
}

/** A recusa do aceite pela API (F14-80): o campo `versaoTermos` ou `versaoPrivacidade`, mostrado junto da caixa. */
export function erroDoAceite(erro: unknown): string | null {
    if (!(erro instanceof ErroApi)) return null
    return erro.doCampo('versaoTermos') ?? erro.doCampo('versaoPrivacidade') ?? null
}
