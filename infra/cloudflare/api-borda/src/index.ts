/**
 * Borda da API (ADR 0010 §3, deploy-ci §2.1): proxy fino de `api.acordu.app.br` para o serviço do Cloud Run, sem
 * lógica de negócio.
 * - Repassa método, caminho, query, cabeçalhos e corpo em stream (uploads de até 20 MB, SSE e downloads).
 * - Acrescenta `X-Origem-Token`: com ele configurado, a API recusa (404) quem a chama direto pela URL `*.run.app`.
 * - `X-Forwarded-For` passa a ser só o `CF-Connecting-IP`; o valor que o cliente mandou é descartado, para não forjar
 *   o IP do rate limit e da auditoria (a API confia em dois saltos: este e o front do Google).
 * - Nada fica em cache, e redirecionamentos voltam ao navegador como vieram.
 */
export interface Env {
    /** URL do serviço no Cloud Run, ex.: `https://acordu-api-xxxx-uk.a.run.app`. */
    ORIGEM: string
    /** Segredo compartilhado com a API (`Proxy__TokenOrigem`). */
    TOKEN_ORIGEM: string
}

export const CABECALHO_TOKEN = 'X-Origem-Token'

/** Cabeçalhos que o cliente não pode escolher: o Worker define os dois, e o `Host` sai do endereço de destino. */
const DESCARTADOS = ['host', 'x-forwarded-for', CABECALHO_TOKEN.toLowerCase()]

export async function repassar(requisicao: Request, env: Env, buscar: typeof fetch = fetch): Promise<Response> {
    if (!env.ORIGEM || !env.TOKEN_ORIGEM) return new Response('Borda sem configuração.', { status: 500 })

    // Caminho e query copiados para a URL da origem, campo a campo: montar com `new URL(caminho, ORIGEM)` deixaria
    // `//outro.host/x` (ou `/\\outro.host/x`) virar URL relativa ao protocolo e levar o token para fora.
    const entrada = new URL(requisicao.url)
    const destino = new URL(env.ORIGEM)
    destino.pathname = entrada.pathname
    destino.search = entrada.search

    const cabecalhos = new Headers(requisicao.headers)
    for (const nome of DESCARTADOS) cabecalhos.delete(nome)
    const ip = requisicao.headers.get('CF-Connecting-IP')
    if (ip) cabecalhos.set('X-Forwarded-For', ip)
    cabecalhos.set(CABECALHO_TOKEN, env.TOKEN_ORIGEM)

    const semCorpo = requisicao.method === 'GET' || requisicao.method === 'HEAD'
    return buscar(destino, {
        method: requisicao.method,
        headers: cabecalhos,
        body: semCorpo ? null : requisicao.body,
        redirect: 'manual',
        cache: 'no-store',
    })
}

export default {
    fetch: (requisicao: Request, env: Env) => repassar(requisicao, env),
} satisfies ExportedHandler<Env>
