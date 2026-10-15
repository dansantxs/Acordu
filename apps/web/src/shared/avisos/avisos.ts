import { UNSTABLE_ToastQueue as FilaDeAvisos } from 'react-aria-components'
import { mensagemDeErro } from '../api/mensagens'

/**
 * Avisos passageiros no canto da tela (toasts). O desfazer é a resposta padrão a uma ação reversível: em vez de
 * perguntar "tem certeza?", a ação acontece e o aviso oferece desfazer por alguns segundos.
 */
export interface Aviso {
    texto: string
    tom: 'neutro' | 'erro'
    desfazer?: () => unknown
}

export const filaDeAvisos = new FilaDeAvisos<Aviso>({ maxVisibleToasts: 3 })

const DURACAO = 5_000
/** Com desfazer, mais tempo para a pessoa reagir. */
const DURACAO_DESFAZER = 8_000

export function avisar(texto: string, opcoes: { desfazer?: () => unknown } = {}): string {
    return filaDeAvisos.add(
        { texto, tom: 'neutro', desfazer: opcoes.desfazer },
        { timeout: opcoes.desfazer ? DURACAO_DESFAZER : DURACAO },
    )
}

/** Erro de uma ação que não tem formulário onde mostrar (arrastar, marcar, excluir). Fica até ser fechado. */
export function avisarErro(erro: unknown): string {
    return filaDeAvisos.add({ texto: mensagemDeErro(erro) ?? 'Não deu para concluir.', tom: 'erro' })
}
