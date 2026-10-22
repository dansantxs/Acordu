import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useCallback, useEffect, useRef, useState } from 'react'
import { pode } from '@/features/casca/navegacao'
import { consultaContrato } from '@/features/contratos/consultas'
import { TIPO_CONTRATO } from '@/features/solicitacoes/rotulos'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { dataCurta, diaPorExtenso, hojeNoFuso } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { Dado, PainelLateral } from '@/shared/ui/PainelLateral'
import {
    consultaPrevia,
    consultaRecorrencia,
    descreverAntecedencia,
    descreverRegra,
    encerrarRecorrencia,
    ESTILO_STATUS_RECORRENCIA,
    pausarOuRetomar,
    STATUS_RECORRENCIA,
    type FiltrosRecorrencias,
    type Recorrencia,
} from './consultas'
import { JanelaRecorrencia } from './JanelaRecorrencia'

const ID_TITULO = 'titulo-recorrencia'

/** Detalhe da recorrência em painel (F4c): regra em frase, próximas datas, modelo da tarefa e as transições. */
export function PainelRecorrencia({ perfil, id }: { perfil: Perfil; id: string }) {
    const navegar = useNavigate()
    const titulo = useRef<HTMLHeadingElement>(null)
    const consulta = useQuery(consultaRecorrencia(id))
    const fechar = useCallback(() => {
        void navegar({ to: '/recorrencias', search: (atual: FiltrosRecorrencias) => atual }).then(() =>
            document.querySelector<HTMLElement>(`[data-recorrencia="${id}"]`)?.focus(),
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
                    <span className="text-sm text-suave">Recorrência</span>
                    {consulta.data && (
                        <span
                            className={`rounded-couro px-2 py-0.5 text-xs ${ESTILO_STATUS_RECORRENCIA[consulta.data.status]}`}
                        >
                            {STATUS_RECORRENCIA[consulta.data.status]}
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
                        ? 'Não encontramos esta recorrência.'
                        : mensagemDeErro(consulta.error)}
                </Alerta>
            ) : (
                <Detalhe recorrencia={consulta.data} perfil={perfil} refTitulo={titulo} />
            )}
        </PainelLateral>
    )
}

function Detalhe({
    recorrencia: r,
    perfil,
    refTitulo,
}: {
    recorrencia: Recorrencia
    perfil: Perfil
    refTitulo: React.RefObject<HTMLHeadingElement | null>
}) {
    const consultas = useQueryClient()
    const fuso = perfil.tenant.fusoHorario
    const hoje = hojeNoFuso(fuso)
    const [editando, setEditando] = useState(false)
    const [encerrando, setEncerrando] = useState(false)
    const gerencia = pode(perfil, 'recorrencia.gerenciar')
    const encerrada = r.status === 'ENCERRADA'
    const equipe = useQuery(consultaEquipe)
    const previa = useQuery({ ...consultaPrevia(r.id, r.versao), enabled: !encerrada })
    const contrato = useQuery({
        ...consultaContrato(r.contratoId ?? ''),
        enabled: r.contratoId !== null && pode(perfil, 'contrato.ver'),
    })
    const responsavel = r.modelo.responsavelId
        ? (equipe.data?.find((p) => p.id === r.modelo.responsavelId)?.nome ?? 'Pessoa que saiu da equipe')
        : null

    const transicao = useMutation({
        mutationFn: (acao: 'pausa' | 'retomada') => pausarOuRetomar(r, acao),
        onSuccess: async (nova) => {
            consultas.setQueryData(consultaRecorrencia(r.id).queryKey, nova)
            await consultas.invalidateQueries({ queryKey: ['recorrencias', 'lista'] })
            avisar(
                nova.status === 'PAUSADA'
                    ? 'Recorrência pausada: nenhuma tarefa nova até retomar.'
                    : 'Recorrência retomada.',
            )
        },
        onError: (erro) => {
            avisarErro(erro)
            void consultas.invalidateQueries({ queryKey: consultaRecorrencia(r.id).queryKey })
        },
    })
    const encerramento = useMutation({
        mutationFn: () => encerrarRecorrencia(r),
        onSuccess: async () => {
            await consultas.invalidateQueries({ queryKey: ['recorrencias'] })
            setEncerrando(false)
            avisar('Recorrência encerrada.')
        },
    })

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
                        {r.modelo.titulo}
                    </h2>
                    {gerencia && !encerrada && (
                        <Botao variante="discreto" onPress={() => setEditando(true)} className="!px-0">
                            Editar
                        </Botao>
                    )}
                </div>
                <p className="text-sm text-suave">
                    {pode(perfil, 'cliente.ver') ? (
                        <Link to="/clientes/$id" params={{ id: r.clienteId }} className="hover:underline">
                            {r.clienteNome}
                        </Link>
                    ) : (
                        r.clienteNome
                    )}
                </p>
            </header>

            {gerencia && !encerrada && (
                <div className="flex flex-wrap items-center gap-2">
                    {r.status === 'ATIVA' ? (
                        <Botao
                            variante="secundario"
                            isPending={transicao.isPending}
                            onPress={() => transicao.mutate('pausa')}
                        >
                            Pausar
                        </Botao>
                    ) : (
                        <Botao isPending={transicao.isPending} onPress={() => transicao.mutate('retomada')}>
                            Retomar
                        </Botao>
                    )}
                    <Botao
                        variante="discreto"
                        onPress={() => setEncerrando(true)}
                        className="ml-auto !text-suave hover:!text-vermelho"
                    >
                        Encerrar
                    </Botao>
                </div>
            )}

            <section
                aria-labelledby="regra-recorrencia"
                className="flex flex-col gap-3 rounded-couro border border-linha bg-superficie p-4 text-sm"
            >
                <h3 id="regra-recorrencia" className="font-semibold">
                    {descreverRegra(r.regra, hoje)}
                </h3>
                <p className="text-suave">
                    {descreverAntecedencia(Number(r.regra.antecedenciaDias))}
                    {r.regra.inicio && ` Desde ${dataCurta(r.regra.inicio, hoje)}.`}
                    {r.regra.fim && ` Até ${dataCurta(r.regra.fim, hoje)}.`}
                </p>
                {encerrada ? (
                    <p className="text-suave">Encerrada: não gera mais tarefas.</p>
                ) : (
                    <div className="flex flex-col gap-1">
                        <span className="text-xs font-semibold text-suave">
                            {r.status === 'PAUSADA' ? 'Datas se retomar hoje' : 'Próximas datas'}
                        </span>
                        {previa.isPending ? (
                            <span className="text-suave">Carregando…</span>
                        ) : previa.isError ? (
                            <Alerta>{mensagemDeErro(previa.error)}</Alerta>
                        ) : previa.data.length === 0 ? (
                            <span className="text-suave">Nenhuma: a regra já terminou.</span>
                        ) : (
                            <ol className="flex flex-wrap gap-2">
                                {previa.data.map((d) => (
                                    <li key={d} className="rounded-couro bg-trilho px-2 py-1 tabular-nums">
                                        {diaPorExtenso(d)}
                                        {d.slice(0, 4) !== hoje.slice(0, 4) && `/${d.slice(2, 4)}`}
                                    </li>
                                ))}
                            </ol>
                        )}
                    </div>
                )}
            </section>

            <section aria-label="Modelo da tarefa" className="grid gap-4 sm:grid-cols-2">
                <Dado rotulo="Responsável">{responsavel ?? <span className="text-suave">Sem responsável</span>}</Dado>
                <Dado rotulo="Estimativa">
                    {r.modelo.estimativaMinutos != null ? (
                        formatarDuracao(Number(r.modelo.estimativaMinutos))
                    ) : (
                        <span className="text-suave">Sem estimativa</span>
                    )}
                </Dado>
                <Dado rotulo="Contrato">
                    {!r.contratoId ? (
                        <span className="text-suave">Sem contrato (horas avulsas)</span>
                    ) : contrato.data ? (
                        <Link to="/contratos/$id" params={{ id: r.contratoId }} className="hover:underline">
                            {TIPO_CONTRATO[contrato.data.tipo]}
                        </Link>
                    ) : (
                        'Com contrato'
                    )}
                </Dado>
                <Dado rotulo="No portal">{r.modelo.visivelCliente ? 'O cliente vê as tarefas' : 'Só a equipe'}</Dado>
                {r.modelo.descricao && (
                    <div className="sm:col-span-2">
                        <Dado rotulo="Descrição">
                            <span className="whitespace-pre-line">{r.modelo.descricao}</span>
                        </Dado>
                    </div>
                )}
                {r.modelo.checklist && r.modelo.checklist.length > 0 && (
                    <div className="sm:col-span-2">
                        <Dado rotulo="Checklist">
                            <ul className="list-inside list-disc">
                                {r.modelo.checklist.map((item, i) => (
                                    <li key={i}>{item}</li>
                                ))}
                            </ul>
                        </Dado>
                    </div>
                )}
            </section>

            <JanelaRecorrencia
                fuso={fuso}
                aberta={editando}
                aoMudar={setEditando}
                recorrencia={r}
                aoSalvar={(nova) => consultas.setQueryData(consultaRecorrencia(r.id).queryKey, nova)}
            />
            <Confirmacao
                titulo="Encerrar a recorrência?"
                acao="Encerrar recorrência"
                aberta={encerrando}
                aoMudar={(a) => {
                    if (!a) {
                        encerramento.reset()
                        setEncerrando(false)
                    }
                }}
                aoConfirmar={() => encerramento.mutate()}
                ocupada={encerramento.isPending}
                erro={encerramento.isError ? mensagemDeErro(encerramento.error) : null}
            >
                <p>Encerrada, ela não gera mais tarefas e não volta a valer. Para só dar um tempo, use Pausar.</p>
                <p>As tarefas já criadas continuam como estão.</p>
            </Confirmacao>
        </div>
    )
}
