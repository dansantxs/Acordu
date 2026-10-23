import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { apiPortal } from '@/shared/api/cliente'
import type { components } from '@/shared/api/portal'

export type PortalEu = components['schemas']['PortalEuDto']
export type StatusExterno = components['schemas']['StatusExterno']
export type SolicitacaoPortal = components['schemas']['SolicitacaoPortalDto']
export type SolicitacaoPortalResumo = components['schemas']['SolicitacaoPortalResumoDto']

export const STATUS_EXTERNO: Record<StatusExterno, string> = {
    RECEBIDA: 'Recebida',
    EM_ANDAMENTO: 'Em andamento',
    AGUARDANDO_VOCE: 'Esperando você',
    CONCLUIDA: 'Concluída',
    CANCELADA: 'Cancelada',
}

export const ESTILO_STATUS_EXTERNO: Record<StatusExterno, string> = {
    RECEBIDA: 'bg-trilho text-tinta',
    EM_ANDAMENTO: 'bg-acento-suave text-acento-texto',
    AGUARDANDO_VOCE: 'bg-ambar-suave text-ambar-texto',
    CONCLUIDA: 'bg-verde-suave text-verde',
    CANCELADA: 'bg-trilho text-suave',
}

/** Grupos da lista do portal (decisão 3 do F7), pelo status externo. */
export const GRUPOS = {
    esperando: { rotulo: 'Esperando você', status: ['AGUARDANDO_VOCE'] },
    andamento: { rotulo: 'Em andamento', status: ['RECEBIDA', 'EM_ANDAMENTO'] },
    concluidas: { rotulo: 'Concluídas', status: ['CONCLUIDA', 'CANCELADA'] },
} satisfies Record<string, { rotulo: string; status: StatusExterno[] }>

export type Grupo = keyof typeof GRUPOS

/** `?grupo=` (padrão: em andamento) e `?pagina=`; `grupo`, e não `aba`, que já é das tarefas (convenções §4). */
export interface FiltrosPortal {
    grupo?: Grupo
    pagina?: number
}

export const TAMANHO_PAGINA = 20

/** Quem é o contato e de qual prestador (`GET /portal/eu`): nome, logotipo, escopo e se vê contratos. */
export const consultaEu = queryOptions({
    queryKey: ['portal', 'eu'],
    queryFn: async () => (await apiPortal.GET('/api/v1/portal/eu')).data!,
    staleTime: 5 * 60_000,
})

export function consultaSolicitacoesPortal(grupo: Grupo, pagina = 1) {
    return queryOptions({
        queryKey: ['portal', 'solicitacoes', grupo, pagina],
        queryFn: async () =>
            (
                await apiPortal.GET('/api/v1/portal/solicitacoes', {
                    params: {
                        query: { statusExterno: GRUPOS[grupo].status.join(','), pagina, tamanhoPagina: TAMANHO_PAGINA },
                    },
                })
            ).data!,
        placeholderData: keepPreviousData,
    })
}

export function consultaSolicitacaoPortal(id: string) {
    return queryOptions({
        queryKey: ['portal', 'solicitacao', id],
        queryFn: async () =>
            (await apiPortal.GET('/api/v1/portal/solicitacoes/{id}', { params: { path: { id } } })).data!,
    })
}

export type InteracaoPortal = components['schemas']['InteracaoPortalDto']

export function consultaInteracoesPortal(id: string) {
    return queryOptions({
        queryKey: ['portal', 'interacoes', id],
        queryFn: async () =>
            (await apiPortal.GET('/api/v1/portal/solicitacoes/{id}/interacoes', { params: { path: { id } } })).data!,
    })
}

/** Resposta do contato (sempre externa): JSON sem anexos, multipart com (contrato §12). */
export async function responderNoPortal(id: string, texto: string | null, anexos: File[]): Promise<InteracaoPortal> {
    if (anexos.length === 0)
        return (
            await apiPortal.POST('/api/v1/portal/solicitacoes/{id}/interacoes', {
                params: { path: { id } },
                body: { texto },
            })
        ).data!
    const formulario = new FormData()
    if (texto) formulario.append('texto', texto)
    anexos.forEach((a) => formulario.append('anexos', a, a.name))
    return (
        await apiPortal.POST('/api/v1/portal/solicitacoes/{id}/interacoes', {
            params: { path: { id } },
            // O openapi-fetch manda FormData como está e deixa o navegador pôr o boundary.
            body: formulario as never,
        })
    ).data!
}

export async function aprovarEntrega(id: string): Promise<SolicitacaoPortal> {
    return (await apiPortal.POST('/api/v1/portal/solicitacoes/{id}/aprovacao', { params: { path: { id } } })).data!
}

export async function pedirAjuste(id: string, motivo: string): Promise<SolicitacaoPortal> {
    return (
        await apiPortal.POST('/api/v1/portal/solicitacoes/{id}/reprovacao', {
            params: { path: { id } },
            body: { motivo },
        })
    ).data!
}

/** Anexo de interação externa: a API devolve a URL assinada e o navegador a abre sem sair da página. */
export async function baixarAnexoPortal(id: string): Promise<void> {
    const { url } = (await apiPortal.GET('/api/v1/portal/anexos/{id}/download', { params: { path: { id } } })).data!
    globalThis.location.assign(url)
}

export type Prioridade = NonNullable<components['schemas']['Prioridade']>

/** Abre pelo portal (contrato §12): JSON sem anexos; com anexos, multipart, e eles viram a primeira mensagem. */
export async function abrirNoPortal(
    dados: { titulo: string; descricao: string | null; prioridade: Prioridade },
    anexos: File[],
): Promise<SolicitacaoPortal> {
    if (anexos.length === 0) return (await apiPortal.POST('/api/v1/portal/solicitacoes', { body: dados })).data!
    const formulario = new FormData()
    formulario.append('titulo', dados.titulo)
    if (dados.descricao) formulario.append('descricao', dados.descricao)
    formulario.append('prioridade', dados.prioridade)
    anexos.forEach((a) => formulario.append('anexos', a, a.name))
    // O openapi-fetch manda FormData como está e deixa o navegador pôr o boundary.
    return (await apiPortal.POST('/api/v1/portal/solicitacoes', { body: formulario as never })).data!
}
