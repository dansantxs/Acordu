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
    /** Quem fornece o Acordu nos textos (ADR 0017). Sem ela, "Nebrys". */
    readonly VITE_NOME_FORNECEDOR?: string
    /** Artigo do nome do fornecedor: `a` (padrão), `o` ou `nenhum` (ADR 0017). */
    readonly VITE_ARTIGO_FORNECEDOR?: string
    /** Canal do fornecedor nas faixas e recusas (F14, D15). Sem ela, os textos não citam endereço. */
    readonly VITE_CONTATO_FORNECEDOR?: string
    /** `true` publica as páginas e os links dos textos legais (F14f, #194). Sem ela, ficam escondidos. */
    readonly VITE_TEXTOS_LEGAIS_PUBLICADOS?: string
}
