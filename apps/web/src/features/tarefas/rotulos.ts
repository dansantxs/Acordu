import type { components } from '@/shared/api/interno'

export type StatusTarefa = components['schemas']['StatusTarefa']

export const STATUS_TAREFA: Record<StatusTarefa, string> = {
    A_FAZER: 'A fazer',
    FAZENDO: 'Fazendo',
    CONCLUIDA: 'Concluída',
    CANCELADA: 'Cancelada',
}

export const ESTILO_STATUS_TAREFA: Record<StatusTarefa, string> = {
    A_FAZER: 'bg-trilho text-suave',
    FAZENDO: 'bg-acento-suave text-acento-texto',
    CONCLUIDA: 'bg-verde-suave text-verde',
    CANCELADA: 'bg-trilho text-suave line-through',
}

export type AcaoStatusTarefa = 'inicio' | 'retorno' | 'conclusao' | 'reabertura' | 'cancelamento'

/** Máquina de estados da tarefa (modelo §4.6, com o retorno de 21/10). A primeira é a principal; cancelar fica à parte. */
export const TRANSICOES_TAREFA: Record<StatusTarefa, AcaoStatusTarefa[]> = {
    A_FAZER: ['inicio', 'cancelamento'],
    FAZENDO: ['conclusao', 'retorno', 'cancelamento'],
    CONCLUIDA: ['reabertura'],
    CANCELADA: [],
}

export const ROTULO_ACAO_TAREFA: Record<AcaoStatusTarefa, string> = {
    inicio: 'Iniciar',
    retorno: 'Voltar para a fazer',
    conclusao: 'Concluir',
    reabertura: 'Reabrir',
    cancelamento: 'Cancelar tarefa',
}
