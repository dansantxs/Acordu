import { useQueries } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { Button } from 'react-aria-components'
import { PRIMEIROS_PASSOS, type ChaveProgresso } from '@/features/ajuda/conteudo'
import { pode } from '@/features/casca/navegacao'
import { api } from '@/shared/api/cliente'
import type { Perfil } from '@/shared/sessao/perfil'

/**
 * Primeiros passos no Hoje (marco do manual, M5): para quem configura a empresa (o Dono), o progresso da ordem de uso
 * pelas listas que já existem. Some quando tudo foi feito ou com "Dispensar" (guardado no navegador, por empresa).
 * Cada chave da consulta começa pelo recurso, para o tempo real e as mutações já existentes a invalidarem, e termina
 * em `primeiros-passos`: aqui a consulta guarda só um booleano, e uma chave igual à da tela (ex.: a seção Empresa)
 * entregaria esse booleano a ela.
 */
const algum = (r: { total: number | string }) => Number(r.total) > 0
const pagina1 = { query: { tamanhoPagina: 1 } } as const

const VERIFICACOES: Record<ChaveProgresso, { chave: string[]; feito: () => Promise<boolean> }> = {
    empresa: {
        chave: ['configuracoes', 'empresa', 'primeiros-passos'],
        feito: async () => {
            const e = (await api.GET('/api/v1/configuracoes/empresa')).data!
            return Boolean(e.responderPara) || e.slaPersonalizado
        },
    },
    calendario: {
        chave: ['configuracoes', 'calendario', 'primeiros-passos'],
        feito: async () => (await api.GET('/api/v1/configuracoes/calendario')).data!.feriados.length > 0,
    },
    equipe: {
        chave: ['usuarios', 'primeiros-passos'],
        feito: async () => {
            const [usuarios, convites] = await Promise.all([api.GET('/api/v1/usuarios'), api.GET('/api/v1/convites')])
            return usuarios.data!.length > 1 || convites.data!.length > 0
        },
    },
    clientes: {
        chave: ['clientes', 'primeiros-passos'],
        feito: async () => algum((await api.GET('/api/v1/clientes', { params: pagina1 })).data!),
    },
    contratos: {
        chave: ['contratos', 'primeiros-passos'],
        feito: async () => algum((await api.GET('/api/v1/contratos', { params: pagina1 })).data!),
    },
    portal: {
        chave: ['auditoria', 'primeiros-passos'],
        feito: async () =>
            algum(
                (
                    await api.GET('/api/v1/auditoria', {
                        params: { query: { acao: 'portal.acesso.concessao', tamanhoPagina: 1 } },
                    })
                ).data!,
            ),
    },
    solicitacoes: {
        chave: ['solicitacoes', 'primeiros-passos'],
        feito: async () => algum((await api.GET('/api/v1/solicitacoes', { params: pagina1 })).data!),
    },
    tarefas: {
        chave: ['tarefas', 'primeiros-passos'],
        feito: async () => algum((await api.GET('/api/v1/tarefas', { params: pagina1 })).data!),
    },
    recorrencias: {
        chave: ['recorrencias', 'primeiros-passos'],
        feito: async () => algum((await api.GET('/api/v1/recorrencias', { params: pagina1 })).data!),
    },
}

const chaveDispensa = (tenantId: string) => `acordu.primeiros-passos.${tenantId}`

function dispensado(tenantId: string): boolean {
    try {
        return localStorage.getItem(chaveDispensa(tenantId)) === 'dispensado'
    } catch {
        return false
    }
}

export function PrimeirosPassos({ perfil }: { perfil: Perfil }) {
    const [oculto, setOculto] = useState(() => dispensado(perfil.tenant.id))
    const paraQuem = pode(perfil, 'configuracao.gerenciar')
    const passos = PRIMEIROS_PASSOS.filter((p) => p.progresso && pode(perfil, ...p.permissoes))
    const consultas = useQueries({
        queries: passos.map((p) => ({
            queryKey: VERIFICACOES[p.progresso!].chave,
            queryFn: VERIFICACOES[p.progresso!].feito,
            enabled: paraQuem && !oculto,
        })),
    })
    if (!paraQuem || oculto || passos.length === 0) return null
    // Só decide depois de tudo carregado: o cartão não pisca para quem já terminou.
    if (consultas.some((c) => c.isPending)) return null
    const feitos = consultas.map((c) => c.data === true)
    const total = feitos.filter(Boolean).length
    if (total === passos.length) return null

    const dispensar = () => {
        try {
            localStorage.setItem(chaveDispensa(perfil.tenant.id), 'dispensado')
        } catch {
            // Sem armazenamento: some até recarregar.
        }
        setOculto(true)
    }

    return (
        <section
            aria-labelledby="hoje-primeiros-passos"
            className="flex flex-col gap-3 rounded-couro border border-acento/40 bg-acento-suave/30 p-4"
        >
            <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                <h2 id="hoje-primeiros-passos" className="mr-auto font-titulo text-xl font-semibold tracking-tight">
                    Primeiros passos
                </h2>
                <Link to="/ajuda" className="text-sm text-acento-texto hover:underline">
                    Ver o manual
                </Link>
                <Button onPress={dispensar} className="text-sm text-suave hover:text-tinta">
                    Dispensar
                </Button>
            </div>
            <div className="flex items-center gap-3 text-sm">
                <span
                    role="meter"
                    aria-label="Primeiros passos feitos"
                    aria-valuemin={0}
                    aria-valuemax={passos.length}
                    aria-valuenow={total}
                    className="h-2 flex-1 overflow-hidden rounded-full bg-trilho"
                >
                    <span className="block h-full bg-acento" style={{ width: `${(total / passos.length) * 100}%` }} />
                </span>
                <span className="text-suave tabular-nums">
                    {total} de {passos.length}
                </span>
            </div>
            <ol className="grid gap-x-6 gap-y-1.5 text-sm sm:grid-cols-2">
                {passos.map((p, i) => (
                    <li key={p.titulo} className="flex items-center gap-2">
                        <span
                            aria-hidden="true"
                            className={`grid size-5 shrink-0 place-items-center rounded-full text-[11px] font-semibold ${feitos[i] ? 'bg-verde text-white' : 'border border-linha text-suave'}`}
                        >
                            {feitos[i] ? '✓' : i + 1}
                        </span>
                        {feitos[i] ? (
                            <span className="text-suave line-through">
                                {p.titulo}
                                <span className="sr-only"> (feito)</span>
                            </span>
                        ) : (
                            <Link to={p.para} className="hover:underline">
                                {p.titulo}
                            </Link>
                        )}
                    </li>
                ))}
            </ol>
        </section>
    )
}
