import { dataNoFuso, ultimoDiaDoTeste } from '@/shared/sessao/gravacao'
import type { EmpresaResumo, SituacaoEmpresa } from './api'

/**
 * Fuso das datas na área. O DTO da área não traz o fuso de cada empresa (contrato §17), e a operação é da Nebrys, no
 * Brasil: as datas aparecem no horário de Brasília (premissa do F14m). A prorrogação manda só o dia (`ate`), que a API
 * interpreta no fuso da empresa.
 */
export const FUSO_NEBRYS = 'America/Sao_Paulo'

export const SITUACOES: readonly SituacaoEmpresa[] = ['TESTE', 'ATIVA', 'VENCIDA', 'CANCELADA']

export const ROTULO_SITUACAO: Record<SituacaoEmpresa, string> = {
    TESTE: 'Em teste',
    ATIVA: 'Ativa',
    VENCIDA: 'Vencida',
    CANCELADA: 'Cancelada',
}

/** Rótulo do filtro (plural), na ordem das abas. */
export const FILTRO_SITUACAO: Record<SituacaoEmpresa, string> = {
    TESTE: 'Em teste',
    ATIVA: 'Ativas',
    VENCIDA: 'Vencidas',
    CANCELADA: 'Canceladas',
}

export const ESTILO_SITUACAO: Record<SituacaoEmpresa, string> = {
    TESTE: 'bg-ambar-suave text-ambar-texto',
    ATIVA: 'bg-verde-suave text-verde',
    VENCIDA: 'bg-vermelho-suave text-vermelho',
    CANCELADA: 'bg-trilho text-suave',
}

/** Etiqueta da empresa de demonstração (#225), ao lado da situação: ela é ATIVA, mas fora do ciclo. */
export const ROTULO_DEMONSTRACAO = 'Demonstração'
export const ESTILO_DEMONSTRACAO = 'bg-acento-suave text-acento-texto'

export type Acao = 'ativacao' | 'prorrogacao' | 'cancelamento' | 'reativacao'

/** As ações que valem em cada situação (contrato §17, tabela de transições); a API confere de novo (`422`). */
export const ACOES_POR_SITUACAO: Record<SituacaoEmpresa, readonly Acao[]> = {
    TESTE: ['ativacao', 'prorrogacao', 'cancelamento'],
    VENCIDA: ['ativacao', 'prorrogacao', 'cancelamento'],
    ATIVA: ['cancelamento'],
    CANCELADA: ['reativacao'],
}

export const ROTULO_ACAO: Record<Acao, string> = {
    ativacao: 'Ativar',
    prorrogacao: 'Prorrogar o teste',
    cancelamento: 'Cancelar',
    reativacao: 'Reativar',
}

export function data(instante: string): string {
    return dataNoFuso(instante, FUSO_NEBRYS)
}

/**
 * A data que importa em cada situação (F14-61: "fim do teste ou data do vencimento ou cancelamento"): o último dia do
 * teste na TESTE, o vencimento na VENCIDA, o cancelamento na CANCELADA; nada na ATIVA.
 */
export function marcoDaSituacao(
    e: Pick<EmpresaResumo, 'situacao' | 'fimDoTeste' | 'somenteLeituraDesde' | 'canceladaEm'>,
): string | null {
    if (e.situacao === 'TESTE' && e.fimDoTeste) return `Teste até ${ultimoDiaDoTeste(e.fimDoTeste, FUSO_NEBRYS)}`
    if (e.situacao === 'VENCIDA' && e.somenteLeituraDesde) return `Venceu em ${data(e.somenteLeituraDesde)}`
    if (e.situacao === 'CANCELADA' && e.canceladaEm) return `Cancelada em ${data(e.canceladaEm)}`
    return null
}
