/**
 * Notificação do sistema operacional pelo navegador (F12c, decisão D6): o pop-up do Windows, do macOS ou do Android,
 * com o Acordu aberto, mesmo minimizado. Sem servidor: a permissão e a escolha ficam neste navegador. Quem usa outro
 * aparelho liga de novo lá. Web Push (com a aba fechada) fica para um marco próprio.
 */

const CHAVE_LIGADO = 'acordu:notificacoes-navegador'
const PREFIXO_MOSTRADA = 'acordu:notificada:'
/** Aviso mais velho que isso não vira notificação: reabrir o app no dia seguinte não despeja os avisos de ontem. */
const IDADE_MAXIMA_MS = 10 * 60_000
/** Quanto tempo a marca de "já mostrada" dura (as abas consultam o sino a cada 60 s). */
const VALIDADE_MARCA_MS = 24 * 60 * 60_000

export type EstadoNotificacoes = 'sem-suporte' | 'negada' | 'desligada' | 'ligada'

function armazenamento(): Storage | null {
    try {
        return window.localStorage
    } catch {
        return null
    }
}

export function suportaNotificacoes(): boolean {
    return typeof window !== 'undefined' && 'Notification' in window
}

/** Ligada quando a pessoa ligou aqui e o navegador ainda permite. */
export function estadoNotificacoes(): EstadoNotificacoes {
    if (!suportaNotificacoes()) return 'sem-suporte'
    if (Notification.permission === 'denied') return 'negada'
    const ligadoEm = armazenamento()?.getItem(CHAVE_LIGADO)
    return ligadoEm && Notification.permission === 'granted' ? 'ligada' : 'desligada'
}

/** Pede a permissão (precisa vir de um clique) e guarda a escolha. */
export async function ligarNotificacoes(agora = Date.now()): Promise<EstadoNotificacoes> {
    if (!suportaNotificacoes()) return 'sem-suporte'
    const permissao = Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission()
    if (permissao !== 'granted') return permissao === 'denied' ? 'negada' : 'desligada'
    armazenamento()?.setItem(CHAVE_LIGADO, String(agora))
    return 'ligada'
}

export function desligarNotificacoes(): void {
    armazenamento()?.removeItem(CHAVE_LIGADO)
}

/** Desde quando está ligada (avisos anteriores não viram notificação). */
function ligadaDesde(): number | null {
    const valor = Number(armazenamento()?.getItem(CHAVE_LIGADO))
    return Number.isFinite(valor) && valor > 0 ? valor : null
}

/**
 * Marca o aviso como mostrado e diz se esta aba deve mostrar. Com duas abas, a primeira a marcar mostra (F12-24); o
 * `tag` da notificação cobre a corrida que sobrar, porque o sistema troca uma pela outra em vez de empilhar.
 */
function reservar(id: string, agora: number): boolean {
    const local = armazenamento()
    if (!local) return true
    const chave = PREFIXO_MOSTRADA + id
    const marca = Number(local.getItem(chave))
    if (marca && agora - marca < VALIDADE_MARCA_MS) return false
    local.setItem(chave, String(agora))
    // Faxina das marcas vencidas, para o armazenamento não crescer sem fim.
    for (let i = local.length - 1; i >= 0; i--) {
        const outra = local.key(i)
        if (outra?.startsWith(PREFIXO_MOSTRADA) && agora - Number(local.getItem(outra)) >= VALIDADE_MARCA_MS)
            local.removeItem(outra)
    }
    return true
}

export interface Notificavel {
    /** Único por aviso: decide a repetição entre abas. */
    id: string
    titulo: string
    corpo?: string
    /** Caminho interno aberto no clique (F12-23). */
    caminho?: string | null
    /** Quando o aviso nasceu (ISO); sem, conta como agora. */
    criadoEm?: string
}

/**
 * Mostra a notificação se a pessoa ligou, o navegador permite, o aviso é novo (depois de ligar e há menos de 10 min) e
 * nenhuma outra aba já mostrou. `abrir` recebe o caminho no clique.
 */
export function notificar(aviso: Notificavel, abrir: (caminho: string) => void, agora = Date.now()): boolean {
    if (estadoNotificacoes() !== 'ligada') return false
    const nascido = aviso.criadoEm ? new Date(aviso.criadoEm).getTime() : agora
    const desde = ligadaDesde()
    if (desde === null || nascido < desde || agora - nascido > IDADE_MAXIMA_MS) return false
    if (!reservar(aviso.id, agora)) return false
    try {
        const notificacao = new Notification(aviso.titulo, {
            ...(aviso.corpo ? { body: aviso.corpo } : {}),
            tag: aviso.id,
            icon: '/favicon.svg',
        })
        notificacao.onclick = () => {
            window.focus()
            if (aviso.caminho) abrir(aviso.caminho)
            notificacao.close()
        }
        return true
    } catch {
        // Android Chrome só aceita notificação pelo service worker (F12f); aqui, sem ela, segue o aviso do app.
        return false
    }
}
