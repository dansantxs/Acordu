import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { Tab, TabList, Tabs } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { TIPO_CONTRATO } from '@/features/solicitacoes/rotulos'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { dataCurta, hojeNoFuso } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Icone } from '@/shared/ui/Icone'
import { Pagina } from '@/shared/ui/Pagina'
import { Paginacao } from '@/shared/ui/Paginacao'
import {
    ABAS_CONTRATO,
    consultaContratos,
    ESTILO_STATUS_CONTRATO,
    STATUS_CONTRATO,
    TAMANHO_PAGINA,
    type AbaContrato,
    type FiltrosContratos,
} from './consultas'
import { JanelaCadastroContrato } from './JanelaCadastroContrato'
import { JanelaFechamentoLote } from './JanelaFechamentoLote'

/** Contratos (F4a): lista por situação e cliente; o detalhe é página própria (decisão 2 do F4). */
export function TelaContratos({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosContratos }) {
    const navegar = useNavigate()
    const [fechandoLote, setFechandoLote] = useState(false)
    const lista = useQuery(consultaContratos(filtros))
    const hoje = hojeNoFuso(perfil.tenant.fusoHorario)
    const filtrar = (mudanca: Partial<FiltrosContratos>) =>
        void navegar({
            to: '/contratos',
            search: (atual: FiltrosContratos) => ({ ...atual, ...mudanca, pagina: undefined }),
        })
    const mudarNovo = (aberta: boolean) =>
        void navegar({
            to: '/contratos',
            search: (atual: FiltrosContratos) => ({ ...atual, novo: aberta || undefined }),
            replace: !aberta,
        })

    return (
        <Pagina
            titulo="Contratos"
            acoes={
                <div className="flex flex-wrap gap-2">
                    {pode(perfil, 'contrato.fechar_periodo') && (
                        <Botao variante="secundario" onPress={() => setFechandoLote(true)}>
                            Fechar o mês
                        </Botao>
                    )}
                    {pode(perfil, 'contrato.gerenciar') && (
                        <Botao onPress={() => mudarNovo(true)}>
                            <Icone nome="mais" />
                            Novo contrato
                        </Botao>
                    )}
                </div>
            }
        >
            <Tabs
                selectedKey={filtros.estado ?? 'ativos'}
                onSelectionChange={(v) => filtrar({ estado: v === 'ativos' ? undefined : (v as AbaContrato) })}
            >
                <TabList aria-label="Situação" className="flex w-fit flex-wrap gap-1 rounded-couro bg-trilho p-1">
                    {(Object.keys(ABAS_CONTRATO) as AbaContrato[]).map((aba) => (
                        <Tab
                            key={aba}
                            id={aba}
                            className="cursor-default rounded-couro px-3 py-1.5 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {ABAS_CONTRATO[aba].rotulo}
                        </Tab>
                    ))}
                </TabList>
            </Tabs>
            {pode(perfil, 'cliente.ver') && (
                <CampoCliente
                    rotulo="Cliente"
                    rotuloOculto
                    valor={filtros.cliente ?? null}
                    aoMudar={(v) => filtrar({ cliente: v ?? undefined })}
                    vazio="Todos os clientes"
                    dica="Cliente"
                    className="sm:w-64"
                />
            )}

            {lista.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : lista.isError ? (
                <Alerta>{mensagemDeErro(lista.error)}</Alerta>
            ) : lista.data.itens.length === 0 ? (
                (filtros.estado ?? 'ativos') === 'ativos' && !filtros.cliente ? (
                    <EstadoVazio titulo="Nenhum contrato ativo">
                        Contrato define as horas do cliente (mensalidade ou banco de horas) e os prazos de atendimento.
                    </EstadoVazio>
                ) : (
                    <EstadoVazio titulo="Nenhum contrato aqui" />
                )
            ) : (
                <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
                    <table className="w-full text-sm">
                        <thead className="border-b border-linha text-left text-xs text-suave">
                            <tr>
                                <th className="px-4 py-2.5 font-semibold">Cliente</th>
                                <th className="px-4 py-2.5 font-semibold">Tipo</th>
                                <th className="hidden px-4 py-2.5 font-semibold md:table-cell">Vigência</th>
                                <th className="hidden px-4 py-2.5 font-semibold lg:table-cell">Horas</th>
                                <th className="hidden px-4 py-2.5 font-semibold md:table-cell">Situação</th>
                            </tr>
                        </thead>
                        <tbody>
                            {lista.data.itens.map((c) => (
                                <tr key={c.id} className="border-b border-linha last:border-0 hover:bg-trilho/50">
                                    <td className="px-4 py-2.5">
                                        <Link
                                            to="/contratos/$id"
                                            params={{ id: c.id }}
                                            className="font-semibold hover:underline"
                                        >
                                            {c.clienteNome}
                                        </Link>
                                    </td>
                                    <td className="px-4 py-2.5">{TIPO_CONTRATO[c.tipo]}</td>
                                    <td className="hidden px-4 py-2.5 text-suave md:table-cell">
                                        {dataCurta(c.inicio, hoje)} a {c.fim ? dataCurta(c.fim, hoje) : 'sem fim'}
                                    </td>
                                    <td className="hidden px-4 py-2.5 text-suave lg:table-cell">
                                        {c.franquiaMinutos != null
                                            ? `${formatarDuracao(Number(c.franquiaMinutos))}${c.tipo === 'MENSAL_FIXO' ? '/mês' : ''}`
                                            : '—'}
                                    </td>
                                    <td className="hidden px-4 py-2.5 md:table-cell">
                                        <span
                                            className={`rounded-couro px-2 py-0.5 text-xs ${ESTILO_STATUS_CONTRATO[c.status]}`}
                                        >
                                            {STATUS_CONTRATO[c.status]}
                                        </span>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
            {lista.data && (
                <Paginacao
                    pagina={filtros.pagina ?? 1}
                    tamanhoPagina={TAMANHO_PAGINA}
                    total={Number(lista.data.total)}
                />
            )}

            <JanelaFechamentoLote fuso={perfil.tenant.fusoHorario} aberta={fechandoLote} aoMudar={setFechandoLote} />
            <JanelaCadastroContrato
                fuso={perfil.tenant.fusoHorario}
                aberta={Boolean(filtros.novo) && pode(perfil, 'contrato.gerenciar')}
                aoMudar={mudarNovo}
                clienteInicial={filtros.cliente}
                aoSalvar={(c) => void navegar({ to: '/contratos/$id', params: { id: c.id } })}
            />
        </Pagina>
    )
}
