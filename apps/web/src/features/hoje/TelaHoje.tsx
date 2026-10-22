import { useQuery } from '@tanstack/react-query'
import { Link, useRouter } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import { consultaApontamentos } from '@/features/apontamentos/consultas'
import { caminhoDoLink, consultaSino } from '@/features/casca/Sino'
import { pode } from '@/features/casca/navegacao'
import { AvisoConfirmacao } from '@/features/sessao/AvisoConfirmacao'
import { SITUACAO_SLA, STATUS } from '@/features/solicitacoes/rotulos'
import { ESTILO_STATUS_TAREFA, STATUS_TAREFA } from '@/features/tarefas/rotulos'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { dataCurta, diaPorExtenso as diaCurto, hojeNoFuso, segundaDaSemana } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import { prazoRelativo } from '@/shared/format/prazo'
import { quando } from '@/shared/format/quando'
import { diaPorExtenso, saudacao } from '@/shared/format/saudacao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Pagina } from '@/shared/ui/Pagina'
import { consultaMinhaSemana, consultaMinhasSolicitacoes, consultaMinhasTarefas } from './consultas'

/**
 * Hoje (F6a, protótipo): o início pessoal. O que está na mão (solicitações pelo prazo de SLA e as tarefas do dia ou
 * atrasadas), o que espera o cliente, a carga da semana, o que já foi apontado hoje e os avisos recentes. Cada bloco
 * só aparece com a permissão de leitura dele.
 */
export function TelaHoje({ perfil }: { perfil: Perfil }) {
    const fuso = perfil.tenant.fusoHorario
    const primeiroNome = perfil.nome.split(' ')[0]
    const verSolicitacoes = pode(perfil, 'solicitacao.ver')
    const verTarefas = pode(perfil, 'tarefa.ver')
    return (
        <Pagina titulo={`${saudacao(fuso)}, ${primeiroNome}`} contexto={diaPorExtenso(fuso)}>
            <AvisoConfirmacao perfil={perfil} />
            <div className="grid gap-6 lg:grid-cols-3">
                <div className="flex flex-col gap-6 lg:col-span-2">
                    {verSolicitacoes && <SolicitacoesNaMao perfil={perfil} />}
                    {verTarefas && <TarefasDoDia fuso={fuso} />}
                    {verSolicitacoes && <EsperandoCliente perfil={perfil} />}
                </div>
                <div className="flex flex-col gap-6">
                    {verTarefas && <MinhaSemana perfil={perfil} />}
                    <ApontadoHoje perfil={perfil} />
                    <Novidades fuso={fuso} />
                </div>
            </div>
        </Pagina>
    )
}

function Bloco({
    id,
    titulo,
    contagem,
    link,
    children,
}: {
    id: string
    titulo: string
    contagem?: number
    link?: ReactNode
    children: ReactNode
}) {
    return (
        <section aria-labelledby={id} className="flex flex-col gap-2">
            <div className="flex items-baseline justify-between gap-3">
                <h2 id={id} className="font-titulo text-xl font-semibold">
                    {titulo}
                    {contagem !== undefined && contagem > 0 && (
                        <span className="ml-2 font-sans text-sm font-normal text-suave tabular-nums">{contagem}</span>
                    )}
                </h2>
                {link && <span className="text-sm">{link}</span>}
            </div>
            {children}
        </section>
    )
}

function Estado({
    consulta,
    vazio,
}: {
    consulta: { isPending: boolean; isError: boolean; error: unknown }
    vazio: string
}) {
    if (consulta.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (consulta.isError)
        return <p className="text-sm text-vermelho">{mensagemDeErro(consulta.error) ?? 'Não deu para carregar.'}</p>
    return <p className="rounded-couro border border-dashed border-linha px-4 py-3 text-sm text-suave">{vazio}</p>
}

const lista = 'divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm'

function SolicitacoesNaMao({ perfil }: { perfil: Perfil }) {
    const consulta = useQuery(consultaMinhasSolicitacoes(perfil.usuarioId, 'ABERTA,EM_ANDAMENTO'))
    const itens = consulta.data?.itens ?? []
    return (
        <Bloco
            id="hoje-solicitacoes"
            titulo="Solicitações com você"
            contagem={Number(consulta.data?.total ?? 0)}
            link={
                <Link to="/solicitacoes" search={{ responsavel: 'eu' }} className="text-acento-texto hover:underline">
                    Ver na fila
                </Link>
            }
        >
            {itens.length === 0 ? (
                <Estado
                    consulta={consulta}
                    vazio="Nenhuma solicitação aberta com você. Bom momento para olhar a fila."
                />
            ) : (
                <ul className={lista}>
                    {itens.map((s) => {
                        const sla = SITUACAO_SLA[s.situacaoSla]
                        return (
                            <li key={s.id} className="flex items-center gap-3 px-4 py-2.5">
                                <span
                                    aria-hidden="true"
                                    className={`size-2 shrink-0 rounded-full ${sla?.estilo ?? 'bg-linha'}`}
                                />
                                <Link
                                    to="/solicitacoes/$id"
                                    params={{ id: s.id }}
                                    className="min-w-0 flex-1 hover:underline"
                                >
                                    <span className="text-suave tabular-nums">#{s.numero}</span> {s.titulo}
                                    <span className="block truncate text-xs text-suave">{s.cliente.nome}</span>
                                </Link>
                                <span className="text-right text-xs whitespace-nowrap text-suave">
                                    {s.prazoResolucao
                                        ? prazoRelativo(s.prazoResolucao, perfil.tenant.fusoHorario)
                                        : 'sem SLA'}
                                    {sla && <span className="sr-only"> ({sla.rotulo})</span>}
                                </span>
                            </li>
                        )
                    })}
                </ul>
            )}
        </Bloco>
    )
}

function TarefasDoDia({ fuso }: { fuso: string }) {
    const hoje = hojeNoFuso(fuso)
    const consulta = useQuery(consultaMinhasTarefas)
    // Do dia: em andamento, planejadas até hoje ou com prazo até hoje (as atrasadas primeiro).
    const doDia = (consulta.data?.itens ?? [])
        .filter(
            (t) =>
                t.status === 'FAZENDO' ||
                (t.planejadaPara !== null && t.planejadaPara <= hoje) ||
                (t.prazo !== null && t.prazo <= hoje),
        )
        .sort((a, b) => (a.prazo ?? '9999').localeCompare(b.prazo ?? '9999'))
    const depois = (consulta.data?.itens.length ?? 0) - doDia.length
    return (
        <Bloco
            id="hoje-tarefas"
            titulo="Tarefas de hoje"
            contagem={doDia.length}
            link={
                <Link to="/tarefas" search={{ responsavel: 'eu' }} className="text-acento-texto hover:underline">
                    {depois > 0 ? `Mais ${depois} adiante` : 'Ver tarefas'}
                </Link>
            }
        >
            {doDia.length === 0 ? (
                <Estado consulta={consulta} vazio="Nada planejado para hoje." />
            ) : (
                <ul className={lista}>
                    {doDia.map((t) => {
                        const atrasada = t.prazo !== null && t.prazo < hoje
                        return (
                            <li key={t.id} className="flex items-center gap-3 px-4 py-2.5">
                                <Link
                                    to="/tarefas/$id"
                                    params={{ id: t.id }}
                                    className="min-w-0 flex-1 hover:underline"
                                >
                                    {t.titulo}
                                    {t.clienteNome && (
                                        <span className="block truncate text-xs text-suave">{t.clienteNome}</span>
                                    )}
                                </Link>
                                {t.prazo && (
                                    <span
                                        className={`text-xs whitespace-nowrap ${atrasada ? 'text-vermelho' : 'text-suave'}`}
                                    >
                                        {atrasada
                                            ? `atrasada desde ${dataCurta(t.prazo, hoje)}`
                                            : `prazo ${dataCurta(t.prazo, hoje)}`}
                                    </span>
                                )}
                                <span className={`rounded-couro px-2 py-0.5 text-xs ${ESTILO_STATUS_TAREFA[t.status]}`}>
                                    {STATUS_TAREFA[t.status]}
                                </span>
                            </li>
                        )
                    })}
                </ul>
            )}
        </Bloco>
    )
}

function EsperandoCliente({ perfil }: { perfil: Perfil }) {
    const consulta = useQuery(consultaMinhasSolicitacoes(perfil.usuarioId, 'AGUARDANDO_CLIENTE,AGUARDANDO_TERCEIRO'))
    const itens = consulta.data?.itens ?? []
    return (
        <Bloco id="hoje-esperando" titulo="Esperando resposta" contagem={Number(consulta.data?.total ?? 0)}>
            {itens.length === 0 ? (
                <Estado consulta={consulta} vazio="Nada parado esperando o cliente ou terceiros." />
            ) : (
                <ul className={lista}>
                    {itens.map((s) => (
                        <li key={s.id} className="flex items-center gap-3 px-4 py-2.5">
                            <Link
                                to="/solicitacoes/$id"
                                params={{ id: s.id }}
                                className="min-w-0 flex-1 hover:underline"
                            >
                                <span className="text-suave tabular-nums">#{s.numero}</span> {s.titulo}
                                <span className="block truncate text-xs text-suave">{s.cliente.nome}</span>
                            </Link>
                            <span className="text-xs text-suave">{STATUS[s.status]}</span>
                        </li>
                    ))}
                </ul>
            )}
        </Bloco>
    )
}

function MinhaSemana({ perfil }: { perfil: Perfil }) {
    const hoje = hojeNoFuso(perfil.tenant.fusoHorario)
    const consulta = useQuery(consultaMinhaSemana(segundaDaSemana(hoje)))
    const eu = consulta.data?.pessoas.find((p) => p.pessoa.id === perfil.usuarioId)
    const maximo = Math.max(1, ...(eu?.carga.porDia.map((d) => Number(d.planejadoMinutos)) ?? [0]))
    return (
        <Bloco id="hoje-semana" titulo="Sua semana">
            {!eu ? (
                <Estado consulta={consulta} vazio="Sem carga planejada nesta semana." />
            ) : (
                <div className="flex flex-col gap-3 rounded-couro border border-linha bg-superficie p-4 text-sm">
                    <p>
                        <span className="font-semibold tabular-nums">
                            {formatarDuracao(Number(eu.carga.planejadoMinutos))}
                        </span>{' '}
                        planejadas de {formatarDuracao(Number(eu.capacidadeMinutos))}
                        {eu.percentual != null && (
                            <span className={Number(eu.percentual) > 100 ? 'text-vermelho' : 'text-suave'}>
                                {' '}
                                · {Number(eu.percentual)}%
                            </span>
                        )}
                    </p>
                    <ol aria-label="Planejado por dia" className="flex h-16 items-end gap-1">
                        {eu.carga.porDia.map((d) => {
                            const minutos = Number(d.planejadoMinutos)
                            return (
                                <li key={d.data} className="flex flex-1 flex-col items-center gap-1">
                                    <span
                                        aria-hidden="true"
                                        className={`w-full rounded-t ${d.data === hoje ? 'bg-acento' : 'bg-acento/40'}`}
                                        style={{ height: `${Math.max(2, (minutos / maximo) * 48)}px` }}
                                    />
                                    <span className="text-[10px] text-suave">
                                        {diaCurto(d.data).slice(0, 3)}
                                        <span className="sr-only">: {formatarDuracao(minutos)}</span>
                                    </span>
                                </li>
                            )
                        })}
                    </ol>
                    {Number(eu.carga.tarefasSemEstimativa) > 0 && (
                        <p className="text-xs text-suave">
                            {Number(eu.carga.tarefasSemEstimativa)} tarefa(s) sem estimativa não entram na conta.
                        </p>
                    )}
                </div>
            )}
        </Bloco>
    )
}

function ApontadoHoje({ perfil }: { perfil: Perfil }) {
    const hoje = hojeNoFuso(perfil.tenant.fusoHorario)
    const consulta = useQuery(consultaApontamentos({ usuarioId: perfil.usuarioId, de: hoje, ate: hoje }))
    const itens = consulta.data?.itens ?? []
    const total = itens.reduce((soma, a) => soma + Number(a.duracaoMinutos), 0)
    return (
        <Bloco
            id="hoje-horas"
            titulo="Apontado hoje"
            link={
                <Link to="/horas" className="text-acento-texto hover:underline">
                    Ver a semana
                </Link>
            }
        >
            {itens.length === 0 ? (
                <Estado
                    consulta={consulta}
                    vazio="Nenhuma hora apontada hoje. O cronômetro da tarefa aponta ao parar."
                />
            ) : (
                <div className="rounded-couro border border-linha bg-superficie text-sm">
                    <p className="border-b border-linha px-4 py-2.5">
                        <span className="font-semibold tabular-nums">{formatarDuracao(total)}</span> em {itens.length}{' '}
                        {itens.length === 1 ? 'apontamento' : 'apontamentos'}
                    </p>
                    <ul className="divide-y divide-linha">
                        {itens.map((a) => (
                            <li key={a.id} className="flex items-baseline justify-between gap-3 px-4 py-2">
                                <Link
                                    to="/tarefas/$id"
                                    params={{ id: a.tarefa.id }}
                                    className="min-w-0 truncate hover:underline"
                                >
                                    {a.tarefa.nome}
                                </Link>
                                <span className="text-xs text-suave tabular-nums">
                                    {formatarDuracao(Number(a.duracaoMinutos))}
                                </span>
                            </li>
                        ))}
                    </ul>
                </div>
            )}
        </Bloco>
    )
}

function Novidades({ fuso }: { fuso: string }) {
    const roteador = useRouter()
    const consulta = useQuery(consultaSino)
    const itens = (consulta.data?.itens ?? []).slice(0, 5)
    return (
        <Bloco id="hoje-novidades" titulo="Novidades">
            {itens.length === 0 ? (
                <Estado consulta={consulta} vazio="Nenhum aviso recente." />
            ) : (
                <ul className={lista}>
                    {itens.map((aviso) => {
                        const caminho = caminhoDoLink(aviso.link)
                        return (
                            <li key={aviso.id} className="px-4 py-2.5">
                                {caminho ? (
                                    <a
                                        href={caminho}
                                        onClick={(e) => {
                                            e.preventDefault()
                                            void roteador.navigate({ href: caminho })
                                        }}
                                        className={`hover:underline ${aviso.lidaEm ? 'text-suave' : 'font-semibold'}`}
                                    >
                                        {aviso.titulo}
                                    </a>
                                ) : (
                                    <span className={aviso.lidaEm ? 'text-suave' : 'font-semibold'}>
                                        {aviso.titulo}
                                    </span>
                                )}
                                <span className="block text-xs text-suave">{quando(aviso.criadaEm, fuso)}</span>
                            </li>
                        )
                    })}
                </ul>
            )}
        </Bloco>
    )
}
