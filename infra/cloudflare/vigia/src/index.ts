/**
 * Vigia da API (ADR 0012; spec do F13, F13-34 a F13-36). Um Cron Trigger de 5 min chama `GET /api/health` pela borda,
 * como um cliente. Duas falhas seguidas (ou mais de 15 s) avisam "API fora do ar desde HH:MM" em #alertas, uma vez por
 * queda; a volta avisa "API voltou, fora por N min". Uma falha só (partida a frio) não avisa. O estado fica no KV e só
 * é gravado quando muda (o plano gratuito tem 1.000 gravações por dia; uma queda gasta três).
 */

export interface Env {
    ESTADO: KVNamespace
    /** Webhook de #alertas (segredo). Sem ele, a vigia só registra no log. */
    DISCORD_ALERTAS?: string
    /** O que vigiar; padrão: o health pela borda. */
    ALVO?: string
}

export interface Estado {
    /** Checagens seguidas que falharam. */
    falhas: number
    /** Quando a primeira dessas falhas aconteceu (ISO). */
    foraDesde?: string
    /** Se a queda já foi avisada (a volta só é avisada se a queda foi). */
    avisada: boolean
}

export const CHAVE = 'estado'
export const ALVO_PADRAO = 'https://api.acordu.app.br/api/health'
export const FALHAS_PARA_AVISAR = 2
export const ESPERA_MS = 15_000

const EM_PE: Estado = { falhas: 0, avisada: false }

/** HH:MM em Brasília, o fuso da operação. */
export function horaDeBrasilia(instante: Date): string {
    return new Intl.DateTimeFormat('pt-BR', {
        timeZone: 'America/Sao_Paulo',
        hour: '2-digit',
        minute: '2-digit',
        hourCycle: 'h23',
    }).format(instante)
}

async function saudavel(alvo: string, buscar: typeof fetch): Promise<boolean> {
    try {
        const resposta = await buscar(alvo, {
            headers: { 'User-Agent': 'acordu-vigia' },
            signal: AbortSignal.timeout(ESPERA_MS),
        })
        return resposta.status === 200
    } catch {
        return false
    }
}

async function avisar(env: Env, texto: string, buscar: typeof fetch): Promise<boolean> {
    if (!env.DISCORD_ALERTAS) {
        console.log(`Vigia sem webhook de #alertas: ${texto}`)
        return false
    }
    try {
        const resposta = await buscar(env.DISCORD_ALERTAS, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            // Menções desligadas, como em todo aviso de operação.
            body: JSON.stringify({ content: texto, allowed_mentions: { parse: [] } }),
            signal: AbortSignal.timeout(10_000),
        })
        if (resposta.ok) return true
        console.log(`Aviso da vigia não saiu: HTTP ${resposta.status}`)
    } catch (erro) {
        console.log(`Aviso da vigia não saiu: ${erro instanceof Error ? erro.name : 'erro'}`)
    }
    return false
}

async function lerEstado(kv: KVNamespace): Promise<Estado> {
    const lido = await kv.get<Estado>(CHAVE, 'json')
    return lido ?? EM_PE
}

/** Uma checagem: lê o estado, chama o alvo, avisa se for o caso e grava o estado novo se mudou. */
export async function checar(env: Env, agora: Date, buscar: typeof fetch = fetch): Promise<Estado> {
    const antes = await lerEstado(env.ESTADO)
    let depois: Estado

    if (await saudavel(env.ALVO ?? ALVO_PADRAO, buscar)) {
        depois = EM_PE
        if (antes.avisada && antes.foraDesde) {
            const minutos = Math.max(1, Math.round((agora.getTime() - Date.parse(antes.foraDesde)) / 60_000))
            // Se o aviso da volta não sair, fica como está e tenta de novo na próxima checagem.
            if (!(await avisar(env, `**API voltou** · fora por ${minutos} min.`, buscar))) depois = antes
        }
    } else {
        const foraDesde = antes.foraDesde ?? agora.toISOString()
        depois = { falhas: antes.falhas + 1, foraDesde, avisada: antes.avisada }
        if (!antes.avisada && depois.falhas >= FALHAS_PARA_AVISAR) {
            const saiu = await avisar(env, `**API fora do ar** desde ${horaDeBrasilia(new Date(foraDesde))} (Brasília).`, buscar)
            depois = { ...depois, avisada: saiu }
        }
        // Já avisada: a contagem não precisa subir no KV a cada checagem.
        if (antes.avisada) depois = antes
    }

    if (JSON.stringify(depois) !== JSON.stringify(antes)) await env.ESTADO.put(CHAVE, JSON.stringify(depois))
    return depois
}

export default {
    async scheduled(controle, env, ctx) {
        ctx.waitUntil(checar(env, new Date(controle.scheduledTime)))
    },
    // Sem rota pública (workers_dev = false e sem domínio); se alguém chegar aqui, não há nada.
    async fetch() {
        return new Response(null, { status: 404 })
    },
} satisfies ExportedHandler<Env>
