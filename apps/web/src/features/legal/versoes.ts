import type { TipoTextoLegal } from './textosLegais'

/**
 * Versões vigentes dos textos legais (D10 do F14, convenções §4). **Mudar o texto é mudar a versão**: no mesmo PR,
 * `versao` e `vigencia` aqui e no cabeçalho do arquivo (`textos/*.md`), e a `impressao` do corpo novo. O teste
 * (`versoes.test.ts`) confere os três e mostra a impressão calculada quando o corpo muda.
 *
 * Antes da primeira publicação (revisão do Daniel no PR do F14f), ajustar o texto muda só a impressão. No F14h a API
 * ganha a mesma versão (`TextosLegais`) para conferir o aceite, e o teste passa a comparar com ela também.
 */
export const VERSOES_TEXTOS_LEGAIS: Record<TipoTextoLegal, { versao: string; vigencia: string; impressao: string }> = {
    termos: { versao: '1', vigencia: '2026-11-16', impressao: 'b3faa24a' },
    privacidade: { versao: '1', vigencia: '2026-11-16', impressao: 'c2c62735' },
}
