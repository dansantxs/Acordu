import type { components } from '@/shared/api/interno'

export type Status = components['schemas']['StatusSolicitacao']
export type Prioridade = components['schemas']['Prioridade']
export type SituacaoSla = components['schemas']['SituacaoSla']

export const STATUS: Record<Status, string> = {
    ABERTA: 'Aberta',
    EM_ANDAMENTO: 'Em andamento',
    AGUARDANDO_CLIENTE: 'Aguardando cliente',
    AGUARDANDO_TERCEIRO: 'Aguardando terceiro',
    RESOLVIDA: 'Resolvida',
    FECHADA: 'Fechada',
    CANCELADA: 'Cancelada',
}

export const PRIORIDADE: Record<Prioridade, string> = {
    BAIXA: 'Baixa',
    MEDIA: 'Média',
    ALTA: 'Alta',
    URGENTE: 'Urgente',
}

/** Cores do status como selo (o lacre/mostarda/musgo da direção visual; neutro para os finais). */
export const ESTILO_STATUS: Record<Status, string> = {
    ABERTA: 'bg-acento-suave text-acento-texto',
    EM_ANDAMENTO: 'bg-acento-suave text-acento-texto',
    AGUARDANDO_CLIENTE: 'bg-trilho text-suave',
    AGUARDANDO_TERCEIRO: 'bg-trilho text-suave',
    RESOLVIDA: 'bg-verde-suave text-verde',
    FECHADA: 'bg-trilho text-suave',
    CANCELADA: 'bg-trilho text-suave line-through',
}

export const ESTILO_PRIORIDADE: Record<Prioridade, string> = {
    BAIXA: 'text-suave',
    MEDIA: 'text-tinta',
    ALTA: 'text-ambar-texto font-semibold',
    URGENTE: 'text-vermelho font-semibold',
}

/** Em aberto: o que ainda pede trabalho (visão padrão da fila). */
export const EM_ABERTO: Status[] = ['ABERTA', 'EM_ANDAMENTO', 'AGUARDANDO_CLIENTE', 'AGUARDANDO_TERCEIRO']

export const SITUACAO_SLA: Record<SituacaoSla, { rotulo: string; estilo: string } | null> = {
    SEM_SLA: null,
    NO_PRAZO: { rotulo: 'No prazo', estilo: 'bg-verde' },
    EM_RISCO: { rotulo: 'Em risco', estilo: 'bg-ambar' },
    VIOLADO: { rotulo: 'Violado', estilo: 'bg-vermelho' },
    CUMPRIDO: { rotulo: 'Cumprido', estilo: 'border-2 border-verde' },
}
