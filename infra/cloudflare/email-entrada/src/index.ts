/**
 * Entrada por e-mail (spec do F15, F15d e F15-89; design do F15, D1, D5 e D11; ADR 0016, itens 1 e 2): o Email Worker da
 * regra catch-all do subdomínio de entrada. Não tem regra de negócio: repassa o MIME bruto, em stream, a
 * `POST /internal/email/entrada` pela borda (que acrescenta o `X-Origem-Token`), com o envelope e um veredito neutro.
 * - Acima de `TAMANHO_MAXIMO`, recusa a entrega (`setReject`, erro permanente ao servidor de origem, que devolve o aviso
 *   de não entrega ao remetente) sem chamar a API.
 * - `202` (ou outro 2xx) da API: entregue. `413`: recusa como acima. Qualquer outra resposta ou falha de rede: lança
 *   erro, o Email Routing devolve falha temporária e o servidor de origem repete mais tarde (a API é idempotente).
 * - O log leva só o resultado, se veio o carimbo de autenticação do provedor e o veredito: nada dos endereços, do
 *   assunto nem do conteúdo (RNF-OPS-01, ADR 0012).
 */

export interface Env {
    /** Segredo compartilhado com a API (`EmailEntrada__Token`). Sem ele, nada é repassado (falha temporária). */
    TOKEN_ENTRADA?: string
    /** URL da rota de entrada; padrão: pela borda. */
    DESTINO?: string
    /** Tamanho máximo do e-mail em bytes (`EmailEntrada__TamanhoMaximoBytes`); padrão 25 MiB. */
    TAMANHO_MAXIMO?: string
    /** Identificador do servidor que carimba a autenticação (o do Email Routing); padrão `mx.cloudflare.net`. */
    SERVIDOR_AUTENTICACAO?: string
}

export const DESTINO_PADRAO = 'https://api.acordu.app.br/internal/email/entrada'
export const TAMANHO_PADRAO = 25 * 1024 * 1024
export const SERVIDOR_PADRAO = 'mx.cloudflare.net'
export const RECUSA_TAMANHO = 'Mensagem acima do tamanho máximo aceito.'

/** `pass`: o domínio do remetente confirma que o e-mail saiu dele; `fail`: não confirma; `none`: sem carimbo. */
export type Autenticidade = 'pass' | 'fail' | 'none'

export type Resultado = 'repassada' | 'recusada-tamanho'

/** O pedaço da mensagem que o Worker usa (o `ForwardableEmailMessage` do Email Routing). */
export type Mensagem = Pick<ForwardableEmailMessage, 'from' | 'to' | 'headers' | 'raw' | 'rawSize' | 'setReject'>

const CABECALHOS_AUTENTICACAO = ['authentication-results', 'arc-authentication-results'] as const

/** Cada instância começa (no ARC, depois de `i=N;`) pelo identificador do servidor, seguido de `;`. */
const INICIO_DE_INSTANCIA = /,\s*(?=(?:i\s*=\s*\d+\s*;\s*)?[a-z0-9.-]+\s*;)/i

/**
 * Os resultados do carimbo do provedor: a primeira instância de `Authentication-Results` (ou, sem ela,
 * `ARC-Authentication-Results`) cujo identificador é o do servidor do provedor. As de outro servidor são ignoradas,
 * porque o remetente pode escrevê-las. O `Headers` junta as repetições com ", ", na ordem da mensagem (a do provedor,
 * acrescentada por último, vem primeiro).
 */
export function carimbo(headers: Headers, servidor: string = SERVIDOR_PADRAO): string | null {
    for (const nome of CABECALHOS_AUTENTICACAO) {
        const valor = headers.get(nome)
        if (!valor) continue
        for (const instancia of valor.split(INICIO_DE_INSTANCIA)) {
            const semArc = instancia.replace(/^\s*i\s*=\s*\d+\s*;/i, '')
            const fim = semArc.indexOf(';')
            const id = (fim < 0 ? semArc : semArc.slice(0, fim)).trim().toLowerCase()
            if (id === servidor.toLowerCase()) return fim < 0 ? '' : semArc.slice(fim + 1)
        }
    }
    return null
}

/** O domínio do endereço do `From`, ou nulo sem endereço ou com mais de um domínio. */
export function dominioDoFrom(from: string | null): string | null {
    const dominios = new Set([...(from ?? '').matchAll(/@([a-z0-9.-]+[a-z0-9])/gi)].map((m) => m[1]!.toLowerCase()))
    return dominios.size === 1 ? [...dominios][0]! : null
}

/** Alinhamento relaxado do DMARC, aproximado: o mesmo domínio ou um subdomínio do outro. */
function alinhado(assinante: string, remetente: string): boolean {
    return assinante.includes('.') && (assinante === remetente || remetente.endsWith('.' + assinante) || assinante.endsWith('.' + remetente))
}

/**
 * O veredito de autenticidade (design D11): DMARC `pass` → `pass`; DMARC que falhou → `fail`; sem DMARC publicado
 * (`none` ou ausente), DKIM `pass` alinhado ao domínio do `From` → `pass` (SPF sozinho não basta: não sobrevive ao
 * redirecionamento e não olha o `From`); sem o carimbo do provedor → `none`.
 */
export function autenticidade(headers: Headers, servidor: string = SERVIDOR_PADRAO): Autenticidade {
    const resultados = carimbo(headers, servidor)
    if (resultados === null) return 'none'
    const dmarc = /(?:^|;)\s*dmarc\s*=\s*([a-z]+)/i.exec(resultados)?.[1]?.toLowerCase()
    if (dmarc === 'pass') return 'pass'
    if (dmarc && dmarc !== 'none') return 'fail'
    const dominio = dominioDoFrom(headers.get('from'))
    if (!dominio) return 'fail'
    for (const dkim of resultados.matchAll(/(?:^|;)\s*dkim\s*=\s*pass\b[^;]*?\bheader\.d\s*=\s*([a-z0-9.-]+)/gi)) {
        if (alinhado(dkim[1]!.toLowerCase(), dominio)) return 'pass'
    }
    return 'fail'
}

/** Cabeçalho HTTP só aceita texto de um byte: o que não for ASCII visível vira "?" (endereço SMTPUTF8, controle). */
function ascii(valor: string): string {
    return valor.replace(/[^\x20-\x7e]/g, '?')
}

function tamanhoMaximo(env: Env): number {
    const valor = Number(env.TAMANHO_MAXIMO)
    return Number.isSafeInteger(valor) && valor > 0 ? valor : TAMANHO_PADRAO
}

/** Um e-mail que o Email Routing entregou: recusa por tamanho ou repassa à API. */
export async function receber(mensagem: Mensagem, env: Env, buscar: typeof fetch = fetch): Promise<Resultado> {
    if (mensagem.rawSize > tamanhoMaximo(env)) {
        mensagem.setReject(RECUSA_TAMANHO)
        console.log('Entrada por e-mail: recusada-tamanho (antes da API).')
        return 'recusada-tamanho'
    }
    // Sem o segredo, a API responderia 404: falha temporária, para o e-mail não se perder até a configuração voltar.
    if (!env.TOKEN_ENTRADA) throw new Error('Entrada por e-mail sem TOKEN_ENTRADA.')

    const servidor = env.SERVIDOR_AUTENTICACAO || SERVIDOR_PADRAO
    const veredito = autenticidade(mensagem.headers, servidor)
    const resposta = await buscar(env.DESTINO || DESTINO_PADRAO, {
        method: 'POST',
        headers: {
            'Content-Type': 'message/rfc822',
            'User-Agent': 'acordu-email-entrada',
            'X-Email-Token': env.TOKEN_ENTRADA,
            'X-Email-Remetente': ascii(mensagem.from),
            'X-Email-Destinatario': ascii(mensagem.to),
            'X-Email-Autenticidade': veredito,
            // O plano gratuito não expõe nota de spam nem antivírus ao Worker (design D11): o filtro é o que o Email
            // Routing já recusou antes de entregar.
            'X-Email-Filtro': 'ok',
        },
        body: mensagem.raw,
        redirect: 'manual',
    })
    const comCarimbo = carimbo(mensagem.headers, servidor) !== null ? 'presente' : 'ausente'

    if (resposta.status === 413) {
        mensagem.setReject(RECUSA_TAMANHO)
        console.log(`Entrada por e-mail: recusada-tamanho (API); carimbo ${comCarimbo}.`)
        return 'recusada-tamanho'
    }
    if (resposta.status < 200 || resposta.status > 299) {
        console.log(`Entrada por e-mail: a API respondeu ${resposta.status}; o servidor de origem vai repetir.`)
        throw new Error(`Rota de entrada respondeu ${resposta.status}.`)
    }
    console.log(`Entrada por e-mail: repassada (${resposta.status}); carimbo ${comCarimbo}; autenticidade ${veredito}.`)
    return 'repassada'
}

export default {
    async email(mensagem, env) {
        await receber(mensagem, env)
    },
    // Sem rota pública (workers_dev = false e sem domínio); se alguém chegar aqui, não há nada.
    async fetch() {
        return new Response(null, { status: 404 })
    },
} satisfies ExportedHandler<Env>
