import { createContext, useContext, useEffect, useRef } from 'react'
import { ErroApi } from '../api/problema'
import type { Perfil } from './perfil'

/**
 * Situação da empresa no front (F14e; spec do F14, F14-10 a F14-12 e F14-25 a F14-28). A API é quem barra a gravação
 * (ADR 0015); o front só explica e desabilita, por um único {@link podeGravar} da sessão (convenções §4).
 */

export type Tenant = Perfil['tenant']
export type SituacaoEmpresa = Tenant['situacao']

/** VENCIDA e CANCELADA (RF-EMP-02): o mesmo critério da API (`SituacaoEmpresaExtensions.SituacoesSomenteLeitura`). */
export const SITUACOES_SOMENTE_LEITURA: readonly SituacaoEmpresa[] = ['VENCIDA', 'CANCELADA']

/** Rótulo de cada situação para quem usa (Configurações › Empresa). */
export const ROTULO_SITUACAO: Record<SituacaoEmpresa, string> = {
    TESTE: 'Em teste',
    ATIVA: 'Ativa',
    VENCIDA: 'Só leitura (o teste venceu)',
    CANCELADA: 'Só leitura (conta cancelada)',
}

/** A empresa da sessão grava? Falso em VENCIDA e CANCELADA. É a única regra das ações desabilitadas na equipe. */
export function podeGravar(tenant: Pick<Tenant, 'situacao'>): boolean {
    return !SITUACOES_SOMENTE_LEITURA.includes(tenant.situacao)
}

const DIA_MS = 86_400_000

/** Dia civil (`YYYY-MM-DD`) de um instante no fuso do tenant (convenções §4). */
function diaNoFuso(instante: Date, fuso: string): string {
    return new Intl.DateTimeFormat('en-CA', { timeZone: fuso }).format(instante)
}

/** `dd/mm/aaaa` de um instante no fuso do tenant. */
export function dataNoFuso(instante: string | Date, fuso: string): string {
    return new Intl.DateTimeFormat('pt-BR', {
        timeZone: fuso,
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
    }).format(new Date(instante))
}

/**
 * Último dia do teste: o dia do instante logo antes do fim. A prorrogação grava o início do dia seguinte (design §1.2),
 * e assim "até 16/12" vale para quem prorrogou até 16/12.
 */
export function ultimoDiaDoTeste(fimDoTeste: string, fuso: string): string {
    return dataNoFuso(new Date(new Date(fimDoTeste).getTime() - 1), fuso)
}

/**
 * Dias que faltam para o fim do teste, contados em dias civis no fuso do tenant até o último dia (0 = termina hoje),
 * só dentro da janela da faixa: `diasFaixaTeste` dias antes do fim, vindos do `/auth/eu` (`Empresas__DiasFaixaTeste`
 * na API, 7 em produção pela decisão 3 do G2). Fora dela, fora do TESTE ou sem fim informado, `null`: nenhuma faixa
 * (F14-11).
 */
export function diasRestantesDoTeste(
    tenant: Pick<Tenant, 'situacao' | 'fimDoTeste' | 'fusoHorario' | 'diasFaixaTeste'>,
    agora = new Date(),
): number | null {
    if (tenant.situacao !== 'TESTE' || !tenant.fimDoTeste) return null
    const fim = new Date(tenant.fimDoTeste)
    if (fim.getTime() - agora.getTime() > Number(tenant.diasFaixaTeste) * DIA_MS) return null
    const ultimo = diaNoFuso(new Date(fim.getTime() - 1), tenant.fusoHorario)
    const hoje = diaNoFuso(agora, tenant.fusoHorario)
    return Math.max(0, Math.round((Date.parse(ultimo) - Date.parse(hoje)) / DIA_MS))
}

/** Id do texto que explica as ações indisponíveis (`aria-describedby` dos botões que gravam). */
export const ID_MOTIVO_SEM_GRAVACAO = 'motivo-sem-gravacao'

export interface Gravacao {
    podeGravar: boolean
    /** Elemento com a explicação, ligado aos botões indisponíveis. */
    idMotivo?: string
}

/**
 * Dado pela casca da equipe (situação da empresa) e pela do portal (`envioDisponivel`). Sem provedor (telas de acesso,
 * aceite de convite), grava: quem barra é a API.
 */
export const ContextoGravacao = createContext<Gravacao>({ podeGravar: true })

export function useGravacao(): Gravacao {
    return useContext(ContextoGravacao)
}

export interface BloqueioDeGravacao {
    /** O controle grava e a empresa está só leitura: fica indisponível. */
    bloqueado: boolean
    /** `aria-describedby` do controle: o que ele já tinha mais, se bloqueado, a explicação da casca. */
    descritoPor: string | undefined
}

/**
 * A mesma regra do `<Botao grava>` para qualquer controle que grava dado da empresa (#191): menus, campos que salvam
 * ao mudar, botões de texto e o arrastar. `grava` falso (o que é da pessoa) nunca bloqueia.
 */
export function useBloqueioDeGravacao(grava = true, descritoPor?: string): BloqueioDeGravacao {
    const gravacao = useGravacao()
    const bloqueado = grava && !gravacao.podeGravar
    return {
        bloqueado,
        descritoPor: bloqueado ? [descritoPor, gravacao.idMotivo].filter(Boolean).join(' ') || undefined : descritoPor,
    }
}

/** Tipos de erro da recusa por só leitura: o da equipe e o neutro do portal (contrato §13). */
const TIPOS_RECUSA = ['empresa-somente-leitura', 'portal-indisponivel']

export function ehRecusaSomenteLeitura(erro: unknown): boolean {
    return erro instanceof ErroApi && erro.status === 422 && TIPOS_RECUSA.includes(erro.tipo)
}

const ouvintes = new Set<() => void>()

/**
 * O cliente HTTP avisa aqui quando a API recusa por só leitura (F14-27): a situação mudou com a tela aberta, e a casca
 * relê a sessão para a faixa e as ações desabilitadas aparecerem. A mensagem da recusa segue com quem chamou.
 */
export function anunciarRecusaSomenteLeitura(): void {
    for (const ouvinte of ouvintes) ouvinte()
}

/** Chama `acao` a cada recusa por só leitura enquanto o componente está montado. */
export function useAoRecusarGravacao(acao: () => void): void {
    const atual = useRef(acao)
    useEffect(() => {
        atual.current = acao
    })
    useEffect(() => {
        const ouvinte = () => atual.current()
        ouvintes.add(ouvinte)
        return () => {
            ouvintes.delete(ouvinte)
        }
    }, [])
}
