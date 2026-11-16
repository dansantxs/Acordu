/// <reference types="vite/client" />

interface ImportMetaEnv {
    /** Base da API em outra origem (produção: `https://api.acordu.app.br`). Sem ela, a mesma origem do SPA. */
    readonly VITE_URL_API?: string
    /** `polling` no nível 0 do ADR 0010; sem ela, `sse`. */
    readonly VITE_TEMPO_REAL?: string
    /** Chave do site do Turnstile no cadastro (pública). Sem ela, sem desafio. */
    readonly VITE_TURNSTILE_CHAVE?: string
    /** Commit do build (produção). Sem ela (dev, testes), os erros do app não são relatados à API. */
    readonly VITE_VERSAO?: string
    /** Canal da Nebrys nas faixas de situação da empresa (F14, D15). Sem ela, as faixas não citam endereço. */
    readonly VITE_CONTATO_NEBRYS?: string
    /** `true` publica as páginas e os links dos textos legais (F14f, #194). Sem ela, ficam escondidos. */
    readonly VITE_TEXTOS_LEGAIS_PUBLICADOS?: string
}
