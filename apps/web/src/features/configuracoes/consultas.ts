import { queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'

export type Empresa = components['schemas']['ConfiguracaoEmpresaDto']
export type Calendario = components['schemas']['CalendarioDto']
export type DiaDaSemana = components['schemas']['DiaDaSemana']
export type ConfiguracaoPortal = components['schemas']['ConfiguracaoPortalDto']
export type Categoria = components['schemas']['CategoriaDto']

/** Empresa (F5a): nome, "responder para" e o SLA que o cadastro de contrato sugere. A equipe toda lê. */
export const consultaEmpresa = queryOptions({
    queryKey: ['configuracoes', 'empresa'],
    queryFn: async () => (await api.GET('/api/v1/configuracoes/empresa')).data!,
    staleTime: 5 * 60_000,
})

export async function salvarEmpresa(dados: components['schemas']['ConfiguracaoEmpresaRequest']): Promise<Empresa> {
    return (await api.PUT('/api/v1/configuracoes/empresa', { body: dados })).data!
}

/** Em `shared/` porque o lembrete do cronômetro (F12d) também lê; aqui, reexportada para as telas de configuração. */
export { consultaCalendario } from '@/shared/calendario/consultaCalendario'

export async function salvarCalendario(dados: components['schemas']['CalendarioRequest']): Promise<Calendario> {
    return (await api.PUT('/api/v1/configuracoes/calendario', { body: dados })).data!
}

export const consultaPortal = queryOptions({
    queryKey: ['configuracoes', 'portal'],
    queryFn: async () => (await api.GET('/api/v1/configuracoes/portal')).data!,
})

export async function salvarPortal(
    dados: components['schemas']['ConfiguracaoPortalRequest'],
): Promise<ConfiguracaoPortal> {
    return (await api.PUT('/api/v1/configuracoes/portal', { body: dados })).data!
}

/** Logotipo em multipart, campo `logo` (PNG, JPEG ou WebP até 512 KB; contrato §6). */
export async function enviarLogo(arquivo: File): Promise<ConfiguracaoPortal> {
    const formulario = new FormData()
    formulario.append('logo', arquivo, arquivo.name)
    return (
        await api.PUT('/api/v1/configuracoes/portal/logo', {
            // O openapi-fetch manda FormData como está e deixa o navegador pôr o boundary.
            body: formulario as never,
        })
    ).data!
}

export async function removerLogo(): Promise<ConfiguracaoPortal> {
    return (await api.DELETE('/api/v1/configuracoes/portal/logo')).data!
}

export async function criarCategoria(nome: string): Promise<Categoria> {
    return (await api.POST('/api/v1/categorias', { body: { nome, ativa: true } })).data!
}

export async function editarCategoria(c: Categoria, mudanca: { nome?: string; ativa?: boolean }): Promise<Categoria> {
    return (
        await api.PATCH('/api/v1/categorias/{id}', {
            params: { path: { id: c.id } },
            body: { nome: mudanca.nome ?? c.nome, ativa: mudanca.ativa ?? null },
        })
    ).data!
}

export async function inativarCategoria(c: Categoria): Promise<void> {
    await api.DELETE('/api/v1/categorias/{id}', { params: { path: { id: c.id } } })
}
