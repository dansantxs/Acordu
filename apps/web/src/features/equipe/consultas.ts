import { queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'

export type Pessoa = components['schemas']['UsuarioEquipeDto']
export type ConviteEquipe = components['schemas']['ConviteEquipeDto']
export type Papel = components['schemas']['PapelDto']

/** Nome do papel padrão que o convite sugere (segurança §3.2). */
export const NOME_MEMBRO = 'Membro'

/** Equipe (`GET /usuarios`, contrato §3): só com `usuario.convidar` ou `usuario.remover`. */
export const consultaPessoas = queryOptions({
    queryKey: ['equipe', 'pessoas'],
    queryFn: async () => (await api.GET('/api/v1/usuarios')).data!,
})

export const consultaConvites = queryOptions({
    queryKey: ['equipe', 'convites'],
    queryFn: async () => (await api.GET('/api/v1/convites')).data!,
})

/** Papéis, padrões primeiro; com `papel.gerenciar` ou `usuario.convidar`. */
export const consultaPapeis = queryOptions({
    queryKey: ['equipe', 'papeis'],
    queryFn: async () => (await api.GET('/api/v1/papeis')).data!,
})

export const consultaCatalogo = queryOptions({
    queryKey: ['equipe', 'catalogo'],
    queryFn: async () => (await api.GET('/api/v1/papeis/permissoes')).data!,
    staleTime: Infinity,
})

export async function trocarPapel(p: Pessoa, papelId: string): Promise<Pessoa> {
    return (
        await api.PATCH('/api/v1/usuarios/{id}/papel', { params: { path: { id: p.usuarioId } }, body: { papelId } })
    ).data!
}

export async function definirCapacidade(p: Pessoa, minutos: number): Promise<Pessoa> {
    return (
        await api.PUT('/api/v1/usuarios/{id}/capacidade', {
            params: { path: { id: p.usuarioId } },
            body: { capacidadeSemanalMinutos: minutos },
        })
    ).data!
}

export async function removerPessoa(p: Pessoa): Promise<void> {
    await api.DELETE('/api/v1/usuarios/{id}', { params: { path: { id: p.usuarioId } } })
}

export async function convidar(email: string, papelId: string): Promise<ConviteEquipe> {
    return (await api.POST('/api/v1/convites', { body: { email, papelId } })).data!
}

export async function revogarConvite(c: ConviteEquipe): Promise<void> {
    await api.DELETE('/api/v1/convites/{id}', { params: { path: { id: c.id } } })
}

export async function criarPapel(nome: string, permissoes: string[]): Promise<Papel> {
    return (await api.POST('/api/v1/papeis', { body: { nome, permissoes } })).data!
}

export async function editarPapel(p: Papel, nome: string, permissoes: string[]): Promise<Papel> {
    return (await api.PATCH('/api/v1/papeis/{id}', { params: { path: { id: p.id } }, body: { nome, permissoes } }))
        .data!
}

export async function excluirPapel(p: Papel): Promise<void> {
    await api.DELETE('/api/v1/papeis/{id}', { params: { path: { id: p.id } } })
}
