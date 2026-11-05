import { ErroApi } from './problema'

/**
 * Texto para mostrar a quem usa, a partir de qualquer falha de uma chamada. Erros de campo ficam nos campos (via
 * `validationErrors`), então aqui só sai mensagem geral quando não há campo para apontar.
 */
export function mensagemDeErro(erro: unknown): string | null {
    if (erro instanceof ErroApi) {
        if (Object.keys(erro.campos).length > 0) return null
        if (erro.status === 429) return 'Muitas tentativas seguidas. Aguarde um minuto e tente de novo.'
        if (erro.status >= 500) return 'O servidor não conseguiu concluir agora. Tente de novo em instantes.'
        return erro.message
    }
    // Só a falha de rede do `fetch` é "sem conexão" (Chrome: "Failed to fetch"; Firefox: "NetworkError…"; Safari: "Load
    // failed"). Outro `TypeError` é defeito do código e não pode mandar a pessoa conferir a internet.
    if (erro instanceof TypeError && /fetch|network|load failed/i.test(erro.message))
        return 'Sem conexão com o servidor. Confira a internet e tente de novo.'
    return 'Algo deu errado. Tente de novo.'
}

/** Erros de campo no formato do `Form` do React Aria (`validationErrors`). */
export function errosDeCampo(erro: unknown): Record<string, string[]> {
    return erro instanceof ErroApi ? erro.campos : {}
}
