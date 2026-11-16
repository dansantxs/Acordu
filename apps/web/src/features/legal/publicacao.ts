import { notFound } from '@tanstack/react-router'
import { TEXTOS_LEGAIS_PUBLICADOS } from '@/shared/config/ambiente'

/**
 * `beforeLoad` de `/termos` e `/privacidade`: com os textos ainda não publicados (`TEXTOS_LEGAIS_PUBLICADOS`, #194), a
 * rota cai na página "não encontrada" da raiz, como um endereço que não existe.
 */
export function exigirTextosLegaisPublicados(): void {
    if (!TEXTOS_LEGAIS_PUBLICADOS) throw notFound()
}
