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
