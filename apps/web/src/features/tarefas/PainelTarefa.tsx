import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useCallback, useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { Button, Checkbox, Form, Switch } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { opcional } from '@/features/clientes/consultas'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { formatarDuracao, lerDuracao } from '@/shared/format/duracao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { Icone } from '@/shared/ui/Icone'
import { Janela } from '@/shared/ui/Janela'
import { Seguidores } from '@/shared/colaboracao/Seguidores'
import { Dado, PainelLateral } from '@/shared/ui/PainelLateral'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import {
    adicionarItem,
    consultaTarefa,
    editarItem,
    editarTarefa,
    formularioDaTarefa,
    removerItem,
    transicionarTarefa,
    type AcaoTarefa,
    type EdicaoTarefa,
    type FiltrosTarefas,
    type Tarefa,
} from './consultas'
import { BotaoCronometro } from '@/features/cronometro/Cronometro'
import { Comentarios } from './Comentarios'
import {
    ESTILO_STATUS_TAREFA,
    ROTULO_ACAO_TAREFA,
    STATUS_TAREFA,
    TRANSICOES_TAREFA,
    type AcaoStatusTarefa,
} from './rotulos'

const ID_TITULO = 'titulo-tarefa'

/**
 * Detalhe da tarefa em painel sobre a lista ou o quadro (plano do F3, 20/10), no padrão das solicitações (convenções
 * §4). Status pelos botões, sem desfazer; responsável muda na hora com desfazer; prazo, dia planejado, estimativa e
 * visibilidade salvam ao mudar.
 */
export function PainelTarefa({ perfil, id }: { perfil: Perfil; id: string }) {
    const navegar = useNavigate()
    const titulo = useRef<HTMLHeadingElement>(null)
    const consulta = useQuery(consultaTarefa(id))

    const fechar = useCallback(() => {
        void navegar({ to: '/tarefas', search: (atual: FiltrosTarefas) => atual }).then(() =>
            document.querySelector<HTMLElement>(`[data-tarefa="${id}"]`)?.focus(),
        )
    }, [id, navegar])

    const carregada = consulta.isSuccess
    useEffect(() => {
        if (carregada) titulo.current?.focus()
    }, [carregada])

    return (
        <PainelLateral
            idTitulo={ID_TITULO}
            fechar={fechar}
            cabecalho={
                <>
                    <span className="text-sm text-suave">Tarefa</span>
                    {consulta.data && (
                        <span
                            className={`rounded-couro px-2 py-0.5 text-xs ${ESTILO_STATUS_TAREFA[consulta.data.status]}`}
                        >
                            {STATUS_TAREFA[consulta.data.status]}
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
                        ? 'Não encontramos esta tarefa. Ela pode ter outro endereço ou não estar visível para você.'
                        : mensagemDeErro(consulta.error)}
                </Alerta>
            ) : (
                <Detalhe tarefa={consulta.data} perfil={perfil} refTitulo={titulo} />
            )}
        </PainelLateral>
    )
}

function Detalhe({
    tarefa: t,
    perfil,
    refTitulo,
}: {
    tarefa: Tarefa
    perfil: Perfil
    refTitulo: React.RefObject<HTMLHeadingElement | null>
}) {
    const consultas = useQueryClient()
    const chave = consultaTarefa(t.id).queryKey
    const equipe = useQuery(consultaEquipe)
    const [editando, setEditando] = useState(false)
    const podeEditar = pode(perfil, 'tarefa.editar')
    const editavel = podeEditar && t.status !== 'CANCELADA'

    function guardar(nova: Tarefa) {
        consultas.setQueryData(chave, nova)
        void consultas.invalidateQueries({ queryKey: ['tarefas', 'lista'] })
        void consultas.invalidateQueries({ queryKey: ['tarefas', 'quadro'] })
    }
    function falhou(erro: unknown) {
        avisarErro(erro)
        void consultas.invalidateQueries({ queryKey: chave })
    }

    const edicao = useMutation({
        mutationFn: ({ base, mudanca }: { base: Tarefa; mudanca: Partial<EdicaoTarefa>; aviso?: string }) =>
            editarTarefa(base, mudanca),
        onSuccess: (nova, { base, mudanca, aviso }) => {
            guardar(nova)
            if (!aviso) return
            const campo = Object.keys(mudanca)[0] as keyof EdicaoTarefa
            const anterior = { [campo]: formularioDaTarefa(base)[campo] }
            avisar(aviso, {
                desfazer: () =>
                    edicao.mutate({ base: consultas.getQueryData<Tarefa>(chave) ?? nova, mudanca: anterior }),
            })
        },
        onError: falhou,
    })
    const salvar = (mudanca: Partial<EdicaoTarefa>, aviso?: string) => edicao.mutate({ base: t, mudanca, aviso })
    const nomeDe = (id: string | null) => (equipe.data ?? []).find((p) => p.id === id)?.nome

    const origem = [
        t.clienteNome && t.clienteId ? (
            pode(perfil, 'cliente.ver') ? (
                <Link key="c" to="/clientes/$id" params={{ id: t.clienteId }} className="hover:underline">
                    {t.clienteNome}
                </Link>
            ) : (
                <span key="c">{t.clienteNome}</span>
            )
        ) : null,
        t.solicitacaoId && t.solicitacaoNumero != null ? (
            pode(perfil, 'solicitacao.ver') ? (
                <Link key="s" to="/solicitacoes/$id" params={{ id: t.solicitacaoId }} className="hover:underline">
                    solicitação #{t.solicitacaoNumero}
                </Link>
            ) : (
                <span key="s">solicitação #{t.solicitacaoNumero}</span>
            )
        ) : null,
        t.projetoNome ? <span key="p">projeto {t.projetoNome}</span> : null,
        t.recorrenciaId ? <span key="r">recorrente</span> : null,
    ].filter(Boolean)

    const estimativa = t.estimativaMinutos != null ? Number(t.estimativaMinutos) : null
    const apontado = Number(t.apontadoMinutos)

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
                        {t.titulo}
                    </h2>
                    {editavel && (
                        <Botao variante="discreto" onPress={() => setEditando(true)} className="!px-0">
                            Editar
                        </Botao>
                    )}
                </div>
                <p className="flex flex-wrap gap-x-1 text-sm text-suave">
                    {origem.length
                        ? origem.flatMap((parte, n) => (n === 0 ? [parte] : [<span key={`sep${n}`}>·</span>, parte]))
                        : 'Tarefa interna, sem cliente'}
                </p>
            </header>

            {podeEditar && <AcoesStatus tarefa={t} aoMudar={guardar} aoFalhar={falhou} />}
            {pode(perfil, 'apontamento.criar') && (t.status === 'A_FAZER' || t.status === 'FAZENDO') && (
                <div>
                    <BotaoCronometro tarefaId={t.id} />
                </div>
            )}

            <section aria-label="Planejamento" className="grid gap-4 sm:grid-cols-2">
                {editavel ? (
                    <CampoSelecao
                        rotulo="Responsável"
                        opcoes={(equipe.data ?? []).map((p) => ({ id: p.id, rotulo: p.nome }))}
                        valor={t.responsavel?.id ?? null}
                        aoMudar={(v) =>
                            v !== (t.responsavel?.id ?? null) &&
                            salvar(
                                { responsavelId: v },
                                v ? `Responsável: ${nomeDe(v) ?? 'alterado'}.` : 'Sem responsável agora.',
                            )
                        }
                        vazio="Sem responsável"
                    />
                ) : (
                    <Dado rotulo="Responsável">{t.responsavel?.nome ?? 'Sem responsável'}</Dado>
                )}
                <CampoData rotulo="Prazo" valor={t.prazo} editavel={editavel} aoMudar={(prazo) => salvar({ prazo })} />
                <CampoData
                    rotulo="Planejada para"
                    descricao="O dia de fazer, separado do prazo."
                    valor={t.planejadaPara}
                    editavel={editavel}
                    aoMudar={(planejadaPara) => salvar({ planejadaPara })}
                />
                <CampoDuracao
                    // Recria o campo quando o valor salvo muda (desfazer, outra aba), sem efeito para copiar o estado.
                    key={estimativa ?? 'vazia'}
                    rotulo="Estimativa"
                    valor={estimativa}
                    editavel={editavel}
                    aoMudar={(estimativaMinutos) => salvar({ estimativaMinutos })}
                />
                <Dado rotulo="Tempo apontado">
                    {formatarDuracao(apontado)}
                    {estimativa != null && (
                        <span className={apontado > estimativa ? 'text-vermelho' : 'text-suave'}>
                            {' '}
                            de {formatarDuracao(estimativa)}
                        </span>
                    )}
                </Dado>
                <Seguidores alvo="tarefas" id={t.id} usuarioId={perfil.usuarioId} />
                {t.clienteId && (
                    <div className="flex flex-col gap-1">
                        <p className="text-sm font-semibold">No portal</p>
                        <Switch
                            isSelected={t.visivelCliente}
                            isDisabled={!editavel}
                            onChange={(visivelCliente) => salvar({ visivelCliente })}
                            className="group flex items-center gap-2 py-2 text-sm"
                        >
                            <span
                                aria-hidden="true"
                                className="flex h-5 w-9 items-center rounded-full bg-trilho p-0.5 transition group-data-[disabled]:opacity-60 group-data-[focus-visible]:ring-2 group-data-[focus-visible]:ring-acento group-data-[selected]:bg-acento"
                            >
                                <span className="size-4 rounded-full bg-superficie shadow transition group-data-[selected]:translate-x-4" />
                            </span>
                            {t.visivelCliente ? 'O cliente vê esta tarefa' : 'Só a equipe vê'}
                        </Switch>
                    </div>
                )}
            </section>

            <section aria-labelledby="descricao-tarefa" className="flex flex-col gap-1">
                <h3 id="descricao-tarefa" className="text-sm font-semibold">
                    Descrição
                </h3>
                {t.descricao ? (
                    <p className="text-sm whitespace-pre-line">{t.descricao}</p>
                ) : (
                    <p className="text-sm text-suave">Sem descrição.</p>
                )}
            </section>

            <Checklist
                tarefa={t}
                editavel={editavel}
                aoMudar={() => consultas.invalidateQueries({ queryKey: chave })}
            />

            <Comentarios
                tarefaId={t.id}
                fuso={perfil.tenant.fusoHorario}
                usuarioId={perfil.usuarioId}
                podeComentar={podeEditar}
            />

            <JanelaEdicaoTarefa
                tarefa={t}
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

/** Data sem hora num `<input type="date">` (acessível e com o calendário do navegador); salva ao mudar. */
function CampoData({
    rotulo,
    descricao,
    valor,
    editavel,
    aoMudar,
}: {
    rotulo: string
    descricao?: string
    valor: string | null
    editavel: boolean
    aoMudar: (valor: string | null) => void
}) {
    const id = useId()
    if (!editavel)
        return (
            <Dado rotulo={rotulo}>
                {valor ? valor.split('-').reverse().join('/') : <span className="text-suave">—</span>}
            </Dado>
        )
    return (
        <div className="flex flex-col gap-1">
            <label htmlFor={id} className="text-sm font-semibold">
                {rotulo}
            </label>
            <input
                id={id}
                type="date"
                defaultValue={valor ?? ''}
                key={valor ?? ''}
                aria-describedby={descricao ? `${id}-d` : undefined}
                onChange={(e) => {
                    const novo = e.target.value || null
                    if (novo !== valor && (novo === null || /^\d{4}-\d{2}-\d{2}$/.test(novo))) aoMudar(novo)
                }}
                className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta"
            />
            {descricao && (
                <p id={`${id}-d`} className="text-xs text-suave">
                    {descricao}
                </p>
            )}
        </div>
    )
}

/** Duração num campo só ("1h30", "1:30", "90"); mostra formatada e salva ao sair do campo. */
function CampoDuracao({
    rotulo,
    valor,
    editavel,
    aoMudar,
}: {
    rotulo: string
    valor: number | null
    editavel: boolean
    aoMudar: (minutos: number | null) => void
}) {
    const id = useId()
    const [texto, setTexto] = useState(valor != null ? formatarDuracao(valor) : '')
    const [invalido, setInvalido] = useState(false)
    if (!editavel) return <Dado rotulo={rotulo}>{valor != null ? formatarDuracao(valor) : '—'}</Dado>

    function confirmar() {
        if (!texto.trim()) {
            setInvalido(false)
            if (valor !== null) aoMudar(null)
            return
        }
        const minutos = lerDuracao(texto)
        setInvalido(minutos === null)
        if (minutos === null) return
        setTexto(formatarDuracao(minutos))
        if (minutos !== valor) aoMudar(minutos)
    }
    return (
        <div className="flex flex-col gap-1">
            <label htmlFor={id} className="text-sm font-semibold">
                {rotulo}
            </label>
            <input
                id={id}
                value={texto}
                placeholder="1h30"
                onChange={(e) => setTexto(e.target.value)}
                onBlur={confirmar}
                onKeyDown={(e) => e.key === 'Enter' && confirmar()}
                aria-invalid={invalido || undefined}
                aria-describedby={`${id}-d`}
                className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
            />
            <p id={`${id}-d`} className={`text-xs ${invalido ? 'text-vermelho' : 'text-suave'}`}>
                {invalido ? 'Não entendi. Use 1h30, 1:30 ou 90.' : 'Ex.: 1h30, 1:30 ou 90 (minutos).'}
            </p>
        </div>
    )
}

function Checklist({ tarefa: t, editavel, aoMudar }: { tarefa: Tarefa; editavel: boolean; aoMudar: () => void }) {
    const [novo, setNovo] = useState('')
    const adicao = useMutation({
        mutationFn: (texto: string) => adicionarItem(t.id, texto),
        onSuccess: () => {
            setNovo('')
            aoMudar()
        },
        onError: avisarErro,
    })
    const marcacao = useMutation({
        mutationFn: ({ itemId, texto, feito }: { itemId: string; texto: string; feito: boolean }) =>
            editarItem(t.id, itemId, texto, feito),
        onSettled: aoMudar,
        onError: avisarErro,
    })
    const remocao = useMutation({
        mutationFn: (itemId: string) => removerItem(t.id, itemId),
        onSettled: aoMudar,
        onError: avisarErro,
    })
    const feitos = t.checklist.filter((i) => i.feito).length
    if (!editavel && t.checklist.length === 0) return null

    function enviar(evento: FormEvent) {
        evento.preventDefault()
        if (novo.trim()) adicao.mutate(novo.trim())
    }
    return (
        <section aria-labelledby="checklist" className="flex flex-col gap-2">
            <h3 id="checklist" className="flex items-baseline justify-between text-sm font-semibold">
                Checklist
                {t.checklist.length > 0 && (
                    <span className="text-xs font-normal text-suave">
                        {feitos} de {t.checklist.length}
                    </span>
                )}
            </h3>
            {t.checklist.length > 0 && (
                <ul className="flex flex-col gap-1">
                    {t.checklist.map((item) => (
                        <li key={item.id} className="group flex items-center gap-2 text-sm">
                            <Checkbox
                                isSelected={item.feito}
                                isDisabled={!editavel}
                                onChange={(feito) => marcacao.mutate({ itemId: item.id, texto: item.texto, feito })}
                                className="group/c flex flex-1 items-center gap-2 py-1"
                            >
                                <span
                                    aria-hidden="true"
                                    className="grid size-4 shrink-0 place-items-center rounded border border-linha bg-superficie group-data-[selected]/c:border-acento group-data-[selected]/c:bg-acento group-data-[focus-visible]/c:ring-2 group-data-[focus-visible]/c:ring-acento"
                                >
                                    <svg
                                        viewBox="0 0 12 12"
                                        className="size-3 text-botao-tinta opacity-0 group-data-[selected]/c:opacity-100"
                                    >
                                        <path
                                            d="M2.5 6.5 5 9l4.5-6"
                                            fill="none"
                                            stroke="currentColor"
                                            strokeWidth="2"
                                        />
                                    </svg>
                                </span>
                                <span className={item.feito ? 'text-suave line-through' : ''}>{item.texto}</span>
                            </Checkbox>
                            {editavel && (
                                <Button
                                    aria-label={`Tirar "${item.texto}"`}
                                    onPress={() => remocao.mutate(item.id)}
                                    className="grid size-6 place-items-center rounded-couro text-suave opacity-60 hover:bg-trilho hover:text-tinta hover:opacity-100 focus-visible:opacity-100"
                                >
                                    <Icone nome="fechar" className="size-3" />
                                </Button>
                            )}
                        </li>
                    ))}
                </ul>
            )}
            {editavel && (
                <form onSubmit={enviar} className="flex gap-2">
                    <label htmlFor="novo-item" className="sr-only">
                        Novo item da checklist
                    </label>
                    <input
                        id="novo-item"
                        value={novo}
                        maxLength={500}
                        onChange={(e) => setNovo(e.target.value)}
                        placeholder="Adicionar item"
                        className="min-w-0 flex-1 rounded-couro border border-linha bg-superficie px-3 py-1.5 text-sm text-tinta placeholder:text-suave"
                    />
                    <Botao type="submit" variante="secundario" isPending={adicao.isPending} isDisabled={!novo.trim()}>
                        Adicionar
                    </Botao>
                </form>
            )}
        </section>
    )
}

function AcoesStatus({
    tarefa: t,
    aoMudar,
    aoFalhar,
}: {
    tarefa: Tarefa
    aoMudar: (nova: Tarefa) => void
    aoFalhar: (erro: unknown) => void
}) {
    const [cancelando, setCancelando] = useState(false)
    const transicao = useMutation({
        mutationFn: (acao: AcaoTarefa) => transicionarTarefa(t, acao),
        onSuccess: (nova) => {
            aoMudar(nova)
            setCancelando(false)
            avisar(`Tarefa ${STATUS_TAREFA[nova.status].toLowerCase()}.`)
        },
    })
    const acoes = TRANSICOES_TAREFA[t.status]
    if (acoes.length === 0) return null
    const [principal, ...demais] = acoes.filter(
        (a): a is Exclude<AcaoStatusTarefa, 'cancelamento'> => a !== 'cancelamento',
    )
    const executar = (acao: AcaoTarefa) => transicao.mutate(acao, { onError: aoFalhar })

    return (
        <div className="flex flex-wrap items-center gap-2">
            {principal && (
                <Botao
                    onPress={() => executar(principal)}
                    isPending={transicao.isPending && transicao.variables === principal}
                >
                    {ROTULO_ACAO_TAREFA[principal]}
                </Botao>
            )}
            {demais.map((acao) => (
                <Botao
                    key={acao}
                    variante="secundario"
                    onPress={() => executar(acao)}
                    isPending={transicao.isPending && transicao.variables === acao}
                >
                    {ROTULO_ACAO_TAREFA[acao]}
                </Botao>
            ))}
            {acoes.includes('cancelamento') && (
                <Button
                    onPress={() => {
                        transicao.reset()
                        setCancelando(true)
                    }}
                    className="ml-auto rounded-couro px-2 py-1 text-sm text-suave hover:text-vermelho hover:underline"
                >
                    {ROTULO_ACAO_TAREFA.cancelamento}
                </Button>
            )}
            <Confirmacao
                titulo="Cancelar esta tarefa?"
                acao="Cancelar tarefa"
                aberta={cancelando}
                aoMudar={setCancelando}
                aoConfirmar={() => transicao.mutate('cancelamento')}
                ocupada={transicao.isPending}
                erro={transicao.isError ? mensagemDeErro(transicao.error) : null}
            >
                <p>Cancelar é definitivo: a tarefa sai da lista e do quadro e não pode ser reaberta.</p>
                <p>As horas já apontadas nela continuam valendo.</p>
            </Confirmacao>
        </div>
    )
}

function JanelaEdicaoTarefa({
    tarefa,
    aberta,
    aoMudar,
    aoSalvar,
}: {
    tarefa: Tarefa
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    aoSalvar: (nova: Tarefa) => void
}) {
    const envio = useMutation({
        mutationFn: (dados: { titulo: string; descricao: string | null }) => editarTarefa(tarefa, dados),
        onSuccess: aoSalvar,
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null
    return (
        <Janela
            titulo="Editar tarefa"
            aberta={aberta}
            aoMudar={(a) => {
                if (!a) envio.reset()
                aoMudar(a)
            }}
            ocupada={envio.isPending}
            larga
        >
            <Form
                className="flex flex-col gap-4"
                validationErrors={errosDeCampo(envio.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    const dados = new FormData(evento.currentTarget)
                    envio.mutate({
                        titulo: String(dados.get('titulo')).trim(),
                        descricao: opcional(dados.get('descricao')),
                    })
                }}
            >
                <LegendaObrigatorios />
                {mensagem && <Alerta>{mensagem}</Alerta>}
                <CampoTexto
                    name="titulo"
                    rotulo="Título"
                    defaultValue={tarefa.titulo}
                    maxLength={200}
                    isRequired
                    autoFocus
                />
                <CampoTexto
                    name="descricao"
                    rotulo="Descrição"
                    defaultValue={tarefa.descricao ?? ''}
                    maxLength={10_000}
                    multilinha
                />
                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={() => aoMudar(false)} isDisabled={envio.isPending}>
                        Voltar
                    </Botao>
                    <Botao type="submit" isPending={envio.isPending}>
                        Salvar
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}
