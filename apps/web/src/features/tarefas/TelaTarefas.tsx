import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { Input, Radio, RadioGroup, SearchField, Tab, TabList, Tabs } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { dataCurta, hojeNoFuso } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import { useAtrasado } from '@/shared/hooks/useAtrasado'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Icone } from '@/shared/ui/Icone'
import { Pagina } from '@/shared/ui/Pagina'
import { Paginacao } from '@/shared/ui/Paginacao'
import {
    ABAS,
    consultaListaTarefas,
    TAMANHO_PAGINA,
    type Aba,
    type FiltrosTarefas,
    type TarefaResumo,
} from './consultas'
import { JanelaNovaTarefa } from './JanelaNovaTarefa'
import { Quadro } from './Quadro'
import { ESTILO_STATUS_TAREFA, STATUS_TAREFA } from './rotulos'

/** Tarefas (F3a): lista com abas ou quadro, filtros no endereço; o detalhe em painel vem no F3b. */
export function TelaTarefas({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosTarefas }) {
    const navegar = useNavigate()
    const fuso = perfil.tenant.fusoHorario
    const [texto, setTexto] = useState(filtros.busca ?? '')
    const busca = useAtrasado(texto.trim())
    const quadro = filtros.modo === 'quadro'
    const lista = useQuery({ ...consultaListaTarefas(filtros), enabled: !quadro })
    const equipe = useQuery(consultaEquipe)

    const filtrar = (mudanca: Partial<FiltrosTarefas>) =>
        void navegar({ to: '.', search: (atual: FiltrosTarefas) => ({ ...atual, ...mudanca, pagina: undefined }) })

    useEffect(() => {
        if (busca === (filtros.busca ?? '')) return
        void navegar({ to: '.', search: { ...filtros, busca: busca || undefined, pagina: undefined }, replace: true })
    }, [busca, filtros, navegar])

    // A janela abre pelo endereço (`?nova=1`), como a de nova solicitação (convenções §4).
    const mudarNova = (aberta: boolean) =>
        void navegar({
            to: '.',
            search: (atual: FiltrosTarefas) => ({ ...atual, nova: aberta || undefined }),
            replace: !aberta,
        })

    const algumFiltro = Boolean(filtros.busca || filtros.cliente || filtros.responsavel)
    const pessoas = [
        { id: 'eu', rotulo: 'Comigo' },
        { id: 'ninguem', rotulo: 'Sem responsável' },
        ...(equipe.data ?? []).filter((p) => p.id !== perfil.usuarioId).map((p) => ({ id: p.id, rotulo: p.nome })),
    ]

    return (
        <Pagina
            ajuda="tarefas"
            titulo="Tarefas"
            acoes={
                <div className="flex flex-wrap items-center gap-3">
                    {pode(perfil, 'tarefa.criar') && (
                        <Botao onPress={() => mudarNova(true)}>
                            <Icone nome="mais" />
                            Nova tarefa
                        </Botao>
                    )}
                    <RadioGroup
                        aria-label="Ver como"
                        orientation="horizontal"
                        value={quadro ? 'quadro' : 'lista'}
                        onChange={(v) =>
                            void navegar({
                                to: '.',
                                search: (atual: FiltrosTarefas) => ({
                                    ...atual,
                                    modo: v === 'quadro' ? ('quadro' as const) : undefined,
                                    pagina: undefined,
                                }),
                            })
                        }
                        className="flex gap-1 rounded-couro bg-trilho p-1"
                    >
                        {(['lista', 'quadro'] as const).map((v) => (
                            <Radio
                                key={v}
                                value={v}
                                className="cursor-default rounded-couro px-3 py-1 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                            >
                                {v === 'lista' ? 'Lista' : 'Quadro'}
                            </Radio>
                        ))}
                    </RadioGroup>
                </div>
            }
        >
            {!quadro && (
                <Tabs
                    selectedKey={filtros.aba ?? 'abertas'}
                    onSelectionChange={(v) => filtrar({ aba: v === 'abertas' ? undefined : (v as Aba) })}
                >
                    <TabList aria-label="Situação" className="flex w-fit flex-wrap gap-1 rounded-couro bg-trilho p-1">
                        {(Object.keys(ABAS) as Aba[]).map((aba) => (
                            <Tab
                                key={aba}
                                id={aba}
                                className="cursor-default rounded-couro px-3 py-1.5 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                            >
                                {ABAS[aba].rotulo}
                            </Tab>
                        ))}
                    </TabList>
                </Tabs>
            )}

            <div className="grid grid-cols-2 gap-3 sm:flex sm:flex-wrap sm:items-end">
                <SearchField
                    aria-label="Buscar tarefas"
                    value={texto}
                    onChange={setTexto}
                    className="col-span-2 flex min-w-64 flex-1 items-center gap-2 rounded-couro border border-linha bg-superficie px-3"
                >
                    <Icone nome="busca" className="size-4 text-suave" />
                    <Input
                        placeholder="Buscar pelo título"
                        className="w-full bg-transparent py-2 outline-none placeholder:text-suave [&::-webkit-search-cancel-button]:hidden"
                    />
                </SearchField>
                {pode(perfil, 'cliente.ver') && (
                    <CampoCliente
                        rotulo="Cliente"
                        rotuloOculto
                        valor={filtros.cliente ?? null}
                        aoMudar={(v) => filtrar({ cliente: v ?? undefined })}
                        vazio="Todos os clientes"
                        dica="Cliente"
                        className="sm:w-52"
                    />
                )}
                <CampoSelecao
                    rotulo="Responsável"
                    rotuloOculto
                    opcoes={pessoas}
                    valor={filtros.responsavel ?? null}
                    aoMudar={(v) => filtrar({ responsavel: v ?? undefined })}
                    vazio="Qualquer responsável"
                    dica="Responsável"
                    className="sm:w-48"
                />
                {algumFiltro && (
                    <Botao
                        variante="discreto"
                        onPress={() => {
                            setTexto('')
                            void navegar({ to: '.', search: { aba: filtros.aba, modo: filtros.modo } })
                        }}
                    >
                        Limpar filtros
                    </Botao>
                )}
            </div>

            {quadro ? (
                <Quadro filtros={filtros} hoje={hojeNoFuso(fuso)} podeEditar={pode(perfil, 'tarefa.editar')} />
            ) : lista.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : lista.isError ? (
                <Alerta>{mensagemDeErro(lista.error)}</Alerta>
            ) : lista.data.itens.length === 0 ? (
                algumFiltro ? (
                    <EstadoVazio titulo="Nenhuma tarefa com esses filtros">
                        Confira a busca ou limpe os filtros.
                    </EstadoVazio>
                ) : (filtros.aba ?? 'abertas') === 'abertas' ? (
                    <EstadoVazio titulo="Nada em aberto">
                        As tarefas criadas nas solicitações, nos projetos e nas recorrências aparecem aqui.
                    </EstadoVazio>
                ) : (
                    <EstadoVazio titulo="Nenhuma tarefa aqui" />
                )
            ) : (
                <Tabela itens={lista.data.itens} hoje={hojeNoFuso(fuso)} />
            )}

            <JanelaNovaTarefa
                perfil={perfil}
                aberta={Boolean(filtros.nova) && pode(perfil, 'tarefa.criar')}
                aoMudar={mudarNova}
                aoCriar={(t) =>
                    void navegar({
                        to: '/tarefas/$id',
                        params: { id: t.id },
                        search: (atual: FiltrosTarefas) => ({ ...atual, nova: undefined }),
                    })
                }
            />

            {!quadro && lista.data && (
                <Paginacao
                    pagina={filtros.pagina ?? 1}
                    tamanhoPagina={TAMANHO_PAGINA}
                    total={Number(lista.data.total)}
                />
            )}
        </Pagina>
    )
}

/** De onde a tarefa vem: cliente e solicitação ou projeto. */
export function OrigemTarefa({ tarefa: t }: { tarefa: TarefaResumo }) {
    const partes = [
        t.clienteNome,
        t.solicitacaoNumero != null ? `#${t.solicitacaoNumero}` : null,
        t.projetoNome,
    ].filter(Boolean)
    return <span className="text-xs text-suave">{partes.length ? partes.join(' · ') : 'Interna'}</span>
}

export function Prazo({ tarefa: t, hoje }: { tarefa: TarefaResumo; hoje: string }) {
    if (!t.prazo) return <span className="text-suave">—</span>
    const aberta = t.status === 'A_FAZER' || t.status === 'FAZENDO'
    const atrasada = aberta && t.prazo < hoje
    return (
        <span className={atrasada ? 'text-vermelho' : t.prazo === hoje && aberta ? 'text-ambar-texto' : ''}>
            {t.prazo === hoje ? 'hoje' : dataCurta(t.prazo, hoje)}
            {atrasada && <span className="sr-only"> (atrasada)</span>}
        </span>
    )
}

function Tabela({ itens, hoje }: { itens: TarefaResumo[]; hoje: string }) {
    const aberta = useParams({ strict: false }).id
    return (
        <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
            <table className="w-full text-sm">
                <thead className="border-b border-linha text-left text-xs text-suave">
                    <tr>
                        <th className="px-4 py-2.5 font-semibold">Tarefa</th>
                        <th className="hidden px-4 py-2.5 font-semibold md:table-cell">Status</th>
                        <th className="hidden px-4 py-2.5 font-semibold lg:table-cell">Responsável</th>
                        <th className="px-4 py-2.5 font-semibold">Prazo</th>
                        <th className="hidden px-4 py-2.5 font-semibold xl:table-cell">Planejada</th>
                        <th className="hidden px-4 py-2.5 font-semibold xl:table-cell">Estimativa</th>
                    </tr>
                </thead>
                <tbody>
                    {itens.map((t) => (
                        <tr
                            key={t.id}
                            aria-current={t.id === aberta ? 'true' : undefined}
                            className="border-b border-linha last:border-0 hover:bg-trilho/50 aria-[current]:bg-acento-suave/60"
                        >
                            <td className="px-4 py-2.5">
                                <Link
                                    to="/tarefas/$id"
                                    params={{ id: t.id }}
                                    search={(atual: FiltrosTarefas) => atual}
                                    data-tarefa={t.id}
                                    className="block font-semibold hover:underline"
                                >
                                    {t.titulo}
                                </Link>
                                <OrigemTarefa tarefa={t} />
                            </td>
                            <td className="hidden px-4 py-2.5 md:table-cell">
                                <span
                                    className={`rounded-couro px-2 py-0.5 text-xs whitespace-nowrap ${ESTILO_STATUS_TAREFA[t.status]}`}
                                >
                                    {STATUS_TAREFA[t.status]}
                                </span>
                            </td>
                            <td className="hidden px-4 py-2.5 text-suave lg:table-cell">
                                {t.responsavel?.nome ?? 'Sem responsável'}
                            </td>
                            <td className="px-4 py-2.5 whitespace-nowrap">
                                <Prazo tarefa={t} hoje={hoje} />
                            </td>
                            <td className="hidden px-4 py-2.5 text-suave xl:table-cell">
                                {t.planejadaPara ? dataCurta(t.planejadaPara, hoje) : '—'}
                            </td>
                            <td className="hidden px-4 py-2.5 text-suave xl:table-cell">
                                {t.estimativaMinutos != null ? formatarDuracao(Number(t.estimativaMinutos)) : '—'}
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    )
}
