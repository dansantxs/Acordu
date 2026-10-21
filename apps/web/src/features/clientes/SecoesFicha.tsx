import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { pode } from '@/features/casca/navegacao'
import { consultaConsumo, ESTILO_STATUS_CONTRATO, STATUS_CONTRATO } from '@/features/contratos/consultas'
import { JanelaCadastroContrato } from '@/features/contratos/JanelaCadastroContrato'
import { Regua } from '@/features/contratos/PaginaContrato'
import { ESTILO_STATUS, STATUS, TIPO_CONTRATO, EM_ABERTO } from '@/features/solicitacoes/rotulos'
import { ESTILO_STATUS_TAREFA, STATUS_TAREFA } from '@/features/tarefas/rotulos'
import { api } from '@/shared/api/cliente'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { dataCurta, hojeNoFuso, mesAtual } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { Icone } from '@/shared/ui/Icone'
import type { Cliente } from './consultas'

function consultaContratosDoCliente(clienteId: string, status?: 'ATIVO') {
    return {
        queryKey: ['contratos', 'do-cliente', clienteId, status ?? 'todos'],
        queryFn: async () =>
            (
                await api.GET('/api/v1/contratos', {
                    params: { query: { clienteId, status, tamanhoPagina: 50 } },
                })
            ).data!.itens,
    }
}

/** Régua de um contrato ativo no resumo: o consumo do mês corrente, com link para a página dele. */
function CartaoContrato({ id, tipo, perfil }: { id: string; tipo: keyof typeof TIPO_CONTRATO; perfil: Perfil }) {
    const consumo = useQuery(consultaConsumo(id, mesAtual(perfil.tenant.fusoHorario)))
    const contrato = useQuery({
        queryKey: ['contratos', 'detalhe', id],
        queryFn: async () => (await api.GET('/api/v1/contratos/{id}', { params: { path: { id } } })).data!,
    })
    return (
        <div className="flex flex-col gap-3 rounded-couro border border-linha bg-superficie p-4">
            <Link to="/contratos/$id" params={{ id }} className="text-sm font-semibold hover:underline">
                {TIPO_CONTRATO[tipo]}
            </Link>
            {consumo.data && contrato.data ? (
                <Regua contrato={contrato.data} consumo={consumo.data} />
            ) : consumo.isError ? (
                <Alerta>{mensagemDeErro(consumo.error)}</Alerta>
            ) : (
                <p className="text-sm text-suave">Carregando…</p>
            )}
        </div>
    )
}

/** Resumo: a régua dos contratos ativos (o "quanto já foi" do protótipo) e o tamanho do que está em aberto. */
export function ResumoCliente({ cliente, perfil }: { cliente: Cliente; perfil: Perfil }) {
    const verContratos = pode(perfil, 'contrato.ver')
    const ativos = useQuery({ ...consultaContratosDoCliente(cliente.id, 'ATIVO'), enabled: verContratos })
    if (!verContratos) return null
    return (
        <section aria-labelledby="contratos-ativos" className="flex flex-col gap-3">
            <h2 id="contratos-ativos" className="font-titulo text-xl font-semibold">
                Contratos ativos
            </h2>
            {ativos.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : ativos.isError ? (
                <Alerta>{mensagemDeErro(ativos.error)}</Alerta>
            ) : ativos.data.length === 0 ? (
                <p className="text-sm text-suave">
                    Nenhum contrato ativo: as horas deste cliente ficam avulsas e as solicitações, sem prazo de SLA.
                </p>
            ) : (
                <div className="grid gap-3 md:grid-cols-2">
                    {ativos.data.map((c) => (
                        <CartaoContrato key={c.id} id={c.id} tipo={c.tipo} perfil={perfil} />
                    ))}
                </div>
            )}
        </section>
    )
}

/** Em aberto: solicitações e tarefas que ainda pedem trabalho, com atalho para abrir uma solicitação nova. */
export function EmAbertoCliente({ cliente, perfil }: { cliente: Cliente; perfil: Perfil }) {
    const navegar = useNavigate()
    const hoje = hojeNoFuso(perfil.tenant.fusoHorario)
    const verSolicitacoes = pode(perfil, 'solicitacao.ver')
    const verTarefas = pode(perfil, 'tarefa.ver')
    const solicitacoes = useQuery({
        queryKey: ['solicitacoes', 'do-cliente', cliente.id],
        queryFn: async () =>
            (
                await api.GET('/api/v1/solicitacoes', {
                    params: { query: { clienteId: cliente.id, status: EM_ABERTO.join(','), tamanhoPagina: 50 } },
                })
            ).data!,
        enabled: verSolicitacoes,
    })
    const tarefas = useQuery({
        queryKey: ['tarefas', 'do-cliente', cliente.id],
        queryFn: async () =>
            (
                await api.GET('/api/v1/tarefas', {
                    params: { query: { clienteId: cliente.id, status: 'A_FAZER,FAZENDO', tamanhoPagina: 50 } },
                })
            ).data!,
        enabled: verTarefas,
    })

    return (
        <div className="grid gap-6 lg:grid-cols-2">
            {verSolicitacoes && (
                <section aria-labelledby="solicitacoes-abertas" className="flex flex-col gap-2">
                    <h2
                        id="solicitacoes-abertas"
                        className="flex items-baseline justify-between font-titulo text-xl font-semibold"
                    >
                        Solicitações
                        {cliente.ativo && pode(perfil, 'solicitacao.criar') && (
                            <Botao
                                variante="discreto"
                                className="!p-0 font-sans text-sm"
                                onPress={() =>
                                    void navegar({ to: '/solicitacoes', search: { nova: true, cliente: cliente.id } })
                                }
                            >
                                <Icone nome="mais" />
                                Nova solicitação
                            </Botao>
                        )}
                    </h2>
                    {solicitacoes.isPending ? (
                        <p className="text-sm text-suave">Carregando…</p>
                    ) : solicitacoes.isError ? (
                        <Alerta>{mensagemDeErro(solicitacoes.error)}</Alerta>
                    ) : solicitacoes.data.itens.length === 0 ? (
                        <p className="text-sm text-suave">Nenhuma solicitação em aberto.</p>
                    ) : (
                        <ul className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                            {solicitacoes.data.itens.map((s) => (
                                <li key={s.id} className="flex items-center justify-between gap-3 px-3 py-2">
                                    <Link to="/solicitacoes/$id" params={{ id: s.id }} className="hover:underline">
                                        <span className="text-suave tabular-nums">#{s.numero}</span> {s.titulo}
                                    </Link>
                                    <span
                                        className={`rounded-couro px-2 py-0.5 text-xs whitespace-nowrap ${ESTILO_STATUS[s.status]}`}
                                    >
                                        {STATUS[s.status]}
                                    </span>
                                </li>
                            ))}
                        </ul>
                    )}
                    {Number(solicitacoes.data?.total ?? 0) > 50 && (
                        <Link
                            to="/solicitacoes"
                            search={{ cliente: cliente.id }}
                            className="text-sm text-acento-texto hover:underline"
                        >
                            Ver todas na fila
                        </Link>
                    )}
                </section>
            )}
            {verTarefas && (
                <section aria-labelledby="tarefas-abertas" className="flex flex-col gap-2">
                    <h2 id="tarefas-abertas" className="font-titulo text-xl font-semibold">
                        Tarefas
                    </h2>
                    {tarefas.isPending ? (
                        <p className="text-sm text-suave">Carregando…</p>
                    ) : tarefas.isError ? (
                        <Alerta>{mensagemDeErro(tarefas.error)}</Alerta>
                    ) : tarefas.data.itens.length === 0 ? (
                        <p className="text-sm text-suave">Nenhuma tarefa em aberto.</p>
                    ) : (
                        <ul className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                            {tarefas.data.itens.map((t) => {
                                const atrasada = t.prazo !== null && t.prazo < hoje
                                return (
                                    <li key={t.id} className="flex items-center justify-between gap-3 px-3 py-2">
                                        <Link to="/tarefas/$id" params={{ id: t.id }} className="hover:underline">
                                            {t.titulo}
                                        </Link>
                                        <span className="flex items-center gap-2 text-xs text-suave">
                                            {t.prazo && (
                                                <span className={atrasada ? 'text-vermelho' : ''}>
                                                    {dataCurta(t.prazo, hoje)}
                                                </span>
                                            )}
                                            <span
                                                className={`rounded-couro px-2 py-0.5 ${ESTILO_STATUS_TAREFA[t.status]}`}
                                            >
                                                {STATUS_TAREFA[t.status]}
                                            </span>
                                        </span>
                                    </li>
                                )
                            })}
                        </ul>
                    )}
                </section>
            )}
        </div>
    )
}

/** Contratos do cliente, de todas as situações, com "Novo contrato" já com o cliente escolhido. */
export function ContratosCliente({ cliente, perfil }: { cliente: Cliente; perfil: Perfil }) {
    const navegar = useNavigate()
    const [novo, setNovo] = useState(false)
    const contratos = useQuery(consultaContratosDoCliente(cliente.id))
    const hoje = hojeNoFuso(perfil.tenant.fusoHorario)
    return (
        <section aria-labelledby="contratos-cliente" className="flex flex-col gap-3">
            <div className="flex items-center justify-between gap-3">
                <h2 id="contratos-cliente" className="font-titulo text-xl font-semibold">
                    Contratos
                </h2>
                {cliente.ativo && pode(perfil, 'contrato.gerenciar') && (
                    <Botao variante="secundario" onPress={() => setNovo(true)}>
                        <Icone nome="mais" />
                        Novo contrato
                    </Botao>
                )}
            </div>
            {contratos.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : contratos.isError ? (
                <Alerta>{mensagemDeErro(contratos.error)}</Alerta>
            ) : contratos.data.length === 0 ? (
                <p className="text-sm text-suave">Nenhum contrato ainda.</p>
            ) : (
                <ul className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                    {contratos.data.map((c) => (
                        <li key={c.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                            <Link to="/contratos/$id" params={{ id: c.id }} className="font-semibold hover:underline">
                                {TIPO_CONTRATO[c.tipo]}
                            </Link>
                            <span className="text-suave">
                                {dataCurta(c.inicio, hoje)} a {c.fim ? dataCurta(c.fim, hoje) : 'sem fim'}
                                {c.franquiaMinutos != null && ` · ${formatarDuracao(Number(c.franquiaMinutos))}`}
                            </span>
                            <span className={`rounded-couro px-2 py-0.5 text-xs ${ESTILO_STATUS_CONTRATO[c.status]}`}>
                                {STATUS_CONTRATO[c.status]}
                            </span>
                        </li>
                    ))}
                </ul>
            )}
            <JanelaCadastroContrato
                fuso={perfil.tenant.fusoHorario}
                aberta={novo}
                aoMudar={setNovo}
                clienteInicial={cliente.id}
                aoSalvar={(c) => void navegar({ to: '/contratos/$id', params: { id: c.id } })}
            />
        </section>
    )
}
