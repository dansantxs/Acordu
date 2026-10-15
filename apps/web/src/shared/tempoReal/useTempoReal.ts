import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { conectarTempoReal } from './conexao'

/**
 * Liga o tempo real enquanto a casca da equipe está na tela. Trocar de empresa remonta a casca (a guarda recarrega
 * o perfil), e a conexão nova já sai com o token da empresa nova.
 */
export function useTempoReal(tenantId: string): void {
    const consultas = useQueryClient()
    useEffect(() => conectarTempoReal({ consultas }), [consultas, tenantId])
}
