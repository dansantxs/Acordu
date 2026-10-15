/**
 * Sessão do SPA (ADR 0007): o access token fica só nesta memória; o refresh mora no cookie HttpOnly e só a API o lê.
 *
 * Renovar entre abas é serializado: duas abas que apresentam o mesmo refresh ao mesmo tempo seriam tratadas como reuso,
 * e a API derrubaria todas as sessões. Por isso:
 * - na aba, chamadas simultâneas a `renovar` compartilham a mesma promessa;
 * - entre abas, a renovação roda sob o Web Lock `acordu-refresh`. Quem espera o lock encontra o cookie já rotacionado
 *   pela aba anterior, e, se ela anunciou um token novo no `BroadcastChannel` durante a espera, usa esse token sem
 *   chamar a API;
 * - falha de rede não é repetida automaticamente (só um 401 encerra a sessão).
 */
export interface TokenDeAcesso {
    accessToken: string
    expiraEm: string
}

export type Situacao = 'carregando' | 'anonima' | 'ativa'

/** Por que a sessão mudou: `troca` é outra empresa ou outro usuário (o cache inteiro deixa de valer). */
export type Motivo = 'inicio' | 'renovacao' | 'troca' | 'encerramento'

export interface EstadoSessao {
    situacao: Situacao
    motivo: Motivo
}

type Mensagem = { tipo: 'token'; token: TokenDeAcesso; troca: boolean } | { tipo: 'encerrada' }

const CANAL = 'acordu-sessao'
const LOCK = 'acordu-refresh'
/** Renova um pouco antes de expirar, e trata como vencido o token a menos disso do fim. */
const ANTECEDENCIA_MS = 60_000

let token: TokenDeAcesso | null = null
let estado: EstadoSessao = { situacao: 'carregando', motivo: 'inicio' }
let emAndamento: Promise<boolean> | null = null
let temporizador: ReturnType<typeof setTimeout> | undefined
const ouvintes = new Set<() => void>()
const canal: BroadcastChannel | null = typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel(CANAL)

/** Chamada à API que troca o cookie por um token novo; `null` quando a API responde 401. Definida pelo cliente HTTP. */
export type Renovador = () => Promise<TokenDeAcesso | null>

let renovador: Renovador = async () => null

export function definirRenovador(funcao: Renovador): void {
    renovador = funcao
}

export function tokenAtual(): string | null {
    return token?.accessToken ?? null
}

export function estadoSessao(): EstadoSessao {
    return estado
}

export function assinar(ouvinte: () => void): () => void {
    ouvintes.add(ouvinte)
    return () => ouvintes.delete(ouvinte)
}

function mudar(novo: EstadoSessao): void {
    estado = novo
    ouvintes.forEach((ouvinte) => ouvinte())
}

function expiraEmMs(t: TokenDeAcesso): number {
    return new Date(t.expiraEm).getTime()
}

/** O token acaba em menos de um minuto (ou já acabou), e a próxima requisição deve renovar antes. */
export function tokenVencendo(agora = Date.now()): boolean {
    return token !== null && expiraEmMs(token) - agora < ANTECEDENCIA_MS
}

function agendarRenovacao(): void {
    clearTimeout(temporizador)
    if (!token) return
    const espera = Math.max(expiraEmMs(token) - Date.now() - ANTECEDENCIA_MS, 0)
    // Falha aqui não encerra nada além do que `renovar` já encerra; a próxima requisição tenta de novo.
    temporizador = setTimeout(() => void renovar().catch(() => undefined), espera)
}

function guardar(novo: TokenDeAcesso, motivo: Motivo): void {
    token = novo
    agendarRenovacao()
    mudar({ situacao: 'ativa', motivo })
}

function limpar(): void {
    token = null
    clearTimeout(temporizador)
    mudar({ situacao: 'anonima', motivo: 'encerramento' })
}

/** Sessão aberta por login, registro, aceite de convite ou troca de empresa: vale também para as outras abas. */
export function abrirSessao(novo: TokenDeAcesso): void {
    guardar(novo, 'troca')
    canal?.postMessage({ tipo: 'token', token: novo, troca: true } satisfies Mensagem)
}

/** Sessão encerrada aqui (logout ou refresh recusado): as outras abas também saem. */
export function encerrarSessao(): void {
    limpar()
    canal?.postMessage({ tipo: 'encerrada' } satisfies Mensagem)
}

async function comLock<T>(tarefa: () => Promise<T>): Promise<T> {
    if (typeof navigator === 'undefined' || !navigator.locks) return tarefa()
    return navigator.locks.request(LOCK, tarefa)
}

/**
 * Pede um access token novo. `true` se a sessão continua ativa; `false` se a API recusou o refresh (a sessão é
 * encerrada). Erro de rede propaga, sem encerrar nem repetir.
 */
export function renovar(): Promise<boolean> {
    if (emAndamento) return emAndamento
    const antes = token?.expiraEm
    emAndamento = comLock(async () => {
        // Outra aba renovou enquanto esperávamos o lock e já mandou o token novo.
        if (token && token.expiraEm !== antes && !tokenVencendo()) return true
        const novo = await renovador()
        if (!novo) {
            encerrarSessao()
            return false
        }
        guardar(novo, estado.situacao === 'ativa' ? 'renovacao' : 'inicio')
        canal?.postMessage({ tipo: 'token', token: novo, troca: false } satisfies Mensagem)
        return true
    }).finally(() => {
        emAndamento = null
    })
    return emAndamento
}

/** Na primeira carga: tenta recuperar a sessão pelo cookie. Sem cookie válido, fica anônima. */
export async function iniciarSessao(): Promise<void> {
    if (estado.situacao !== 'carregando') return
    try {
        await renovar()
    } catch {
        // Sem rede na primeira carga: trata como anônima; o login mostra o erro se continuar assim.
        if (estado.situacao === 'carregando') mudar({ situacao: 'anonima', motivo: 'inicio' })
    }
}

/** Resolve quando a sessão deixa de estar carregando. */
export function aguardarSessao(): Promise<EstadoSessao> {
    if (estado.situacao !== 'carregando') return Promise.resolve(estado)
    return new Promise((resolver) => {
        const cancelar = assinar(() => {
            if (estado.situacao === 'carregando') return
            cancelar()
            resolver(estado)
        })
    })
}

canal?.addEventListener('message', (evento: MessageEvent<Mensagem>) => {
    const mensagem = evento.data
    if (mensagem.tipo === 'encerrada') {
        if (estado.situacao !== 'anonima') limpar()
        return
    }
    guardar(mensagem.token, mensagem.troca ? 'troca' : estado.situacao === 'ativa' ? 'renovacao' : 'inicio')
})

/** Só para testes: volta ao estado inicial. */
export function reiniciarParaTestes(): void {
    token = null
    emAndamento = null
    clearTimeout(temporizador)
    estado = { situacao: 'carregando', motivo: 'inicio' }
    ouvintes.clear()
}
