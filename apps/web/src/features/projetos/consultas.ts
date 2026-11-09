import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, seVersao } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'

export type ProjetoResumo = components['schemas']['ProjetoResumoDto']
export type Projeto = components['schemas']['ProjetoDto']
export type StatusProjeto = components['schemas']['StatusProjeto']
export type NovoProjeto = components['schemas']['NovoProjetoRequest']
export type EdicaoProjeto = components['schemas']['EdicaoProjetoRequest']

export const STATUS_PROJETO: Record<StatusProjeto, string> = {
    EM_ANDAMENTO: 'Em andamento',
    CONCLUIDO: 'Concluído',
    CANCELADO: 'Cancelado',
}

export const ESTILO_STATUS_PROJETO: Record<StatusProjeto, string> = {
    EM_ANDAMENTO: 'bg-acento-suave text-acento-texto',
    CONCLUIDO: 'bg-verde-suave text-verde',
    CANCELADO: 'bg-trilho text-suave line-through',
}

export const ABAS_PROJETO = {
    andamento: { rotulo: 'Em andamento', status: ['EM_ANDAMENTO'] },
    concluidos: { rotulo: 'Concluídos', status: ['CONCLUIDO'] },
    cancelados: { rotulo: 'Cancelados', status: ['CANCELADO'] },
    todos: { rotulo: 'Todos', status: [] },
} satisfies Record<string, { rotulo: string; status: StatusProjeto[] }>

export type AbaProjeto = keyof typeof ABAS_PROJETO

/** Filtros no endereço (`/projetos?situacao=&cliente=&pagina=`); `novo` abre a janela de criar. */
export interface FiltrosProjetos {
    situacao?: AbaProjeto
    cliente?: string
    pagina?: number
    novo?: boolean
}

export const TAMANHO_PAGINA = 50

export function consultaProjetos({ novo, ...filtros }: FiltrosProjetos) {
    const status = ABAS_PROJETO[filtros.situacao ?? 'andamento'].status
    return queryOptions({
        queryKey: ['projetos', 'lista', filtros],
        queryFn: async () =>
            (
                await api.GET('/api/v1/projetos', {
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

/** Projetos em andamento do cliente (para onde a tarefa pode ir, F11-23); 200 bastam para escolher. */
export function consultaProjetosDoCliente(clienteId: string) {
    return queryOptions({
        queryKey: ['projetos', 'do-cliente', clienteId],
        queryFn: async () =>
            (
                await api.GET('/api/v1/projetos', {
                    params: { query: { clienteId, status: 'EM_ANDAMENTO', pagina: 1, tamanhoPagina: 200 } },
                })
            ).data!.itens,
    })
}

export function consultaProjeto(id: string) {
    return queryOptions({
        queryKey: ['projetos', 'detalhe', id],
        queryFn: async () => (await api.GET('/api/v1/projetos/{id}', { params: { path: { id } } })).data!,
    })
}

export async function criarProjeto(dados: NovoProjeto): Promise<Projeto> {
    return (await api.POST('/api/v1/projetos', { body: dados })).data!
}

export async function editarProjeto(p: Projeto, dados: EdicaoProjeto): Promise<Projeto> {
    return (
        await api.PATCH('/api/v1/projetos/{id}', { params: { path: { id: p.id } }, body: dados, ...seVersao(p.versao) })
    ).data!
}

export async function encerrarProjeto(p: Projeto, acao: 'conclusao' | 'cancelamento'): Promise<Projeto> {
    const opcoes = { params: { path: { id: p.id } }, ...seVersao(p.versao) }
    return acao === 'conclusao'
        ? (await api.POST('/api/v1/projetos/{id}/conclusao', opcoes)).data!
        : (await api.POST('/api/v1/projetos/{id}/cancelamento', opcoes)).data!
}
