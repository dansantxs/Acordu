import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, seVersao } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'

export type ClienteResumo = components['schemas']['ClienteResumoDto']
export type Cliente = components['schemas']['ClienteDto']
export type Contato = components['schemas']['ContatoDto']
export type DadosCliente = components['schemas']['ClienteRequest']
export type DadosContato = components['schemas']['ContatoRequest']

export interface FiltrosClientes {
    busca?: string
    inativos?: boolean
    pagina?: number
}

export const TAMANHO_PAGINA = 50

/** Chaves sob `clientes` (convenções §4): o sinal de tempo real `cliente` invalida lista e fichas juntas. */
export function consultaClientes(filtros: FiltrosClientes) {
    return queryOptions({
        queryKey: ['clientes', 'lista', filtros],
        queryFn: async () =>
            (
                await api.GET('/api/v1/clientes', {
                    params: {
                        query: {
                            busca: filtros.busca || undefined,
                            // Sem "mostrar inativos", só os ativos; com ele, todos.
                            ativo: filtros.inativos ? undefined : true,
                            pagina: filtros.pagina ?? 1,
                            tamanhoPagina: TAMANHO_PAGINA,
                        },
                    },
                })
            ).data!,
        placeholderData: keepPreviousData,
    })
}

export function consultaCliente(id: string) {
    return queryOptions({
        queryKey: ['clientes', id],
        queryFn: async () => (await api.GET('/api/v1/clientes/{id}', { params: { path: { id } } })).data!,
    })
}

export async function criarCliente(dados: DadosCliente): Promise<Cliente> {
    return (await api.POST('/api/v1/clientes', { body: dados })).data!
}

export async function editarCliente(cliente: Cliente, dados: DadosCliente): Promise<Cliente> {
    const { data } = await api.PATCH('/api/v1/clientes/{id}', {
        params: { path: { id: cliente.id } },
        body: dados,
        ...seVersao(cliente.versao),
    })
    return data!
}

export async function inativarCliente(cliente: Cliente): Promise<void> {
    await api.DELETE('/api/v1/clientes/{id}', { params: { path: { id: cliente.id } }, ...seVersao(cliente.versao) })
}

export async function adicionarContato(cliente: Cliente, dados: DadosContato): Promise<Contato> {
    return (await api.POST('/api/v1/clientes/{id}/contatos', { params: { path: { id: cliente.id } }, body: dados }))
        .data!
}

export async function editarContato(cliente: Cliente, contato: Contato, dados: DadosContato): Promise<Contato> {
    const { data } = await api.PATCH('/api/v1/clientes/{id}/contatos/{contatoId}', {
        params: { path: { id: cliente.id, contatoId: contato.id } },
        body: dados,
        ...seVersao(cliente.versao),
    })
    return data!
}

export async function inativarContato(cliente: Cliente, contato: Contato): Promise<void> {
    await api.DELETE('/api/v1/clientes/{id}/contatos/{contatoId}', {
        params: { path: { id: cliente.id, contatoId: contato.id } },
        ...seVersao(cliente.versao),
    })
}

/** Texto opcional: vazio vira `null`, como a API guarda. */
export function opcional(valor: FormDataEntryValue | null): string | null {
    const texto = typeof valor === 'string' ? valor.trim() : ''
    return texto === '' ? null : texto
}
