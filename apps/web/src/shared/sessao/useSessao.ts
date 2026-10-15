import { useSyncExternalStore } from 'react'
import { assinar, estadoSessao, type EstadoSessao } from './sessao'

export function useSessao(): EstadoSessao {
    return useSyncExternalStore(assinar, estadoSessao)
}
