import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from '@tanstack/react-router'
import { Tab, TabList, Tabs } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { dataCurta, hojeNoFuso } from '@/shared/format/datas'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Icone } from '@/shared/ui/Icone'
import { Pagina } from '@/shared/ui/Pagina'
import { Paginacao } from '@/shared/ui/Paginacao'
import {
    ABAS_RECORRENCIA,
    consultaRecorrencias,
    ESTILO_STATUS_RECORRENCIA,
    PERIODICIDADE,
    STATUS_RECORRENCIA,
    TAMANHO_PAGINA,
    type AbaRecorrencia,
    type FiltrosRecorrencias,
} from './consultas'
import { JanelaRecorrencia } from './JanelaRecorrencia'

/** Recorrências (RF-REC-01..04, F4c): lista por situação e cliente, pela próxima data; o detalhe abre em painel. */
export function TelaRecorrencias({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosRecorrencias }) {
    const navegar = useNavigate()
    const aberta = useParams({ strict: false }).id
    const lista = useQuery(consultaRecorrencias(filtros))
    const equipe = useQuery(consultaEquipe)
    const nomes = new Map((equipe.data ?? []).map((p) => [p.id, p.nome]))
    const hoje = hojeNoFuso(perfil.tenant.fusoHorario)
    const gerencia = pode(perfil, 'recorrencia.gerenciar')
    const filtrar = (mudanca: Partial<FiltrosRecorrencias>) =>
        void navegar({ to: '.', search: (atual: FiltrosRecorrencias) => ({ ...atual, ...mudanca, pagina: undefined }) })
    const mudarNova = (abrir: boolean) =>
        void navegar({
            to: '.',
            search: (atual: FiltrosRecorrencias) => ({ ...atual, nova: abrir || undefined }),
            replace: !abrir,
        })
    const aba = filtros.status ?? 'ativas'

    return (
        <Pagina
            titulo="Recorrências"
            acoes={
                gerencia && (
                    <Botao onPress={() => mudarNova(true)}>
                        <Icone nome="mais" />
                        Nova recorrência
                    </Botao>
                )
            }
        >
            <Tabs
                selectedKey={aba}
                onSelectionChange={(v) => filtrar({ status: v === 'ativas' ? undefined : (v as AbaRecorrencia) })}
            >
                <TabList aria-label="Situação" className="flex w-fit flex-wrap gap-1 rounded-couro bg-trilho p-1">
                    {(Object.keys(ABAS_RECORRENCIA) as AbaRecorrencia[]).map((a) => (
                        <Tab
                            key={a}
                            id={a}
                            className="cursor-default rounded-couro px-3 py-1.5 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {ABAS_RECORRENCIA[a].rotulo}
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
                filtros.cliente ? (
                    <EstadoVazio titulo="Nenhuma recorrência deste cliente aqui" />
                ) : aba === 'ativas' ? (
                    <EstadoVazio titulo="Nenhuma recorrência ativa">
                        Recorrência cria sozinha a tarefa que se repete (o fechamento do mês, a revisão da semana), já
                        com responsável, checklist e prazo.
                    </EstadoVazio>
                ) : (
                    <EstadoVazio titulo="Nenhuma recorrência aqui" />
                )
            ) : (
                <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
                    <table className="w-full text-sm">
                        <thead className="border-b border-linha text-left text-xs text-suave">
                            <tr>
                                <th className="px-4 py-2.5 font-semibold">Tarefa</th>
                                <th className="hidden px-4 py-2.5 font-semibold md:table-cell">Repete</th>
                                <th className="px-4 py-2.5 font-semibold">Próxima</th>
                                <th className="hidden px-4 py-2.5 font-semibold lg:table-cell">Responsável</th>
                                <th className="hidden px-4 py-2.5 font-semibold md:table-cell">Status</th>
                            </tr>
                        </thead>
                        <tbody>
                            {lista.data.itens.map((r) => (
                                <tr
                                    key={r.id}
                                    aria-current={r.id === aberta ? 'true' : undefined}
                                    className="border-b border-linha last:border-0 hover:bg-trilho/50 aria-[current]:bg-acento-suave/60"
                                >
                                    <td className="px-4 py-2.5">
                                        <Link
                                            to="/recorrencias/$id"
                                            params={{ id: r.id }}
                                            search={(atual: FiltrosRecorrencias) => atual}
                                            data-recorrencia={r.id}
                                            className="block font-semibold hover:underline"
                                        >
                                            {r.titulo}
                                        </Link>
                                        <span className="text-xs text-suave">{r.clienteNome}</span>
                                    </td>
                                    <td className="hidden px-4 py-2.5 md:table-cell">
                                        {PERIODICIDADE[r.periodicidade]}
                                    </td>
                                    <td className="px-4 py-2.5 whitespace-nowrap">
                                        {r.status === 'ATIVA' && r.proximaData ? dataCurta(r.proximaData, hoje) : '—'}
                                    </td>
                                    <td className="hidden px-4 py-2.5 text-suave lg:table-cell">
                                        {r.responsavelId
                                            ? (nomes.get(r.responsavelId) ?? 'Pessoa que saiu')
                                            : 'Sem responsável'}
                                    </td>
                                    <td className="hidden px-4 py-2.5 md:table-cell">
                                        <span
                                            className={`rounded-couro px-2 py-0.5 text-xs whitespace-nowrap ${ESTILO_STATUS_RECORRENCIA[r.status]}`}
                                        >
                                            {STATUS_RECORRENCIA[r.status]}
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

            <JanelaRecorrencia
                fuso={perfil.tenant.fusoHorario}
                aberta={Boolean(filtros.nova) && gerencia}
                aoMudar={mudarNova}
                aoSalvar={(r) =>
                    void navegar({
                        to: '/recorrencias/$id',
                        params: { id: r.id },
                        search: (atual: FiltrosRecorrencias) => ({ ...atual, nova: undefined }),
                    })
                }
            />
        </Pagina>
    )
}
