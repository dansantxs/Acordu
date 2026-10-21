import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, seVersao } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'
import type { StatusTarefa } from './rotulos'

export type TarefaResumo = components['schemas']['TarefaResumoDto']

/** Abas da lista (o quadro mostra as colunas). */
export const ABAS = {
    abertas: { rotulo: 'Em aberto', status: ['A_FAZER', 'FAZENDO'] },
    concluidas: { rotulo: 'Concluídas', status: ['CONCLUIDA'] },
    canceladas: { rotulo: 'Canceladas', status: ['CANCELADA'] },
    todas: { rotulo: 'Todas', status: [] },
} satisfies Record<string, { rotulo: string; status: StatusTarefa[] }>

export type Aba = keyof typeof ABAS

/** Filtros no endereço (`/tarefas?modo=quadro&responsavel=eu&...`); valores padrão omitidos. */
export interface FiltrosTarefas {
    modo?: 'quadro'
    aba?: Aba
    busca?: string
    cliente?: string
    /** Id da pessoa, `eu` ou `ninguem`. */
    responsavel?: string
    pagina?: number
    /** Abre a janela de nova tarefa (Ctrl+K, botão); não filtra. */
    nova?: boolean
}

export const TAMANHO_PAGINA = 50

function parametros(filtros: FiltrosTarefas) {
    return {
        busca: filtros.busca,
        clienteId: filtros.cliente,
        minhas: filtros.responsavel === 'eu' || undefined,
        semResponsavel: filtros.responsavel === 'ninguem' || undefined,
        responsavelId:
            filtros.responsavel && filtros.responsavel !== 'eu' && filtros.responsavel !== 'ninguem'
                ? filtros.responsavel
                : undefined,
    }
}

export function consultaListaTarefas({ modo, nova, ...filtros }: FiltrosTarefas) {
    const status = ABAS[filtros.aba ?? 'abertas'].status
    return queryOptions({
        queryKey: ['tarefas', 'lista', filtros],
        queryFn: async () =>
            (
                await api.GET('/api/v1/tarefas', {
                    params: {
                        query: {
                            ...parametros(filtros),
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

/** Dias de concluídas que o quadro mostra (decidido em 21/10). */
export const DIAS_CONCLUIDAS_NO_QUADRO = 14

/** Quadro: abertas e concluídas recentes numa chamada (`concluidaDesde` só recorta as concluídas). */
export function consultaQuadro({ modo, aba, pagina, nova, ...filtros }: FiltrosTarefas, agora = Date.now()) {
    const desde = new Date(agora - DIAS_CONCLUIDAS_NO_QUADRO * 86_400_000)
    // Arredonda para a hora: a chave da consulta não muda a cada renderização.
    desde.setUTCMinutes(0, 0, 0)
    return queryOptions({
        queryKey: ['tarefas', 'quadro', filtros],
        queryFn: async () =>
            (
                await api.GET('/api/v1/tarefas', {
                    params: {
                        query: {
                            ...parametros(filtros),
                            status: 'A_FAZER,FAZENDO,CONCLUIDA',
                            concluidaDesde: desde.toISOString(),
                            tamanhoPagina: 200,
                        },
                    },
                })
            ).data!,
    })
}

export type Coluna = Exclude<StatusTarefa, 'CANCELADA'>

/**
 * Chamadas para levar a tarefa de uma coluna a outra pela máquina de estados (modelo §4.6): de "A fazer" para
 * "Concluída" passa por "Fazendo"; de "Concluída" para "A fazer", reabre e retorna.
 */
export function caminho(de: Coluna, para: Coluna): ('inicio' | 'conclusao' | 'reabertura' | 'retorno')[] {
    const passos: Record<Coluna, Partial<Record<Coluna, ('inicio' | 'conclusao' | 'reabertura' | 'retorno')[]>>> = {
        A_FAZER: { FAZENDO: ['inicio'], CONCLUIDA: ['inicio', 'conclusao'] },
        FAZENDO: { A_FAZER: ['retorno'], CONCLUIDA: ['conclusao'] },
        CONCLUIDA: { FAZENDO: ['reabertura'], A_FAZER: ['reabertura', 'retorno'] },
    }
    return passos[de][para] ?? []
}

export type Tarefa = components['schemas']['TarefaDto']

export async function moverTarefa(id: string, de: Coluna, para: Coluna): Promise<Tarefa | null> {
    let ultima: Tarefa | null = null
    const opcoes = { params: { path: { id } } }
    for (const passo of caminho(de, para)) {
        ultima =
            passo === 'inicio'
                ? (await api.POST('/api/v1/tarefas/{id}/inicio', opcoes)).data!
                : passo === 'conclusao'
                  ? (await api.POST('/api/v1/tarefas/{id}/conclusao', opcoes)).data!
                  : passo === 'reabertura'
                    ? (await api.POST('/api/v1/tarefas/{id}/reabertura', opcoes)).data!
                    : (await api.POST('/api/v1/tarefas/{id}/retorno', opcoes)).data!
    }
    return ultima
}

/** Detalhe (`GET /tarefas/{id}`), com checklist, total apontado e nomes da origem. */
export function consultaTarefa(id: string) {
    return queryOptions({
        queryKey: ['tarefas', 'detalhe', id],
        queryFn: async () => (await api.GET('/api/v1/tarefas/{id}', { params: { path: { id } } })).data!,
    })
}

export type EdicaoTarefa = components['schemas']['EdicaoTarefaRequest']

/** O `PATCH` recebe o formulário inteiro (omitir `planejadaPara` limpa o dia): parte do atual. */
export function formularioDaTarefa(t: Tarefa): EdicaoTarefa {
    return {
        titulo: t.titulo,
        descricao: t.descricao,
        responsavelId: t.responsavel?.id ?? null,
        prazo: t.prazo,
        estimativaMinutos: t.estimativaMinutos,
        visivelCliente: t.visivelCliente,
        planejadaPara: t.planejadaPara,
    }
}

export async function editarTarefa(t: Tarefa, mudanca: Partial<EdicaoTarefa>): Promise<Tarefa> {
    return (
        await api.PATCH('/api/v1/tarefas/{id}', {
            params: { path: { id: t.id } },
            body: { ...formularioDaTarefa(t), ...mudanca },
            ...seVersao(t.versao),
        })
    ).data!
}

export type AcaoTarefa = 'inicio' | 'retorno' | 'conclusao' | 'reabertura' | 'cancelamento'

export async function transicionarTarefa(t: Tarefa, acao: AcaoTarefa): Promise<Tarefa> {
    const opcoes = { params: { path: { id: t.id } }, ...seVersao(t.versao) }
    switch (acao) {
        case 'inicio':
            return (await api.POST('/api/v1/tarefas/{id}/inicio', opcoes)).data!
        case 'retorno':
            return (await api.POST('/api/v1/tarefas/{id}/retorno', opcoes)).data!
        case 'conclusao':
            return (await api.POST('/api/v1/tarefas/{id}/conclusao', opcoes)).data!
        case 'reabertura':
            return (await api.POST('/api/v1/tarefas/{id}/reabertura', opcoes)).data!
        case 'cancelamento':
            return (await api.POST('/api/v1/tarefas/{id}/cancelamento', opcoes)).data!
    }
}

export async function adicionarItem(tarefaId: string, texto: string): Promise<void> {
    await api.POST('/api/v1/tarefas/{id}/itens', { params: { path: { id: tarefaId } }, body: { texto, feito: null } })
}

export async function editarItem(tarefaId: string, itemId: string, texto: string, feito: boolean): Promise<void> {
    await api.PATCH('/api/v1/tarefas/{id}/itens/{itemId}', {
        params: { path: { id: tarefaId, itemId } },
        body: { texto, feito },
    })
}

export async function removerItem(tarefaId: string, itemId: string): Promise<void> {
    await api.DELETE('/api/v1/tarefas/{id}/itens/{itemId}', { params: { path: { id: tarefaId, itemId } } })
}

export type Comentario = components['schemas']['ComentarioTarefaDto']

/** Comentários da tarefa (contrato §8.1a), do mais antigo; só da equipe. */
export function consultaComentarios(id: string) {
    return queryOptions({
        queryKey: ['tarefas', 'comentarios', id],
        queryFn: async () => (await api.GET('/api/v1/tarefas/{id}/comentarios', { params: { path: { id } } })).data!,
    })
}

/** Sem anexos vai em JSON; com anexos, multipart (como as interações da solicitação). */
export async function comentar(id: string, texto: string | null, anexos: File[]): Promise<Comentario> {
    if (anexos.length === 0)
        return (await api.POST('/api/v1/tarefas/{id}/comentarios', { params: { path: { id } }, body: { texto } })).data!
    const formulario = new FormData()
    if (texto) formulario.append('texto', texto)
    anexos.forEach((arquivo) => formulario.append('anexos', arquivo, arquivo.name))
    return (
        await api.POST('/api/v1/tarefas/{id}/comentarios', {
            params: { path: { id } },
            body: formulario as unknown as { texto: string | null },
        })
    ).data!
}

export async function excluirComentario(id: string, comentarioId: string): Promise<void> {
    await api.DELETE('/api/v1/tarefas/{id}/comentarios/{comentarioId}', { params: { path: { id, comentarioId } } })
}

export type NovaTarefa = components['schemas']['NovaTarefaRequest']

export async function criarTarefa(dados: NovaTarefa): Promise<Tarefa> {
    return (await api.POST('/api/v1/tarefas', { body: dados })).data!
}
