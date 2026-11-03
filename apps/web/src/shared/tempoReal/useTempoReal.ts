import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { MODO_TEMPO_REAL, type ModoTempoReal } from '../config/ambiente'
import { conectarTempoReal } from './conexao'

/**
 * Liga o tempo real enquanto a casca da equipe está na tela. Trocar de empresa remonta a casca (a guarda recarrega
 * o perfil), e a conexão nova já sai com o token da empresa nova. No modo `polling` (nível 0 do ADR 0010) não abre
 * conexão: a Agenda e o Quadro recarregam sozinhos (`intervaloDePolling`).
 */
export function useTempoReal(tenantId: string, modo: ModoTempoReal = MODO_TEMPO_REAL): void {
    const consultas = useQueryClient()
    useEffect(() => (modo === 'sse' ? conectarTempoReal({ consultas }) : undefined), [consultas, tenantId, modo])
}
