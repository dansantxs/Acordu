/// <reference types="vite/client" />

interface ImportMetaEnv {
    /** Base da API em outra origem (produção: `https://api.acordu.app.br`). Sem ela, a mesma origem do SPA. */
    readonly VITE_URL_API?: string
    /** `polling` no nível 0 do ADR 0010; sem ela, `sse`. */
    readonly VITE_TEMPO_REAL?: string
}
