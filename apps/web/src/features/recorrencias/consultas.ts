import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, seVersao } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'

export type RecorrenciaResumo = components['schemas']['RecorrenciaResumoDto']
export type Recorrencia = components['schemas']['RecorrenciaDto']
export type Regra = components['schemas']['RegraRecorrenciaDto']
export type Modelo = components['schemas']['ModeloTarefaDto']
export type Periodicidade = components['schemas']['Periodicidade']
export type StatusRecorrencia = components['schemas']['StatusRecorrencia']

export const PERIODICIDADE: Record<Periodicidade, string> = {
    SEMANAL: 'Toda semana',
    MENSAL: 'Todo mês',
    TRIMESTRAL: 'A cada 3 meses',
    ANUAL: 'Todo ano',
}

export const STATUS_RECORRENCIA: Record<StatusRecorrencia, string> = {
    ATIVA: 'Ativa',
    PAUSADA: 'Pausada',
    ENCERRADA: 'Encerrada',
}

export const ESTILO_STATUS_RECORRENCIA: Record<StatusRecorrencia, string> = {
    ATIVA: 'bg-verde-suave text-verde',
    PAUSADA: 'bg-ambar-suave text-ambar-texto',
    ENCERRADA: 'bg-trilho text-suave',
}

export const ABAS_RECORRENCIA = {
    ativas: { rotulo: 'Ativas', status: ['ATIVA'] },
    pausadas: { rotulo: 'Pausadas', status: ['PAUSADA'] },
    encerradas: { rotulo: 'Encerradas', status: ['ENCERRADA'] },
    todas: { rotulo: 'Todas', status: [] },
} satisfies Record<string, { rotulo: string; status: StatusRecorrencia[] }>

export type AbaRecorrencia = keyof typeof ABAS_RECORRENCIA

/**
 * Filtros no endereço (`/recorrencias?status=&cliente=&pagina=`); `nova` abre a janela de criar. O nome `status` não
 * colide com o de outra rota (convenções §4: `aba`, `situacao` e `estado` já têm outros valores).
 */
export interface FiltrosRecorrencias {
    status?: AbaRecorrencia
    cliente?: string
    pagina?: number
    nova?: boolean
}

export const TAMANHO_PAGINA = 50

export function consultaRecorrencias({ nova, ...filtros }: FiltrosRecorrencias) {
    const status = ABAS_RECORRENCIA[filtros.status ?? 'ativas'].status
    return queryOptions({
        queryKey: ['recorrencias', 'lista', filtros],
        queryFn: async () =>
            (
                await api.GET('/api/v1/recorrencias', {
                    params: {
                        query: {
                            clienteId: filtros.cliente,
                            status: status.length ? status.join(',') : undefined,
                            pagina: filtros.pagina ?? 1,
                            tamanhoPagina: TAMANHO_PAGINA,
                        },
                    },
                })
            ).data!,
        placeholderData: keepPreviousData,
    })
}

export function consultaRecorrencia(id: string) {
    return queryOptions({
        queryKey: ['recorrencias', 'detalhe', id],
        queryFn: async () => (await api.GET('/api/v1/recorrencias/{id}', { params: { path: { id } } })).data!,
    })
}

/** Próximas datas de uma recorrência salva (ativa: a partir da próxima; pausada: se retomasse hoje). */
export function consultaPrevia(id: string, versao: string) {
    return queryOptions({
        queryKey: ['recorrencias', 'previa', id, versao],
        queryFn: async () =>
            (
                await api.GET('/api/v1/recorrencias/{id}/previa', {
                    params: { path: { id }, query: { quantidade: 6 } },
                })
            ).data!,
    })
}

/** Prévia de uma regra ainda não salva (`POST /recorrencias/previa`, contrato §10): o mesmo gerador do job. */
export async function previaDaRegra(regra: Regra): Promise<string[]> {
    return (await api.POST('/api/v1/recorrencias/previa', { body: { regra, quantidade: 6 } })).data!
}

export async function criarRecorrencia(dados: components['schemas']['NovaRecorrenciaRequest']): Promise<Recorrencia> {
    return (await api.POST('/api/v1/recorrencias', { body: dados })).data!
}

export async function editarRecorrencia(
    r: Recorrencia,
    dados: components['schemas']['EdicaoRecorrenciaRequest'],
): Promise<Recorrencia> {
    return (
        await api.PATCH('/api/v1/recorrencias/{id}', {
            params: { path: { id: r.id } },
            body: dados,
            ...seVersao(r.versao),
        })
    ).data!
}

export async function pausarOuRetomar(r: Recorrencia, acao: 'pausa' | 'retomada'): Promise<Recorrencia> {
    const opcoes = { params: { path: { id: r.id } }, ...seVersao(r.versao) }
    return acao === 'pausa'
        ? (await api.POST('/api/v1/recorrencias/{id}/pausa', opcoes)).data!
        : (await api.POST('/api/v1/recorrencias/{id}/retomada', opcoes)).data!
}

/** Encerrar é final: a recorrência fica como histórico e não gera mais tarefas. */
export async function encerrarRecorrencia(r: Recorrencia): Promise<void> {
    await api.DELETE('/api/v1/recorrencias/{id}', { params: { path: { id: r.id } }, ...seVersao(r.versao) })
}

const DIAS_SEMANA = ['segunda', 'terça', 'quarta', 'quinta', 'sexta', 'sábado', 'domingo']
export const NOME_DIA_SEMANA = DIAS_SEMANA.map((d, i) => (i < 5 ? `${d}-feira` : d))

const MESES = [
    'janeiro',
    'fevereiro',
    'março',
    'abril',
    'maio',
    'junho',
    'julho',
    'agosto',
    'setembro',
    'outubro',
    'novembro',
    'dezembro',
]

/**
 * A regra em uma frase ("Todo mês, no dia 31 (ou no último dia)"). TRIMESTRAL e ANUAL tiram o mês do início
 * (contrato §10); sem início, o mês de hoje.
 */
export function descreverRegra(regra: Regra, hoje: string): string {
    const dia = Number(regra.diaReferencia)
    const mes = Number((regra.inicio ?? hoje).slice(5, 7)) - 1
    const fimDoMes = dia > 28 ? ' (ou no último dia)' : ''
    switch (regra.periodicidade) {
        case 'SEMANAL':
            return `Toda ${NOME_DIA_SEMANA[dia - 1] ?? 'semana'}`
        case 'MENSAL':
            return `Todo mês, no dia ${dia}${fimDoMes}`
        case 'TRIMESTRAL':
            return `A cada 3 meses, no dia ${dia}${fimDoMes}, a partir de ${MESES[mes]}`
        case 'ANUAL':
            return `Todo ano, em ${dia} de ${MESES[mes]}${fimDoMes}`
    }
}

/** Quando a tarefa aparece em relação à data de referência (o prazo dela). */
export function descreverAntecedencia(dias: number): string {
    if (dias === 0) return 'A tarefa aparece no próprio dia.'
    return `A tarefa aparece ${dias === 1 ? '1 dia' : `${dias} dias`} antes, com prazo na data.`
}
