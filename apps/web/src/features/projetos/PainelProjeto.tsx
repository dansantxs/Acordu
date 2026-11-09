import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useCallback, useEffect, useRef, useState } from 'react'
import { pode } from '@/features/casca/navegacao'
import { consultaContrato } from '@/features/contratos/consultas'
import { TIPO_CONTRATO } from '@/features/solicitacoes/rotulos'
import { consultaTarefasDoProjeto, type TarefasAbertas } from '@/features/tarefas/consultas'
import { JanelaNovaTarefa } from '@/features/tarefas/JanelaNovaTarefa'
import { ESTILO_STATUS_TAREFA, STATUS_TAREFA } from '@/features/tarefas/rotulos'
import { useTarefasAbertas } from '@/features/tarefas/TarefasAbertasJunto'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar } from '@/shared/avisos/avisos'
import { dataCurta, hojeNoFuso } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { Dado, PainelLateral } from '@/shared/ui/PainelLateral'
import {
    consultaProjeto,
    encerrarProjeto,
    ESTILO_STATUS_PROJETO,
    STATUS_PROJETO,
    type FiltrosProjetos,
    type Projeto,
} from './consultas'
import { JanelaProjeto } from './JanelaProjeto'

const ID_TITULO = 'titulo-projeto'

/** Detalhe do projeto em painel (F3e): progresso (RF-PRJ-02), dados, tarefas e nova tarefa no projeto. */
export function PainelProjeto({
    perfil,
    id,
    abrirConclusao = false,
}: {
    perfil: Perfil
    id: string
    abrirConclusao?: boolean
}) {
    const navegar = useNavigate()
    const titulo = useRef<HTMLHeadingElement>(null)
    const consulta = useQuery(consultaProjeto(id))
    const fechar = useCallback(() => {
        void navegar({ to: '/projetos', search: (atual: FiltrosProjetos) => atual }).then(() =>
            document.querySelector<HTMLElement>(`[data-projeto="${id}"]`)?.focus(),
        )
    }, [id, navegar])
    const carregado = consulta.isSuccess
    useEffect(() => {
        if (carregado) titulo.current?.focus()
    }, [carregado])

    return (
        <PainelLateral
            idTitulo={ID_TITULO}
            fechar={fechar}
            cabecalho={
                <>
                    <span className="text-sm text-suave">Projeto</span>
                    {consulta.data && (
                        <span
                            className={`rounded-couro px-2 py-0.5 text-xs ${ESTILO_STATUS_PROJETO[consulta.data.status]}`}
                        >
                            {STATUS_PROJETO[consulta.data.status]}
                        </span>
                    )}
                </>
            }
        >
            {consulta.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : consulta.isError ? (
                <Alerta>
                    {consulta.error instanceof ErroApi && consulta.error.status === 404
                        ? 'Não encontramos este projeto.'
                        : mensagemDeErro(consulta.error)}
                </Alerta>
            ) : (
                <Detalhe projeto={consulta.data} perfil={perfil} refTitulo={titulo} abrirConclusao={abrirConclusao} />
            )}
        </PainelLateral>
    )
}

function Barra({ rotulo, valor, maximo, texto }: { rotulo: string; valor: number; maximo: number; texto: string }) {
    const pct = maximo > 0 ? Math.min(Math.round((valor / maximo) * 100), 100) : 0
    const estourou = maximo > 0 && valor > maximo
    return (
        <div className="flex flex-col gap-1">
            <div className="flex items-baseline justify-between text-sm">
                <span className="font-semibold">{rotulo}</span>
                <span className={estourou ? 'text-vermelho' : 'text-suave'}>{texto}</span>
            </div>
            <div
                role="meter"
                aria-label={rotulo}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={pct}
                aria-valuetext={texto}
                className="h-2 overflow-hidden rounded-full bg-trilho"
            >
                <div className={`h-full ${estourou ? 'bg-vermelho' : 'bg-acento'}`} style={{ width: `${pct}%` }} />
            </div>
        </div>
    )
}

function Detalhe({
    projeto: p,
    perfil,
    refTitulo,
    abrirConclusao,
}: {
    projeto: Projeto
    perfil: Perfil
    refTitulo: React.RefObject<HTMLHeadingElement | null>
    abrirConclusao: boolean
}) {
    const consultas = useQueryClient()
    const navegar = useNavigate()
    const hoje = hojeNoFuso(perfil.tenant.fusoHorario)
    const [editando, setEditando] = useState(false)
    const gerencia = pode(perfil, 'projeto.gerenciar')
    const emAndamento = p.status === 'EM_ANDAMENTO'
    const [encerrando, setEncerrando] = useState<'conclusao' | 'cancelamento' | null>(
        abrirConclusao && gerencia && emAndamento ? 'conclusao' : null,
    )
    // O `?concluir=true` já fez seu papel: sai do endereço para recarregar ou voltar não reabrir a janela.
    useEffect(() => {
        if (abrirConclusao)
            void navegar({
                to: '/projetos/$id',
                params: { id: p.id },
                search: (atual: FiltrosProjetos) => ({ ...atual, concluir: undefined }),
                replace: true,
            })
    }, [abrirConclusao, navegar, p.id])
    const [novaTarefa, setNovaTarefa] = useState(false)
    const doProjeto = useQuery(consultaTarefasDoProjeto(p.id))
    const contrato = useQuery({
        ...consultaContrato(p.contratoId ?? ''),
        enabled: p.contratoId !== null && pode(perfil, 'contrato.ver'),
    })
    const abertas = (doProjeto.data ?? []).filter((t) => t.status === 'A_FAZER' || t.status === 'FAZENDO')
    const junto = useTarefasAbertas(
        abertas,
        pode(perfil, 'tarefa.editar'),
        encerrando === 'cancelamento' ? 'CANCELAR' : 'CONCLUIR',
        encerrando !== null,
    )
    const encerramento = useMutation({
        mutationFn: ({ acao, tarefas }: { acao: 'conclusao' | 'cancelamento'; tarefas?: TarefasAbertas }) =>
            encerrarProjeto(p, acao, tarefas),
        onSuccess: async (novo) => {
            consultas.setQueryData(consultaProjeto(p.id).queryKey, novo)
            await Promise.all([
                consultas.invalidateQueries({ queryKey: ['projetos', 'lista'] }),
                consultas.invalidateQueries({ queryKey: ['tarefas'] }),
            ])
            setEncerrando(null)
            avisar(`Projeto ${STATUS_PROJETO[novo.status].toLowerCase()}.`)
        },
        // A mensagem fica na janela; a lista de tarefas e a versão voltam atualizadas (F11-31 vale aqui também).
        onError: () =>
            Promise.all([
                consultas.invalidateQueries({ queryKey: consultaProjeto(p.id).queryKey }),
                consultas.invalidateQueries({ queryKey: consultaTarefasDoProjeto(p.id).queryKey }),
            ]),
    })

    const abrirEncerramento = (acao: 'conclusao' | 'cancelamento') => {
        encerramento.reset()
        setEncerrando(acao)
    }

    const progresso = p.progresso
    const total = Number(progresso.tarefasTotal)
    const concluidas = Number(progresso.tarefasConcluidas)
    const apontado = Number(progresso.apontadoMinutos)
    const estimativa = progresso.estimativaMinutos != null ? Number(progresso.estimativaMinutos) : null

    return (
        <div className="flex flex-col gap-6">
            <header className="flex flex-col gap-1">
                <div className="flex items-start gap-3">
                    <h2
                        id={ID_TITULO}
                        ref={refTitulo}
                        tabIndex={-1}
                        className="mr-auto font-titulo text-2xl font-semibold tracking-tight outline-none"
                    >
                        {p.nome}
                    </h2>
                    {gerencia && emAndamento && (
                        <Botao variante="discreto" onPress={() => setEditando(true)} className="!px-0">
                            Editar
                        </Botao>
                    )}
                </div>
                <p className="text-sm text-suave">
                    {pode(perfil, 'cliente.ver') ? (
                        <Link to="/clientes/$id" params={{ id: p.cliente.id }} className="hover:underline">
                            {p.cliente.nome}
                        </Link>
                    ) : (
                        p.cliente.nome
                    )}
                </p>
            </header>

            {gerencia && emAndamento && (
                <div className="flex flex-wrap items-center gap-2">
                    <Botao onPress={() => abrirEncerramento('conclusao')}>Concluir projeto</Botao>
                    <Botao
                        variante="discreto"
                        onPress={() => abrirEncerramento('cancelamento')}
                        className="ml-auto !text-suave hover:!text-vermelho"
                    >
                        Cancelar projeto
                    </Botao>
                </div>
            )}

            <section
                aria-label="Progresso"
                className="flex flex-col gap-4 rounded-couro border border-linha bg-superficie p-4"
            >
                <Barra
                    rotulo="Tarefas concluídas"
                    valor={concluidas}
                    maximo={total}
                    texto={total ? `${concluidas} de ${total}` : 'nenhuma tarefa'}
                />
                <Barra
                    rotulo="Horas apontadas"
                    valor={apontado}
                    maximo={estimativa ?? 0}
                    texto={
                        estimativa != null
                            ? `${formatarDuracao(apontado)} de ${formatarDuracao(estimativa)}`
                            : `${formatarDuracao(apontado)} (sem estimativa)`
                    }
                />
            </section>

            <section aria-label="Dados" className="grid gap-4 sm:grid-cols-2">
                <Dado rotulo="Período">
                    {p.inicio || p.prazo ? (
                        <span className={emAndamento && p.prazo && p.prazo < hoje ? 'text-vermelho' : ''}>
                            {p.inicio ? dataCurta(p.inicio, hoje) : 'sem início'} a{' '}
                            {p.prazo ? dataCurta(p.prazo, hoje) : 'sem prazo'}
                        </span>
                    ) : (
                        <span className="text-suave">Sem datas</span>
                    )}
                </Dado>
                <Dado rotulo="Contrato">
                    {!p.contratoId ? (
                        <span className="text-suave">Sem contrato (horas avulsas)</span>
                    ) : contrato.data ? (
                        <span>
                            {TIPO_CONTRATO[contrato.data.tipo]}{' '}
                            <span className="text-suave">· desde {dataCurta(contrato.data.inicio)}</span>
                        </span>
                    ) : (
                        'Com contrato'
                    )}
                </Dado>
            </section>

            <section aria-labelledby="tarefas-projeto" className="flex flex-col gap-2">
                <h3 id="tarefas-projeto" className="flex items-baseline justify-between text-sm font-semibold">
                    Tarefas
                    {emAndamento && pode(perfil, 'tarefa.criar') && (
                        <Botao variante="discreto" onPress={() => setNovaTarefa(true)} className="!p-0">
                            Nova tarefa
                        </Botao>
                    )}
                </h3>
                {doProjeto.isPending ? (
                    <p className="text-sm text-suave">Carregando…</p>
                ) : doProjeto.isError ? (
                    <Alerta>{mensagemDeErro(doProjeto.error)}</Alerta>
                ) : doProjeto.data.length === 0 ? (
                    <p className="text-sm text-suave">Nenhuma tarefa ainda.</p>
                ) : (
                    <ul className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                        {doProjeto.data.map((t) => (
                            <li key={t.id} className="flex items-center justify-between gap-3 px-3 py-2">
                                <Link
                                    to="/tarefas/$id"
                                    params={{ id: t.id }}
                                    className={`hover:underline ${t.status === 'CANCELADA' ? 'text-suave line-through' : ''}`}
                                >
                                    {t.titulo}
                                </Link>
                                <span className="flex items-center gap-2 text-xs text-suave">
                                    {t.responsavel?.nome}
                                    <span className={`rounded-couro px-2 py-0.5 ${ESTILO_STATUS_TAREFA[t.status]}`}>
                                        {STATUS_TAREFA[t.status]}
                                    </span>
                                </span>
                            </li>
                        ))}
                    </ul>
                )}
            </section>

            <JanelaProjeto
                aberta={editando}
                aoMudar={setEditando}
                projeto={p}
                aoSalvar={(novo) => consultas.setQueryData(consultaProjeto(p.id).queryKey, novo)}
            />
            <JanelaNovaTarefa
                perfil={perfil}
                aberta={novaTarefa}
                aoMudar={setNovaTarefa}
                origem={{ tipo: 'projeto', id: p.id, nome: p.nome, clienteNome: p.cliente.nome }}
                aoCriar={() => undefined}
            />
            <Confirmacao
                titulo={encerrando === 'conclusao' ? 'Concluir o projeto?' : 'Cancelar o projeto?'}
                acao={encerrando === 'conclusao' ? 'Concluir projeto' : 'Cancelar projeto'}
                aberta={encerrando !== null}
                aoMudar={(a) => !a && setEncerrando(null)}
                aoConfirmar={() =>
                    encerrando &&
                    doProjeto.isSuccess &&
                    encerramento.mutate({ acao: encerrando, ...(junto.pedido ? { tarefas: junto.pedido } : {}) })
                }
                // Sem a lista de tarefas, a pessoa confirmaria sem ver as abertas (F11-27, F11-31).
                ocupada={encerramento.isPending || doProjeto.isPending}
                erro={
                    encerramento.isError
                        ? mensagemDeErro(encerramento.error)
                        : doProjeto.isError
                          ? mensagemDeErro(doProjeto.error)
                          : null
                }
            >
                <p>
                    {encerrando === 'conclusao'
                        ? 'Projeto concluído não volta a andamento e não recebe tarefas novas.'
                        : 'Projeto cancelado não volta a andamento e não recebe tarefas novas.'}
                </p>
                {junto.quadro}
                <p>As horas já lançadas continuam como estão.</p>
            </Confirmacao>
        </div>
    )
}
