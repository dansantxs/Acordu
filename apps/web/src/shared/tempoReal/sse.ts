/**
 * Leitor de `text/event-stream` sobre bytes (SSE lido com `fetch`, ADR 0009). Só o que a API manda: `event`, `data`,
 * `retry` e comentários (`: vivo`), que são ignorados.
 */
export interface MensagemSse {
    evento: string
    dados: string
}

export class LeitorSse {
    private resto = ''
    private evento = ''
    private dados: string[] = []
    retry: number | null = null

    /** Recebe um pedaço de texto e devolve as mensagens completas que ele fechou. */
    alimentar(pedaco: string): MensagemSse[] {
        const saida: MensagemSse[] = []
        const linhas = (this.resto + pedaco).split(/\r\n|\r|\n/)
        this.resto = linhas.pop() ?? ''
        for (const linha of linhas) {
            if (linha === '') {
                if (this.dados.length > 0)
                    saida.push({ evento: this.evento || 'message', dados: this.dados.join('\n') })
                this.evento = ''
                this.dados = []
                continue
            }
            if (linha.startsWith(':')) continue
            const doisPontos = linha.indexOf(':')
            const campo = doisPontos === -1 ? linha : linha.slice(0, doisPontos)
            const valor = doisPontos === -1 ? '' : linha.slice(doisPontos + 1).replace(/^ /, '')
            if (campo === 'event') this.evento = valor
            else if (campo === 'data') this.dados.push(valor)
            else if (campo === 'retry' && /^\d+$/.test(valor)) this.retry = Number(valor)
        }
        return saida
    }
}
