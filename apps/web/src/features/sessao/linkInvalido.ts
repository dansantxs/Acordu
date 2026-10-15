import { ErroApi } from '@/shared/api/problema'

/** A API responde 401 para token de link inválido, expirado, revogado ou já usado, sem distinguir. */
export function linkRecusado(erro: unknown): boolean {
    return erro instanceof ErroApi && erro.status === 401
}
