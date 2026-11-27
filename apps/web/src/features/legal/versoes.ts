import type { TipoTextoLegal } from './textosLegais'

/**
 * Versões vigentes dos textos legais (D10 do F14, convenções §4). **Mudar o texto é mudar a versão**: no mesmo PR,
 * `versao` e `vigencia` aqui e no cabeçalho do arquivo (`textos/*.md`), a `impressao` do corpo novo e a constante da API
 * (`TextosLegais`, Application). O teste (`versoes.test.ts`) confere os quatro e mostra a impressão calculada quando o
 * corpo muda.
 *
 * Antes da primeira publicação (revisão do Daniel no PR do F14f), ajustar o texto muda só a impressão. A versão da API
 * vai ao OpenAPI como o `enum` de `versaoTermos` e `versaoPrivacidade` (F14h): o teste compara com ele, e o tipo literal
 * daqui (`as const`) só cabe no corpo do cadastro se for o da API (o `pnpm build` falha se divergirem).
 */
export const VERSOES_TEXTOS_LEGAIS = {
    termos: { versao: '1', vigencia: '2026-11-16', impressao: '42f0d2c2' },
    privacidade: { versao: '1', vigencia: '2026-11-16', impressao: '3f34a499' },
} as const satisfies Record<TipoTextoLegal, { versao: string; vigencia: string; impressao: string }>
