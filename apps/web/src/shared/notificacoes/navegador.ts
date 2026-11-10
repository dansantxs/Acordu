/**
 * Notificação do sistema operacional pelo navegador (F12c, decisão D6): o pop-up do Windows, do macOS ou do Android,
 * com o Acordu aberto, mesmo minimizado. Sem servidor: a permissão é do navegador, e a escolha fica nele por pessoa e por
 * empresa (`escopo`), para quem entra depois no mesmo computador não herdar. Quem usa outro aparelho liga de novo lá. Web
 * Push (com a aba fechada) fica para um marco próprio.
 */

const PREFIXO_LIGADO = 'acordu:notificacoes-navegador:'
const PREFIXO_MOSTRADA = 'acordu:notificada:'
/** Aviso mais velho que isso não vira notificação: reabrir o app no dia seguinte não despeja os avisos de ontem. */
const IDADE_MAXIMA_MS = 10 * 60_000
/** Folga para o relógio da máquina diferente do do servidor (o `criadaEm` vem do servidor). */
const FOLGA_RELOGIO_MS = 2 * 60_000
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

/** Escopo da escolha: a pessoa na empresa ativa. */
export function escopoNotificacoes(perfil: { usuarioId: string; tenant: { id: string } }): string {
    return `${perfil.tenant.id}:${perfil.usuarioId}`
}

export function suportaNotificacoes(): boolean {
    return typeof window !== 'undefined' && 'Notification' in window
}

/** Ligada quando a pessoa ligou aqui e o navegador ainda permite. */
export function estadoNotificacoes(escopo: string): EstadoNotificacoes {
    if (!suportaNotificacoes()) return 'sem-suporte'
    if (Notification.permission === 'denied') return 'negada'
    const ligadoEm = armazenamento()?.getItem(PREFIXO_LIGADO + escopo)
    return ligadoEm && Notification.permission === 'granted' ? 'ligada' : 'desligada'
}

/** Pede a permissão (precisa vir de um clique) e guarda a escolha. */
export async function ligarNotificacoes(escopo: string, agora = Date.now()): Promise<EstadoNotificacoes> {
    if (!suportaNotificacoes()) return 'sem-suporte'
    const permissao = Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission()
    if (permissao !== 'granted') return permissao === 'denied' ? 'negada' : 'desligada'
    armazenamento()?.setItem(PREFIXO_LIGADO + escopo, String(agora))
    return 'ligada'
}

export function desligarNotificacoes(escopo: string): void {
    armazenamento()?.removeItem(PREFIXO_LIGADO + escopo)
}

/** Desde quando está ligada (avisos anteriores não viram notificação). */
function ligadaDesde(escopo: string): number | null {
    const valor = Number(armazenamento()?.getItem(PREFIXO_LIGADO + escopo))
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

/** Desfaz a reserva quando a notificação não saiu: outra aba, ou a próxima consulta, tenta de novo. */
function liberar(id: string): void {
    armazenamento()?.removeItem(PREFIXO_MOSTRADA + id)
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
 * Mostra a notificação se a pessoa ligou, o navegador permite, o aviso é novo (depois de ligar e há menos de 10 min,
 * com 2 min de folga de relógio) e nenhuma outra aba já mostrou. Sai também com o Acordu visível: o pop-up é o mesmo
 * aviso do sino, e quem não quer desliga. `abrir` recebe o caminho no clique. Devolve se tentou mostrar: pelo service
 * worker a resposta vem depois, e uma falha lá só desfaz a reserva (o aviso continua no sino de qualquer jeito).
 */
export function notificar(
    escopo: string,
    aviso: Notificavel,
    abrir: (caminho: string) => void,
    agora = Date.now(),
): boolean {
    if (estadoNotificacoes(escopo) !== 'ligada') return false
    const nascido = aviso.criadoEm ? new Date(aviso.criadoEm).getTime() : agora
    const desde = ligadaDesde(escopo)
    if (desde === null || nascido < desde - FOLGA_RELOGIO_MS || agora - nascido > IDADE_MAXIMA_MS + FOLGA_RELOGIO_MS)
        return false
    if (!reservar(aviso.id, agora)) return false
    try {
        const notificacao = new Notification(aviso.titulo, {
            ...(aviso.corpo ? { body: aviso.corpo } : {}),
            tag: aviso.id,
            icon: '/icones/icone-192.png',
        })
        notificacao.onclick = () => {
            window.focus()
            if (aviso.caminho) abrir(aviso.caminho)
            notificacao.close()
        }
        return true
    } catch {
        // Android Chrome só aceita notificação pelo service worker (F12f): mostra por ele, e o clique abre o item lá.
        if (!('serviceWorker' in navigator) || !navigator.serviceWorker.controller) {
            liberar(aviso.id)
            return false
        }
        void navigator.serviceWorker.ready
            .then((registro) =>
                registro.showNotification(aviso.titulo, {
                    ...(aviso.corpo ? { body: aviso.corpo } : {}),
                    tag: aviso.id,
                    icon: '/icones/icone-192.png',
                    data: { caminho: aviso.caminho ?? '/' },
                }),
            )
            .catch(() => liberar(aviso.id))
        return true
    }
}
