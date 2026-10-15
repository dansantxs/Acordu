/** Erro da API no formato RFC 9457 (contrato §13), com os erros de campo em `errors`. */
export interface ProblemDetails {
    type?: string
    title?: string
    status?: number
    detail?: string
    errors?: Record<string, string[]>
}

const BASE_TIPOS = 'https://acordu.app.br/erros/'

export class ErroApi extends Error {
    readonly status: number
    /** Sufixo do `type` (`validacao`, `conflito`, `sem-permissao`…), ou vazio se a resposta não era Problem Details. */
    readonly tipo: string
    readonly campos: Record<string, string[]>

    constructor(status: number, problema: ProblemDetails | null) {
        super(problema?.detail ?? problema?.title ?? `Erro ${status}`)
        this.name = 'ErroApi'
        this.status = status
        this.tipo = problema?.type?.startsWith(BASE_TIPOS) ? problema.type.slice(BASE_TIPOS.length) : ''
        this.campos = problema?.errors ?? {}
    }

    /** Primeira mensagem do campo (camelCase, com ponto para aninhados: `regra.diaReferencia`). */
    doCampo(campo: string): string | undefined {
        return this.campos[campo]?.[0]
    }
}

export async function lerProblema(resposta: Response): Promise<ErroApi> {
    let problema: ProblemDetails | null = null
    const tipo = resposta.headers.get('content-type') ?? ''
    if (tipo.includes('json')) {
        try {
            problema = (await resposta.clone().json()) as ProblemDetails
        } catch {
            problema = null
        }
    }
    return new ErroApi(resposta.status, problema)
}
