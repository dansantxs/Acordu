import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Button } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { formatarDuracao } from '@/shared/format/duracao'
import { dataHora, prazoRelativo } from '@/shared/format/prazo'
import { quando } from '@/shared/format/quando'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { Icone } from '@/shared/ui/Icone'
import {
    consultaCategorias,
    consultaContrato,
    consultaSolicitacao,
    editarSolicitacao,
    formularioDe,
    transicionar,
    type EdicaoSolicitacao,
    type FiltrosFila,
    type Solicitacao,
    type Transicao,
} from './consultas'
import { Conversa } from './Conversa'
import { JanelaContrato } from './JanelaContrato'
import { JanelaLinkPortal } from './JanelaLinkPortal'
import { Seguidores } from './Seguidores'
import { Semelhantes } from './Semelhantes'
import { JanelaEdicao, JanelaMotivo, JanelaResolucao } from './JanelasSolicitacao'
import {
    EM_ABERTO,
    ESTILO_PRIORIDADE,
    ESTILO_STATUS,
    FINAIS,
    ORIGEM,
    PRIORIDADE,
    ROTULO_ACAO,
    SITUACAO_SLA,
    STATUS,
    STATUS_TAREFA,
    TIPO_CONTRATO,
    TRANSICOES,
    type AcaoStatus,
    type Prioridade,
} from './rotulos'

const ID_TITULO = 'titulo-solicitacao'

/**
 * Detalhe da solicitação em painel sobre a fila (F2, plano de 16/10): endereço próprio (`/solicitacoes/{id}`), a fila
 * continua clicável embaixo e Esc fecha. Não é modal de propósito: trocar de solicitação é clicar na próxima linha.
 */
export function PainelSolicitacao({ perfil, id }: { perfil: Perfil; id: string }) {
    const navegar = useNavigate()
    const titulo = useRef<HTMLHeadingElement>(null)
    const consulta = useQuery(consultaSolicitacao(id))

    useEffect(() => {
        function fechar() {
            void navegar({ to: '/solicitacoes', search: (atual: FiltrosFila) => atual }).then(() =>
                // O foco volta para a linha de onde a pessoa veio, se ela estiver na página.
                document.querySelector<HTMLElement>(`[data-solicitacao="${id}"]`)?.focus(),
            )
        }
        function aoTeclar(evento: KeyboardEvent) {
            if (evento.key !== 'Escape' || evento.defaultPrevented) return
            // Esc numa lista aberta, num campo de escolha ou numa janela é deles, não do painel.
            const alvo = evento.target instanceof Element ? evento.target : null
            if (alvo?.closest('[role="combobox"], [role="listbox"], [role="dialog"], [role="menu"]')) return
            fechar()
        }
        document.addEventListener('keydown', aoTeclar)
        return () => document.removeEventListener('keydown', aoTeclar)
    }, [id, navegar])

    const carregada = consulta.isSuccess
    useEffect(() => {
        if (carregada) titulo.current?.focus()
    }, [carregada])

    return (
        <aside
            aria-labelledby={ID_TITULO}
            className="fixed inset-y-0 right-0 z-40 flex w-full max-w-2xl flex-col border-l border-linha bg-fundo text-tinta shadow-2xl"
        >
            <div className="flex items-center gap-3 border-b border-linha bg-superficie px-5 py-3">
                <span className="text-sm text-suave tabular-nums">
                    {consulta.data ? `#${consulta.data.numero}` : 'Solicitação'}
                </span>
                {consulta.data && (
                    <span className={`rounded-couro px-2 py-0.5 text-xs ${ESTILO_STATUS[consulta.data.status]}`}>
                        {STATUS[consulta.data.status]}
                    </span>
                )}
                <Link
                    to="/solicitacoes"
                    search={(atual: FiltrosFila) => atual}
                    aria-label="Fechar"
                    title="Fechar (Esc)"
                    className="ml-auto grid size-8 place-items-center rounded-couro text-suave hover:bg-trilho hover:text-tinta"
                >
                    <Icone nome="fechar" />
                </Link>
            </div>
            <div className="flex-1 overflow-y-auto px-5 py-5">
                {consulta.isPending ? (
                    <p className="text-sm text-suave">Carregando…</p>
                ) : consulta.isError ? (
                    <Alerta>
                        {consulta.error instanceof ErroApi && consulta.error.status === 404
                            ? 'Não encontramos esta solicitação. Ela pode ter outro endereço ou não estar visível para você.'
                            : mensagemDeErro(consulta.error)}
                    </Alerta>
                ) : (
                    <Detalhe solicitacao={consulta.data} perfil={perfil} refTitulo={titulo} />
                )}
            </div>
        </aside>
    )
}

function Dado({ rotulo, children }: { rotulo: string; children: ReactNode }) {
    return (
        <div className="flex flex-col gap-1">
            <p className="text-sm font-semibold">{rotulo}</p>
            <div className="py-2 text-sm">{children}</div>
        </div>
    )
}

function Detalhe({
    solicitacao: s,
    perfil,
    refTitulo,
}: {
    solicitacao: Solicitacao
    perfil: Perfil
    refTitulo: React.RefObject<HTMLHeadingElement | null>
}) {
    const consultas = useQueryClient()
    const chave = consultaSolicitacao(s.id).queryKey
    const fuso = perfil.tenant.fusoHorario
    const equipe = useQuery(consultaEquipe)
    const categorias = useQuery(consultaCategorias)
    const [editando, setEditando] = useState(false)
    const [janelaLink, setJanelaLink] = useState(false)
    const [janelaContrato, setJanelaContrato] = useState(false)

    const podeEditar = pode(perfil, 'solicitacao.editar')
    const final = FINAIS.includes(s.status)
    const editavel = podeEditar && !final

    function guardar(nova: Solicitacao) {
        consultas.setQueryData(chave, nova)
        void consultas.invalidateQueries({ queryKey: ['solicitacoes', 'lista'] })
    }
    function falhou(erro: unknown) {
        avisarErro(erro)
        // 409 (outra pessoa mexeu) ou transição que deixou de valer: o painel volta ao que está gravado.
        void consultas.invalidateQueries({ queryKey: chave })
    }

    // Responsável, prioridade e categoria mudam na hora e oferecem desfazer (plano do F2).
    const edicao = useMutation({
        mutationFn: ({ base, mudanca }: { base: Solicitacao; mudanca: Partial<EdicaoSolicitacao>; aviso?: string }) =>
            editarSolicitacao(base, mudanca),
        onSuccess: (nova, { base, mudanca, aviso }) => {
            guardar(nova)
            if (!aviso) return
            const campo = Object.keys(mudanca)[0] as keyof EdicaoSolicitacao
            const anterior = { [campo]: formularioDe(base)[campo] }
            avisar(aviso, {
                desfazer: () =>
                    edicao.mutate({ base: consultas.getQueryData<Solicitacao>(chave) ?? nova, mudanca: anterior }),
            })
        },
        onError: falhou,
    })

    const nomeDe = (id: string | null) => (equipe.data ?? []).find((p) => p.id === id)?.nome
    const categoriasAtivas = (categorias.data ?? []).filter((c) => c.ativa || c.id === s.categoriaId)
    const nomeCategoria = (categorias.data ?? []).find((c) => c.id === s.categoriaId)?.nome

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
                        {s.titulo}
                    </h2>
                    {pode(perfil, 'portal.gerenciar_acessos') && !final && (
                        <Botao variante="discreto" onPress={() => setJanelaLink(true)} className="!px-0">
                            Link do portal
                        </Botao>
                    )}
                    {editavel && (
                        <Botao variante="discreto" onPress={() => setEditando(true)} className="!px-0">
                            Editar
                        </Botao>
                    )}
                </div>
                <p className="text-sm text-suave">
                    {pode(perfil, 'cliente.ver') ? (
                        <Link to="/clientes/$id" params={{ id: s.cliente.id }} className="hover:underline">
                            {s.cliente.nome}
                        </Link>
                    ) : (
                        s.cliente.nome
                    )}
                    {s.contato && ` · ${s.contato.nome}`} · aberta {quando(s.abertaEm, fuso)} {ORIGEM[s.origem]}
                </p>
            </header>

            {podeEditar && <AcoesStatus solicitacao={s} aoMudar={guardar} aoFalhar={falhou} />}

            {s.status === 'CANCELADA' && (
                <Alerta tom="aviso">
                    Cancelada{s.canceladaEm && ` em ${dataHora(s.canceladaEm, fuso)}`}
                    {s.motivoCancelamento && `: ${s.motivoCancelamento}`}
                </Alerta>
            )}
            {s.solucao && (
                <section aria-labelledby="solucao" className="rounded-couro border border-verde/40 bg-verde-suave p-4">
                    <h3 id="solucao" className="text-sm font-semibold text-verde">
                        Solução{s.resolvidaEm && ` · ${dataHora(s.resolvidaEm, fuso)}`}
                    </h3>
                    {s.solucao.problema && (
                        <p className="mt-2 text-sm">
                            <span className="font-semibold">Problema:</span> {s.solucao.problema}
                        </p>
                    )}
                    {s.solucao.causa && (
                        <p className="mt-1 text-sm">
                            <span className="font-semibold">Causa:</span> {s.solucao.causa}
                        </p>
                    )}
                    <p className="mt-2 text-sm whitespace-pre-line">{s.solucao.texto}</p>
                </section>
            )}

            <section aria-label="Atendimento" className="grid gap-4 sm:grid-cols-2">
                {editavel ? (
                    <CampoSelecao
                        rotulo="Responsável"
                        opcoes={(equipe.data ?? []).map((p) => ({ id: p.id, rotulo: p.nome }))}
                        valor={s.responsavel?.id ?? null}
                        aoMudar={(v) =>
                            v !== (s.responsavel?.id ?? null) &&
                            edicao.mutate({
                                base: s,
                                mudanca: { responsavelId: v },
                                aviso: v ? `Responsável: ${nomeDe(v) ?? 'alterado'}.` : 'Sem responsável agora.',
                            })
                        }
                        vazio="Sem responsável"
                    />
                ) : (
                    <Dado rotulo="Responsável">{s.responsavel?.nome ?? 'Sem responsável'}</Dado>
                )}
                {editavel && s.status !== 'RESOLVIDA' ? (
                    <CampoSelecao
                        rotulo="Prioridade"
                        opcoes={(Object.keys(PRIORIDADE) as Prioridade[]).map((p) => ({
                            id: p,
                            rotulo: PRIORIDADE[p],
                        }))}
                        valor={s.prioridade}
                        aoMudar={(v) =>
                            v &&
                            v !== s.prioridade &&
                            edicao.mutate({
                                base: s,
                                mudanca: { prioridade: v as Prioridade },
                                aviso: `Prioridade: ${PRIORIDADE[v as Prioridade]}.`,
                            })
                        }
                    />
                ) : (
                    <Dado rotulo="Prioridade">
                        <span className={ESTILO_PRIORIDADE[s.prioridade]}>{PRIORIDADE[s.prioridade]}</span>
                    </Dado>
                )}
                {editavel && categoriasAtivas.length > 0 ? (
                    <CampoSelecao
                        rotulo="Categoria"
                        opcoes={categoriasAtivas.map((c) => ({ id: c.id, rotulo: c.nome }))}
                        valor={s.categoriaId}
                        aoMudar={(v) =>
                            v !== s.categoriaId &&
                            edicao.mutate({
                                base: s,
                                mudanca: { categoriaId: v },
                                aviso: v
                                    ? `Categoria: ${categoriasAtivas.find((c) => c.id === v)?.nome}.`
                                    : 'Sem categoria agora.',
                            })
                        }
                        vazio="Sem categoria"
                    />
                ) : (
                    s.categoriaId && <Dado rotulo="Categoria">{nomeCategoria ?? '—'}</Dado>
                )}
                <Dado rotulo="Contrato">
                    <span className="flex flex-wrap items-baseline gap-x-3">
                        <ContratoDaSolicitacao contratoId={s.contratoId} perfil={perfil} />
                        {/* Contrato não troca em RESOLVIDA nem em estado final (modelo §4.3). */}
                        {editavel && s.status !== 'RESOLVIDA' && pode(perfil, 'contrato.ver') && (
                            <Botao variante="discreto" onPress={() => setJanelaContrato(true)} className="!p-0">
                                Trocar
                            </Botao>
                        )}
                    </span>
                </Dado>
                <Dado rotulo="Tempo apontado">{formatarDuracao(Number(s.tempoApontadoMinutos))}</Dado>
                <Seguidores solicitacaoId={s.id} usuarioId={perfil.usuarioId} />
            </section>

            <Prazos solicitacao={s} fuso={fuso} />

            <section aria-labelledby="descricao" className="flex flex-col gap-1">
                <h3 id="descricao" className="text-sm font-semibold">
                    Descrição
                </h3>
                {s.descricao ? (
                    <p className="text-sm whitespace-pre-line">{s.descricao}</p>
                ) : (
                    <p className="text-sm text-suave">Sem descrição.</p>
                )}
            </section>

            {EM_ABERTO.includes(s.status) && <Semelhantes solicitacaoId={s.id} fuso={fuso} />}

            {s.tarefas.length > 0 && (
                <section aria-labelledby="tarefas" className="flex flex-col gap-2">
                    <h3 id="tarefas" className="text-sm font-semibold">
                        Tarefas
                    </h3>
                    <ul className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                        {s.tarefas.map((t) => (
                            <li key={t.id} className="flex items-center justify-between gap-3 px-3 py-2">
                                <span className={t.status === 'CANCELADA' ? 'text-suave line-through' : ''}>
                                    {t.titulo}
                                </span>
                                <span className="text-xs text-suave">{STATUS_TAREFA[t.status]}</span>
                            </li>
                        ))}
                    </ul>
                </section>
            )}

            <Conversa solicitacao={s} fuso={fuso} podeComentar={pode(perfil, 'solicitacao.comentar')} />

            <JanelaLinkPortal
                solicitacao={s}
                fuso={fuso}
                podeVerCliente={pode(perfil, 'cliente.ver')}
                aberta={janelaLink}
                aoMudar={setJanelaLink}
            />
            <JanelaContrato
                solicitacao={s}
                aberta={janelaContrato}
                aoMudar={setJanelaContrato}
                aoTrocar={(nova) => {
                    guardar(nova)
                    setJanelaContrato(false)
                    avisar(
                        nova.contratoId
                            ? 'Contrato trocado; o prazo foi recalculado.'
                            : 'Solicitação sem contrato agora: sem prazo.',
                    )
                }}
            />
            <JanelaEdicao
                solicitacao={s}
                aberta={editando}
                aoMudar={setEditando}
                aoSalvar={(nova) => {
                    guardar(nova)
                    setEditando(false)
                }}
            />
        </div>
    )
}

function ContratoDaSolicitacao({ contratoId, perfil }: { contratoId: string | null; perfil: Perfil }) {
    const podeVer = pode(perfil, 'contrato.ver')
    const contrato = useQuery({ ...consultaContrato(contratoId ?? ''), enabled: contratoId !== null && podeVer })
    if (!contratoId) return <span className="text-suave">Sem contrato (sem prazo)</span>
    if (!podeVer || !contrato.data) return <span>Com contrato</span>
    const desde = contrato.data.inicio.split('-').reverse().join('/')
    return (
        <span>
            {TIPO_CONTRATO[contrato.data.tipo]} <span className="text-suave">· desde {desde}</span>
        </span>
    )
}

function Prazos({ solicitacao: s, fuso }: { solicitacao: Solicitacao; fuso: string }) {
    const sla = s.sla
    if (!sla) return null
    const situacao = SITUACAO_SLA[sla.situacao]
    const percentual = Math.min(Number(sla.percentualConsumido), 100)
    const cumprido = sla.situacao === 'CUMPRIDO'
    const respondidaNoPrazo =
        sla.primeiraRespostaEm && new Date(sla.primeiraRespostaEm) <= new Date(sla.prazoPrimeiraResposta)
    return (
        <section
            aria-labelledby="prazos"
            className="flex flex-col gap-3 rounded-couro border border-linha bg-superficie p-4"
        >
            <h3 id="prazos" className="text-sm font-semibold">
                Prazos (SLA)
            </h3>
            <div className="grid gap-3 text-sm sm:grid-cols-2">
                <div>
                    <p className="text-suave">Primeira resposta</p>
                    {sla.primeiraRespostaEm ? (
                        <p className={respondidaNoPrazo ? 'text-verde' : 'text-vermelho'}>
                            respondida {dataHora(sla.primeiraRespostaEm, fuso)}
                            {!respondidaNoPrazo && ' (fora do prazo)'}
                        </p>
                    ) : (
                        <p>
                            até {dataHora(sla.prazoPrimeiraResposta, fuso)}{' '}
                            <span className="text-suave">({prazoRelativo(sla.prazoPrimeiraResposta, fuso)})</span>
                        </p>
                    )}
                </div>
                <div>
                    <p className="text-suave">Solução</p>
                    <p>
                        até {dataHora(sla.prazoResolucao, fuso)}{' '}
                        {!cumprido && !sla.pausado && (
                            <span className="text-suave">({prazoRelativo(sla.prazoResolucao, fuso)})</span>
                        )}
                    </p>
                </div>
            </div>
            {situacao && (
                <div className="flex flex-col gap-1">
                    <div
                        role="meter"
                        aria-label="Prazo de solução consumido"
                        aria-valuemin={0}
                        aria-valuemax={100}
                        aria-valuenow={percentual}
                        aria-valuetext={`${Number(sla.percentualConsumido)}% · ${situacao.rotulo}`}
                        className="h-2 overflow-hidden rounded-full bg-trilho"
                    >
                        <div
                            className={`h-full ${sla.pausado ? 'bg-suave' : situacao.estilo}`}
                            style={{ width: `${cumprido ? 100 : percentual}%` }}
                        />
                    </div>
                    <p className="text-xs text-suave">
                        {sla.pausado
                            ? 'Relógio pausado enquanto aguarda o cliente.'
                            : `${situacao.rotulo} · ${Number(sla.percentualConsumido)}% do prazo em horas úteis`}
                    </p>
                </div>
            )}
        </section>
    )
}

/** Mudanças de status (RF-SOL-03): valem na hora, sem desfazer; resolver, reabrir e cancelar pedem um texto. */
function AcoesStatus({
    solicitacao: s,
    aoMudar,
    aoFalhar,
}: {
    solicitacao: Solicitacao
    aoMudar: (nova: Solicitacao) => void
    aoFalhar: (erro: unknown) => void
}) {
    const [janela, setJanela] = useState<'resolucao' | 'reabertura' | 'cancelamento' | null>(null)
    const transicao = useMutation({
        mutationFn: (t: Transicao) => transicionar(s, t),
        onSuccess: (nova) => {
            aoMudar(nova)
            setJanela(null)
            avisar(`Solicitação #${nova.numero}: ${STATUS[nova.status].toLowerCase()}.`)
        },
    })
    const acoes = TRANSICOES[s.status]
    if (acoes.length === 0) return null

    const executar = (acao: AcaoStatus) => {
        if (acao === 'resolucao' || acao === 'reabertura' || acao === 'cancelamento') {
            transicao.reset()
            setJanela(acao)
        } else transicao.mutate({ acao }, { onError: aoFalhar })
    }
    const principal = acoes[0]
    const demais = acoes.slice(1).filter((a) => a !== 'cancelamento')

    return (
        <div className="flex flex-wrap items-center gap-2">
            {principal && principal !== 'cancelamento' && (
                <Botao
                    onPress={() => executar(principal)}
                    isPending={transicao.isPending && transicao.variables.acao === principal}
                >
                    {ROTULO_ACAO[principal]}
                </Botao>
            )}
            {demais.map((acao) => (
                <Botao
                    key={acao}
                    variante="secundario"
                    onPress={() => executar(acao)}
                    isPending={transicao.isPending && transicao.variables.acao === acao}
                >
                    {ROTULO_ACAO[acao]}
                </Botao>
            ))}
            {acoes.includes('cancelamento') && (
                <Button
                    onPress={() => executar('cancelamento')}
                    className="ml-auto rounded-couro px-2 py-1 text-sm text-suave hover:text-vermelho hover:underline"
                >
                    {ROTULO_ACAO.cancelamento}
                </Button>
            )}

            <JanelaResolucao
                aberta={janela === 'resolucao'}
                aoMudar={(a) => !a && setJanela(null)}
                ocupada={transicao.isPending}
                erro={transicao.error}
                aoResolver={(solucao) => transicao.mutate({ acao: 'resolucao', solucao })}
            />
            <JanelaMotivo
                titulo="Reabrir solicitação"
                acao="Reabrir"
                explicacao="A solução registrada sai da solicitação e o prazo volta a correr."
                aberta={janela === 'reabertura'}
                aoMudar={(a) => !a && setJanela(null)}
                ocupada={transicao.isPending}
                erro={transicao.error}
                aoConfirmar={(motivo) => transicao.mutate({ acao: 'reabertura', motivo })}
            />
            <JanelaMotivo
                titulo={`Cancelar a solicitação #${s.numero}?`}
                acao="Cancelar solicitação"
                explicacao="Cancelar é definitivo: a solicitação sai da fila e não pode ser reaberta. O cliente vê como cancelada no portal."
                perigosa
                aberta={janela === 'cancelamento'}
                aoMudar={(a) => !a && setJanela(null)}
                ocupada={transicao.isPending}
                erro={transicao.error}
                aoConfirmar={(motivo) => transicao.mutate({ acao: 'cancelamento', motivo })}
            />
        </div>
    )
}
