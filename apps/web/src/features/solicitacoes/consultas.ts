import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, seVersao } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'
import { EM_ABERTO, type Prioridade, type Status } from './rotulos'

export type SolicitacaoResumo = components['schemas']['SolicitacaoResumoDto']

/** Visões da fila: atalhos para o filtro de status. */
export const VISOES = {
    abertas: { rotulo: 'Em aberto', status: EM_ABERTO },
    'aguardando-cliente': { rotulo: 'Aguardando cliente', status: ['AGUARDANDO_CLIENTE'] },
    resolvidas: { rotulo: 'Resolvidas', status: ['RESOLVIDA', 'FECHADA'] },
    canceladas: { rotulo: 'Canceladas', status: ['CANCELADA'] },
    todas: { rotulo: 'Todas', status: [] },
} satisfies Record<string, { rotulo: string; status: Status[] }>

export type Visao = keyof typeof VISOES

export const ORDENS = {
    '-abertaEm': 'Mais recentes',
    abertaEm: 'Mais antigas',
    prazoResolucao: 'Prazo mais próximo',
    '-prioridade': 'Mais urgentes',
    '-numero': 'Número',
} as const

export type Ordem = keyof typeof ORDENS

/** Filtros no endereço (`?visao=&cliente=&responsavel=&...`); valores padrão omitidos. */
export interface FiltrosFila {
    visao?: Visao
    busca?: string
    cliente?: string
    /** Id da pessoa, `eu` ou `ninguem` (sem responsável). */
    responsavel?: string
    prioridade?: Prioridade
    categoria?: string
    sla?: 'EM_RISCO' | 'VIOLADO'
    ordem?: Ordem
    pagina?: number
    /** Abre a janela de nova solicitação (Ctrl+K, botão); não filtra. */
    nova?: boolean
}

export const TAMANHO_PAGINA = 50

export function consultaFila({ nova, ...filtros }: FiltrosFila, usuarioId: string) {
    const status = VISOES[filtros.visao ?? 'abertas'].status
    return queryOptions({
        queryKey: ['solicitacoes', 'lista', filtros, filtros.responsavel === 'eu' ? usuarioId : null],
        queryFn: async () =>
            (
                await api.GET('/api/v1/solicitacoes', {
                    params: {
                        query: {
                            status: status.length ? status.join(',') : undefined,
                            busca: filtros.busca,
                            clienteId: filtros.cliente,
                            responsavelId:
                                filtros.responsavel === 'eu'
                                    ? usuarioId
                                    : filtros.responsavel === 'ninguem'
                                      ? undefined
                                      : filtros.responsavel,
                            semResponsavel: filtros.responsavel === 'ninguem' || undefined,
                            prioridade: filtros.prioridade,
                            categoriaId: filtros.categoria,
                            situacaoSla: filtros.sla,
                            ordem: filtros.ordem,
                            pagina: filtros.pagina ?? 1,
                            tamanhoPagina: TAMANHO_PAGINA,
                        },
                    },
                })
            ).data!,
        placeholderData: keepPreviousData,
    })
}

export const consultaCategorias = queryOptions({
    queryKey: ['categorias'],
    queryFn: async () => (await api.GET('/api/v1/categorias')).data!,
    staleTime: 5 * 60_000,
})

export type NovaSolicitacao = components['schemas']['NovaSolicitacaoRequest']
export type Solicitacao = components['schemas']['SolicitacaoDto']
export type ContratoResumo = components['schemas']['ContratoResumoDto']

export async function abrirSolicitacao(dados: NovaSolicitacao): Promise<Solicitacao> {
    return (await api.POST('/api/v1/solicitacoes', { body: dados })).data!
}

/** Contratos ativos do cliente, para escolher na abertura (sem escolher, a API usa o único ativo, se houver). */
export function consultaContratosAtivos(clienteId: string) {
    return queryOptions({
        queryKey: ['contratos', 'ativos', clienteId],
        queryFn: async () =>
            (
                await api.GET('/api/v1/contratos', {
                    params: { query: { clienteId, status: 'ATIVO', tamanhoPagina: 50 } },
                })
            ).data!.itens,
    })
}

/** Detalhe (`GET /solicitacoes/{id}`); a chave começa pelo recurso, para o sinal de tempo real invalidar. */
export function consultaSolicitacao(id: string) {
    return queryOptions({
        queryKey: ['solicitacoes', 'detalhe', id],
        queryFn: async () => (await api.GET('/api/v1/solicitacoes/{id}', { params: { path: { id } } })).data!,
    })
}

export type EdicaoSolicitacao = components['schemas']['EdicaoSolicitacaoRequest']

/** O `PATCH` recebe o formulário inteiro (contrato §2): parte do atual e troca só o que mudou. */
export function formularioDe(s: Solicitacao): EdicaoSolicitacao {
    return {
        titulo: s.titulo,
        descricao: s.descricao,
        prioridade: s.prioridade,
        categoriaId: s.categoriaId,
        responsavelId: s.responsavel?.id ?? null,
    }
}

export async function editarSolicitacao(s: Solicitacao, mudanca: Partial<EdicaoSolicitacao>): Promise<Solicitacao> {
    return (
        await api.PATCH('/api/v1/solicitacoes/{id}', {
            params: { path: { id: s.id } },
            body: { ...formularioDe(s), ...mudanca },
            ...seVersao(s.versao),
        })
    ).data!
}

export type Solucao = components['schemas']['SolucaoDto']

/** Transições simples (sem corpo) e as que pedem solução ou motivo. */
export type Transicao =
    | { acao: 'inicio' | 'aguardando-cliente' | 'aguardando-terceiro' | 'retomada' }
    | { acao: 'resolucao'; solucao: Solucao }
    | { acao: 'reabertura' | 'cancelamento'; motivo: string }

export async function transicionar(s: Solicitacao, t: Transicao): Promise<Solicitacao> {
    const opcoes = { params: { path: { id: s.id } }, ...seVersao(s.versao) }
    switch (t.acao) {
        case 'inicio':
            return (await api.POST('/api/v1/solicitacoes/{id}/inicio', opcoes)).data!
        case 'aguardando-cliente':
            return (await api.POST('/api/v1/solicitacoes/{id}/aguardando-cliente', opcoes)).data!
        case 'aguardando-terceiro':
            return (await api.POST('/api/v1/solicitacoes/{id}/aguardando-terceiro', opcoes)).data!
        case 'retomada':
            return (await api.POST('/api/v1/solicitacoes/{id}/retomada', opcoes)).data!
        case 'resolucao':
            return (await api.POST('/api/v1/solicitacoes/{id}/resolucao', { ...opcoes, body: { solucao: t.solucao } }))
                .data!
        case 'reabertura':
            return (await api.POST('/api/v1/solicitacoes/{id}/reabertura', { ...opcoes, body: { motivo: t.motivo } }))
                .data!
        case 'cancelamento':
            return (await api.POST('/api/v1/solicitacoes/{id}/cancelamento', { ...opcoes, body: { motivo: t.motivo } }))
                .data!
    }
}

export type Contrato = components['schemas']['ContratoDto']

export function consultaContrato(id: string) {
    return queryOptions({
        queryKey: ['contratos', 'detalhe', id],
        queryFn: async () => (await api.GET('/api/v1/contratos/{id}', { params: { path: { id } } })).data!,
    })
}

export type Interacao = components['schemas']['InteracaoDto']
export type Visibilidade = components['schemas']['Visibilidade']

/** Linha do tempo (internas e externas), da mais antiga para a mais nova. */
export function consultaInteracoes(id: string) {
    return queryOptions({
        queryKey: ['solicitacoes', 'interacoes', id],
        queryFn: async () =>
            (await api.GET('/api/v1/solicitacoes/{id}/interacoes', { params: { path: { id } } })).data!,
    })
}

/** Sem anexos vai em JSON; com anexos, multipart (`anexos` repetido, contrato §7). */
export async function registrarInteracao(
    id: string,
    dados: { texto: string | null; visibilidade: Visibilidade; anexos: File[] },
): Promise<Interacao> {
    const { texto, visibilidade, anexos } = dados
    if (anexos.length === 0) {
        return (
            await api.POST('/api/v1/solicitacoes/{id}/interacoes', {
                params: { path: { id } },
                body: { texto, visibilidade },
            })
        ).data!
    }
    const formulario = new FormData()
    if (texto) formulario.append('texto', texto)
    formulario.append('visibilidade', visibilidade)
    anexos.forEach((arquivo) => formulario.append('anexos', arquivo, arquivo.name))
    return (
        await api.POST('/api/v1/solicitacoes/{id}/interacoes', {
            params: { path: { id } },
            // O openapi-fetch manda FormData como está e deixa o navegador pôr o boundary.
            body: formulario as unknown as { texto: string | null; visibilidade: Visibilidade },
        })
    ).data!
}

/** Quem segue (RF-COL-03): recebe no sino as novidades da solicitação. */
export function consultaSeguidores(id: string) {
    return queryOptions({
        queryKey: ['solicitacoes', 'seguidores', id],
        queryFn: async () =>
            (await api.GET('/api/v1/solicitacoes/{id}/seguidores', { params: { path: { id } } })).data!,
    })
}

export async function seguir(id: string, seguindo: boolean): Promise<void> {
    const opcoes = { params: { path: { id } } }
    if (seguindo) await api.PUT('/api/v1/solicitacoes/{id}/seguidores/eu', opcoes)
    else await api.DELETE('/api/v1/solicitacoes/{id}/seguidores/eu', opcoes)
}

export type Semelhante = components['schemas']['SolicitacaoSemelhanteDto']

/** Até 5 resolvidas parecidas (RF-CON-02), para reaproveitar a solução. */
export function consultaSemelhantes(id: string) {
    return queryOptions({
        queryKey: ['solicitacoes', 'semelhantes', id],
        queryFn: async () =>
            (await api.GET('/api/v1/solicitacoes/{id}/semelhantes', { params: { path: { id } } })).data!,
        staleTime: 5 * 60_000,
    })
}

export type LinkPortal = components['schemas']['LinkPortalDto']
export type LinkEmitido = components['schemas']['LinkEmitidoDto']

/** Links sem senha válidos desta solicitação (contrato §7, 6d3), sem o token. */
export function consultaLinksPortal(id: string) {
    return queryOptions({
        queryKey: ['solicitacoes', 'links-portal', id],
        queryFn: async () =>
            (await api.GET('/api/v1/solicitacoes/{id}/acessos-portal', { params: { path: { id } } })).data!,
    })
}

export async function emitirLinkPortal(
    id: string,
    dados: components['schemas']['EmissaoLinkRequest'],
): Promise<LinkEmitido> {
    return (await api.POST('/api/v1/solicitacoes/{id}/acessos-portal', { params: { path: { id } }, body: dados })).data!
}

export async function revogarLinkPortal(id: string, acessoId: string): Promise<void> {
    await api.DELETE('/api/v1/solicitacoes/{id}/acessos-portal/{acessoId}', { params: { path: { id, acessoId } } })
}

/** Fora do `PATCH` de propósito (contrato §7): recalcula o SLA com os prazos do contrato novo. */
export async function trocarContrato(s: Solicitacao, contratoId: string | null): Promise<Solicitacao> {
    return (
        await api.PUT('/api/v1/solicitacoes/{id}/contrato', {
            params: { path: { id: s.id } },
            body: { contratoId },
            ...seVersao(s.versao),
        })
    ).data!
}
