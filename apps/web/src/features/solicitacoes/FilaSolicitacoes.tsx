import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { Input, SearchField, Tab, TabList, Tabs } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { prazoRelativo, dataHora } from '@/shared/format/prazo'
import { quando } from '@/shared/format/quando'
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
    consultaCategorias,
    consultaFila,
    ORDENS,
    TAMANHO_PAGINA,
    VISOES,
    type FiltrosFila,
    type Ordem,
    type SolicitacaoResumo,
    type Visao,
} from './consultas'
import { JanelaNovaSolicitacao } from './JanelaNovaSolicitacao'
import { ESTILO_PRIORIDADE, ESTILO_STATUS, PRIORIDADE, SITUACAO_SLA, STATUS, type Prioridade } from './rotulos'

/** Fila de solicitações (F2b): visões, filtros e ordem no endereço; o detalhe abre em painel por cima (F2d). */
export function FilaSolicitacoes({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosFila }) {
    const navegar = useNavigate()
    const aberta = useParams({ strict: false }).id
    const fuso = perfil.tenant.fusoHorario
    const [texto, setTexto] = useState(filtros.busca ?? '')
    const busca = useAtrasado(texto.trim())
    const fila = useQuery(consultaFila(filtros, perfil.usuarioId))
    const equipe = useQuery(consultaEquipe)
    const categorias = useQuery(consultaCategorias)

    // Pelo endereço atual (e não pelo `filtros` da renderização): duas mudanças seguidas não se atropelam.
    // `to: '.'`: com o painel aberto, filtrar não fecha a solicitação.
    const filtrar = (mudanca: Partial<FiltrosFila>) =>
        void navegar({ to: '.', search: (atual: FiltrosFila) => ({ ...atual, ...mudanca, pagina: undefined }) })

    useEffect(() => {
        if (busca === (filtros.busca ?? '')) return
        void navegar({
            to: '.',
            search: { ...filtros, busca: busca || undefined, pagina: undefined },
            replace: true,
        })
    }, [busca, filtros, navegar])

    // A janela abre pelo endereço (`?nova=1`): o Ctrl+K e outras telas chegam aqui com ela aberta.
    const mudarNova = (aberta: boolean) =>
        void navegar({
            to: '.',
            search: (atual: FiltrosFila) => ({ ...atual, nova: aberta || undefined }),
            replace: !aberta,
        })

    const algumFiltro = Boolean(
        filtros.busca ||
        filtros.cliente ||
        filtros.responsavel ||
        filtros.prioridade ||
        filtros.categoria ||
        filtros.sla,
    )
    const pessoas = [
        { id: 'eu', rotulo: 'Comigo' },
        { id: 'ninguem', rotulo: 'Sem responsável' },
        ...(equipe.data ?? []).filter((p) => p.id !== perfil.usuarioId).map((p) => ({ id: p.id, rotulo: p.nome })),
    ]

    return (
        <Pagina
            ajuda="solicitacoes"
            titulo="Solicitações"
            acoes={
                pode(perfil, 'solicitacao.criar') && (
                    <Botao grava onPress={() => mudarNova(true)}>
                        <Icone nome="mais" />
                        Nova solicitação
                    </Botao>
                )
            }
        >
            <Tabs
                selectedKey={filtros.visao ?? 'abertas'}
                onSelectionChange={(v) => filtrar({ visao: v === 'abertas' ? undefined : (v as Visao) })}
            >
                <TabList aria-label="Visões" className="flex w-fit flex-wrap gap-1 rounded-couro bg-trilho p-1">
                    {(Object.keys(VISOES) as Visao[]).map((visao) => (
                        <Tab
                            key={visao}
                            id={visao}
                            className="cursor-default rounded-couro px-3 py-1.5 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {VISOES[visao].rotulo}
                        </Tab>
                    ))}
                </TabList>
            </Tabs>

            <div className="grid grid-cols-2 gap-3 sm:flex sm:flex-wrap sm:items-end">
                <SearchField
                    aria-label="Buscar solicitações"
                    value={texto}
                    onChange={setTexto}
                    className="col-span-2 flex min-w-64 flex-1 items-center gap-2 rounded-couro border border-linha bg-superficie px-3"
                >
                    <Icone nome="busca" className="size-4 text-suave" />
                    <Input
                        placeholder="Buscar por título ou número (#42)"
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
                <CampoSelecao
                    rotulo="Prioridade"
                    rotuloOculto
                    opcoes={(Object.keys(PRIORIDADE) as Prioridade[])
                        .reverse()
                        .map((p) => ({ id: p, rotulo: PRIORIDADE[p] }))}
                    valor={filtros.prioridade ?? null}
                    aoMudar={(v) => filtrar({ prioridade: (v as Prioridade | null) ?? undefined })}
                    vazio="Qualquer prioridade"
                    dica="Prioridade"
                    className="sm:w-44"
                />
                {(categorias.data?.length ?? 0) > 0 && (
                    <CampoSelecao
                        rotulo="Categoria"
                        rotuloOculto
                        opcoes={(categorias.data ?? []).map((c) => ({ id: c.id, rotulo: c.nome }))}
                        valor={filtros.categoria ?? null}
                        aoMudar={(v) => filtrar({ categoria: v ?? undefined })}
                        vazio="Qualquer categoria"
                        dica="Categoria"
                        className="sm:w-44"
                    />
                )}
                <CampoSelecao
                    rotulo="Prazo"
                    rotuloOculto
                    opcoes={[
                        { id: 'EM_RISCO', rotulo: 'Em risco' },
                        { id: 'VIOLADO', rotulo: 'Violado' },
                    ]}
                    valor={filtros.sla ?? null}
                    aoMudar={(v) => filtrar({ sla: (v as FiltrosFila['sla']) ?? undefined })}
                    vazio="Qualquer prazo"
                    dica="Prazo"
                    className="sm:w-40"
                />
                <CampoSelecao
                    rotulo="Ordem"
                    rotuloOculto
                    opcoes={(Object.keys(ORDENS) as Ordem[]).map((o) => ({ id: o, rotulo: ORDENS[o] }))}
                    valor={filtros.ordem ?? '-abertaEm'}
                    aoMudar={(v) => filtrar({ ordem: !v || v === '-abertaEm' ? undefined : (v as Ordem) })}
                    className="sm:w-44"
                />
                {algumFiltro && (
                    <Botao
                        variante="discreto"
                        onPress={() => {
                            setTexto('')
                            void navegar({
                                to: '.',
                                search: { visao: filtros.visao, ordem: filtros.ordem },
                            })
                        }}
                    >
                        Limpar filtros
                    </Botao>
                )}
            </div>

            {fila.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : fila.isError ? (
                <Alerta>{mensagemDeErro(fila.error)}</Alerta>
            ) : fila.data.itens.length === 0 ? (
                algumFiltro ? (
                    <EstadoVazio titulo="Nenhuma solicitação com esses filtros">
                        Confira a busca ou limpe os filtros.
                    </EstadoVazio>
                ) : (filtros.visao ?? 'abertas') === 'abertas' ? (
                    <EstadoVazio titulo="Nada em aberto">
                        Fila zerada. As solicitações novas, do portal ou da equipe, aparecem aqui.
                    </EstadoVazio>
                ) : (
                    <EstadoVazio titulo="Nenhuma solicitação aqui" />
                )
            ) : (
                <Tabela itens={fila.data.itens} fuso={fuso} aberta={aberta} />
            )}

            <JanelaNovaSolicitacao
                perfil={perfil}
                aberta={Boolean(filtros.nova) && pode(perfil, 'solicitacao.criar')}
                aoMudar={mudarNova}
                clienteInicial={filtros.cliente}
                aoAbrir={(s) =>
                    void navegar({
                        to: '/solicitacoes/$id',
                        params: { id: s.id },
                        search: (atual: FiltrosFila) => ({ ...atual, nova: undefined }),
                    })
                }
            />

            {fila.data && (
                <Paginacao
                    pagina={filtros.pagina ?? 1}
                    tamanhoPagina={TAMANHO_PAGINA}
                    total={Number(fila.data.total)}
                />
            )}
        </Pagina>
    )
}

function Prazo({ item, fuso }: { item: SolicitacaoResumo; fuso: string }) {
    const pausada = item.status === 'AGUARDANDO_CLIENTE'
    const situacao = SITUACAO_SLA[item.situacaoSla]
    if (!item.prazoResolucao || !situacao) return <span className="text-suave">—</span>
    const final = item.situacaoSla === 'CUMPRIDO'
    return (
        <span className="flex items-center gap-2" title={`Prazo de solução: ${dataHora(item.prazoResolucao, fuso)}`}>
            <span
                aria-hidden="true"
                className={`size-2.5 shrink-0 rounded-full ${pausada ? 'bg-suave' : situacao.estilo}`}
            />
            <span className="sr-only">{pausada ? 'Pausado' : situacao.rotulo}:</span>
            <span
                className={
                    item.situacaoSla === 'VIOLADO'
                        ? 'text-vermelho'
                        : item.situacaoSla === 'EM_RISCO'
                          ? 'text-ambar-texto'
                          : ''
                }
            >
                {final ? 'cumprido' : pausada ? 'pausado' : prazoRelativo(item.prazoResolucao, fuso)}
            </span>
        </span>
    )
}

function Tabela({ itens, fuso, aberta }: { itens: SolicitacaoResumo[]; fuso: string; aberta?: string }) {
    return (
        <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
            <table className="w-full text-sm">
                <thead className="border-b border-linha text-left text-xs text-suave">
                    <tr>
                        <th className="w-16 px-4 py-2.5 font-semibold">#</th>
                        <th className="px-4 py-2.5 font-semibold">Solicitação</th>
                        <th className="hidden px-4 py-2.5 font-semibold md:table-cell">Status</th>
                        <th className="hidden px-4 py-2.5 font-semibold lg:table-cell">Prioridade</th>
                        <th className="hidden px-4 py-2.5 font-semibold lg:table-cell">Responsável</th>
                        <th className="px-4 py-2.5 font-semibold">Prazo</th>
                        <th className="hidden px-4 py-2.5 font-semibold xl:table-cell">Aberta</th>
                    </tr>
                </thead>
                <tbody>
                    {itens.map((s) => (
                        <tr
                            key={s.id}
                            aria-current={s.id === aberta ? 'true' : undefined}
                            className="border-b border-linha last:border-0 hover:bg-trilho/50 aria-[current]:bg-acento-suave/60"
                        >
                            <td className="px-4 py-2.5 text-suave tabular-nums">{s.numero}</td>
                            <td className="px-4 py-2.5">
                                <Link
                                    to="/solicitacoes/$id"
                                    params={{ id: s.id }}
                                    search={(atual: Record<string, unknown>) => atual}
                                    data-solicitacao={s.id}
                                    className="font-semibold hover:underline"
                                >
                                    {s.titulo}
                                </Link>
                                <span className="block text-xs text-suave">{s.cliente.nome}</span>
                            </td>
                            <td className="hidden px-4 py-2.5 md:table-cell">
                                <span
                                    className={`rounded-couro px-2 py-0.5 text-xs whitespace-nowrap ${ESTILO_STATUS[s.status]}`}
                                >
                                    {STATUS[s.status]}
                                </span>
                            </td>
                            <td className={`hidden px-4 py-2.5 lg:table-cell ${ESTILO_PRIORIDADE[s.prioridade]}`}>
                                {PRIORIDADE[s.prioridade]}
                            </td>
                            <td className="hidden px-4 py-2.5 text-suave lg:table-cell">
                                {s.responsavel?.nome ?? 'Sem responsável'}
                            </td>
                            <td className="px-4 py-2.5 whitespace-nowrap">
                                <Prazo item={s} fuso={fuso} />
                            </td>
                            <td className="hidden px-4 py-2.5 text-suave xl:table-cell">{quando(s.abertaEm, fuso)}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    )
}
