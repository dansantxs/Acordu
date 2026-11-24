import { queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'

export type ModoCadastro = components['schemas']['ModoCadastro']

/**
 * Modo do cadastro (`GET /auth/cadastro`, D11 do F14): a API é a fonte, a tela só segue. Muda só com um deploy, então
 * fica no cache enquanto a página estiver aberta.
 */
export const consultaModoCadastro = queryOptions({
    queryKey: ['cadastro', 'modo'],
    queryFn: async (): Promise<ModoCadastro> => (await api.GET('/api/v1/auth/cadastro')).data!.modo,
    staleTime: Infinity,
    retry: false,
})

/** E-mail do convite de empresa e se ele já tem conta (`POST /auth/registro/convite/consulta`; F14-46, F14-105). */
export const consultaConviteEmpresa = (token: string) =>
    queryOptions({
        queryKey: ['cadastro', 'convite', token],
        queryFn: async () => (await api.POST('/api/v1/auth/registro/convite/consulta', { body: { token } })).data!,
        staleTime: Infinity,
        retry: false,
    })

export type ListaEspera = components['schemas']['ListaEsperaRequest']

/** Entrada na lista de espera (`POST /lista-espera`, contrato §16): `202` sem corpo, também para e-mail repetido. */
export async function entrarNaListaEspera(corpo: ListaEspera): Promise<void> {
    await api.POST('/api/v1/lista-espera', { body: corpo })
}
