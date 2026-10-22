import { queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'

export type Preferencias = components['schemas']['PreferenciasDto']
export type CategoriaAviso = components['schemas']['CategoriaAviso']
export type AssinaturaAgenda = components['schemas']['AssinaturaAgendaDto']

export const consultaPreferencias = queryOptions({
    queryKey: ['conta', 'preferencias'],
    queryFn: async () => (await api.GET('/api/v1/notificacoes/preferencias')).data!,
})

export async function salvarPreferencias(dados: Preferencias): Promise<Preferencias> {
    return (await api.PUT('/api/v1/notificacoes/preferencias', { body: dados })).data!
}

export const consultaAssinatura = queryOptions({
    queryKey: ['conta', 'agenda'],
    queryFn: async () => (await api.GET('/api/v1/agenda/assinatura')).data!,
})

/** Gera o link (e invalida o anterior): é a única vez em que a URL com o token aparece (contrato §11d). */
export async function gerarLinkAgenda(discreta: boolean) {
    return (await api.POST('/api/v1/agenda/assinatura', { body: { discreta } })).data!
}

export async function mudarModoAgenda(discreta: boolean) {
    return (await api.PATCH('/api/v1/agenda/assinatura', { body: { discreta } })).data!
}

export async function revogarLinkAgenda(): Promise<void> {
    await api.DELETE('/api/v1/agenda/assinatura')
}
