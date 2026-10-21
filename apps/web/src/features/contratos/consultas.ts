import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, seVersao } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'
import type { Prioridade } from '@/features/solicitacoes/rotulos'

export type Contrato = components['schemas']['ContratoDto']
export type ContratoResumo = components['schemas']['ContratoResumoDto']
export type StatusContrato = components['schemas']['StatusContrato']
export type PrazoSla = components['schemas']['PrazoSlaDto']
export type NovoContrato = components['schemas']['NovoContratoRequest']
export type EdicaoContrato = components['schemas']['EdicaoContratoRequest']
export type Consumo = components['schemas']['ConsumoContratoDto']

export const STATUS_CONTRATO: Record<StatusContrato, string> = {
    ATIVO: 'Ativo',
    SUSPENSO: 'Suspenso',
    ENCERRADO: 'Encerrado',
}

export const ESTILO_STATUS_CONTRATO: Record<StatusContrato, string> = {
    ATIVO: 'bg-verde-suave text-verde',
    SUSPENSO: 'bg-ambar-suave text-ambar-texto',
    ENCERRADO: 'bg-trilho text-suave',
}

/** SLA sugerido ao criar (plano do F4, decisão 3), em minutos de horas úteis; a pessoa ajusta. */
export const SLA_PADRAO: Record<Prioridade, { primeiraResposta: number; resolucao: number }> = {
    URGENTE: { primeiraResposta: 60, resolucao: 240 },
    ALTA: { primeiraResposta: 120, resolucao: 960 },
    MEDIA: { primeiraResposta: 240, resolucao: 1920 },
    BAIXA: { primeiraResposta: 480, resolucao: 4800 },
}

export const ABAS_CONTRATO = {
    ativos: { rotulo: 'Ativos', status: 'ATIVO' },
    suspensos: { rotulo: 'Suspensos', status: 'SUSPENSO' },
    encerrados: { rotulo: 'Encerrados', status: 'ENCERRADO' },
    todos: { rotulo: 'Todos', status: undefined },
} satisfies Record<string, { rotulo: string; status: StatusContrato | undefined }>

export type AbaContrato = keyof typeof ABAS_CONTRATO

/** Filtros no endereço (`/contratos?estado=&cliente=`); `novo` abre a janela de criar. */
export interface FiltrosContratos {
    estado?: AbaContrato
    cliente?: string
    pagina?: number
    novo?: boolean
}

export const TAMANHO_PAGINA = 50

export function consultaContratos({ novo, ...filtros }: FiltrosContratos) {
    return queryOptions({
        queryKey: ['contratos', 'lista', filtros],
        queryFn: async () =>
            (
                await api.GET('/api/v1/contratos', {
                    params: {
                        query: {
                            clienteId: filtros.cliente,
                            status: ABAS_CONTRATO[filtros.estado ?? 'ativos'].status,
                            pagina: filtros.pagina ?? 1,
                            tamanhoPagina: TAMANHO_PAGINA,
                        },
                    },
                })
            ).data!,
        placeholderData: keepPreviousData,
    })
}

export function consultaContrato(id: string) {
    return queryOptions({
        queryKey: ['contratos', 'detalhe', id],
        queryFn: async () => (await api.GET('/api/v1/contratos/{id}', { params: { path: { id } } })).data!,
    })
}

/** Consumo de um mês (`YYYY-MM`); no banco de horas, o saldo é o acumulado até ele (read side §3.2). */
export function consultaConsumo(id: string, periodo: string) {
    return queryOptions({
        queryKey: ['contratos', 'consumo', id, periodo],
        queryFn: async () =>
            (await api.GET('/api/v1/contratos/{id}/consumo', { params: { path: { id }, query: { periodo } } })).data!,
        placeholderData: keepPreviousData,
    })
}

export async function criarContrato(dados: NovoContrato): Promise<Contrato> {
    return (await api.POST('/api/v1/contratos', { body: dados })).data!
}

export async function editarContrato(c: Contrato, dados: EdicaoContrato): Promise<Contrato> {
    return (
        await api.PATCH('/api/v1/contratos/{id}', {
            params: { path: { id: c.id } },
            body: dados,
            ...seVersao(c.versao),
        })
    ).data!
}

export type AcaoContrato = 'suspensao' | 'reativacao' | 'encerramento'

export async function transicionarContrato(c: Contrato, acao: AcaoContrato, data?: string): Promise<Contrato> {
    const opcoes = { params: { path: { id: c.id } }, ...seVersao(c.versao) }
    if (acao === 'suspensao') return (await api.POST('/api/v1/contratos/{id}/suspensao', opcoes)).data!
    if (acao === 'reativacao') return (await api.POST('/api/v1/contratos/{id}/reativacao', opcoes)).data!
    // Corpo opcional lido à mão pela API (fora do OpenAPI): sem data, encerra hoje.
    return (
        await api.POST('/api/v1/contratos/{id}/encerramento', {
            ...opcoes,
            body: (data ? { data } : undefined) as never,
        })
    ).data!
}

export async function ajustarSaldo(id: string, quantidadeMinutos: number, motivo: string, data: string | null) {
    await api.POST('/api/v1/contratos/{id}/ajustes', {
        params: { path: { id } },
        body: { quantidadeMinutos, motivo, data },
    })
}

export async function fecharPeriodo(id: string, periodo: string) {
    await api.POST('/api/v1/contratos/{id}/periodos/{periodo}/fechamento', { params: { path: { id, periodo } } })
}
