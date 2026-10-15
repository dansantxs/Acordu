import { queryOptions } from '@tanstack/react-query'
import { api } from '../api/cliente'
import type { components } from '../api/interno'

export type Perfil = components['schemas']['PerfilUsuario']

/** `GET /auth/eu`: usuário, empresa ativa, papel, permissões e as empresas em que ele tem vínculo. */
export const consultaPerfil = queryOptions({
    queryKey: ['sessao', 'eu'],
    queryFn: async (): Promise<Perfil> => {
        const { data } = await api.GET('/api/v1/auth/eu')
        return data!
    },
    staleTime: 5 * 60_000,
})

/** Contato de cliente usa o portal, não o app da equipe. */
export function ehContato(perfil: Perfil): boolean {
    return perfil.clienteId !== null
}
