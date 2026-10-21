import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from '@tanstack/react-router'
import { Tab, TabList, Tabs } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { CampoCliente } from '@/features/clientes/CampoCliente'
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
    ABAS_PROJETO,
    consultaProjetos,
    ESTILO_STATUS_PROJETO,
    STATUS_PROJETO,
    TAMANHO_PAGINA,
    type AbaProjeto,
    type FiltrosProjetos,
} from './consultas'
import { JanelaProjeto } from './JanelaProjeto'

/** Projetos (RF-PRJ-01/02, F3e): lista por situação e cliente; o detalhe com progresso e tarefas abre em painel. */
export function TelaProjetos({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosProjetos }) {
    const navegar = useNavigate()
    const aberto = useParams({ strict: false }).id
    const lista = useQuery(consultaProjetos(filtros))
    const hoje = hojeNoFuso(perfil.tenant.fusoHorario)
    const filtrar = (mudanca: Partial<FiltrosProjetos>) =>
        void navegar({ to: '.', search: (atual: FiltrosProjetos) => ({ ...atual, ...mudanca, pagina: undefined }) })
    const mudarNovo = (aberta: boolean) =>
        void navegar({
            to: '.',
            search: (atual: FiltrosProjetos) => ({ ...atual, novo: aberta || undefined }),
            replace: !aberta,
        })

    return (
        <Pagina
            titulo="Projetos"
            acoes={
                pode(perfil, 'projeto.gerenciar') && (
                    <Botao onPress={() => mudarNovo(true)}>
                        <Icone nome="mais" />
                        Novo projeto
                    </Botao>
                )
            }
        >
            <Tabs
                selectedKey={filtros.situacao ?? 'andamento'}
                onSelectionChange={(v) => filtrar({ situacao: v === 'andamento' ? undefined : (v as AbaProjeto) })}
            >
                <TabList aria-label="Situação" className="flex w-fit flex-wrap gap-1 rounded-couro bg-trilho p-1">
                    {(Object.keys(ABAS_PROJETO) as AbaProjeto[]).map((aba) => (
                        <Tab
                            key={aba}
                            id={aba}
                            className="cursor-default rounded-couro px-3 py-1.5 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {ABAS_PROJETO[aba].rotulo}
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
                    <EstadoVazio titulo="Nenhum projeto deste cliente aqui" />
                ) : (filtros.situacao ?? 'andamento') === 'andamento' ? (
                    <EstadoVazio titulo="Nenhum projeto em andamento">
                        Projeto agrupa as tarefas de uma entrega maior, com prazo e estimativa de horas.
                    </EstadoVazio>
                ) : (
                    <EstadoVazio titulo="Nenhum projeto aqui" />
                )
            ) : (
                <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
                    <table className="w-full text-sm">
                        <thead className="border-b border-linha text-left text-xs text-suave">
                            <tr>
                                <th className="px-4 py-2.5 font-semibold">Projeto</th>
                                <th className="hidden px-4 py-2.5 font-semibold md:table-cell">Status</th>
                                <th className="px-4 py-2.5 font-semibold">Prazo</th>
                                <th className="hidden px-4 py-2.5 font-semibold lg:table-cell">Estimativa</th>
                            </tr>
                        </thead>
                        <tbody>
                            {lista.data.itens.map((p) => {
                                const atrasado = p.status === 'EM_ANDAMENTO' && p.prazo !== null && p.prazo < hoje
                                return (
                                    <tr
                                        key={p.id}
                                        aria-current={p.id === aberto ? 'true' : undefined}
                                        className="border-b border-linha last:border-0 hover:bg-trilho/50 aria-[current]:bg-acento-suave/60"
                                    >
                                        <td className="px-4 py-2.5">
                                            <Link
                                                to="/projetos/$id"
                                                params={{ id: p.id }}
                                                search={(atual: FiltrosProjetos) => atual}
                                                data-projeto={p.id}
                                                className="block font-semibold hover:underline"
                                            >
                                                {p.nome}
                                            </Link>
                                            <span className="text-xs text-suave">{p.cliente.nome}</span>
                                        </td>
                                        <td className="hidden px-4 py-2.5 md:table-cell">
                                            <span
                                                className={`rounded-couro px-2 py-0.5 text-xs whitespace-nowrap ${ESTILO_STATUS_PROJETO[p.status]}`}
                                            >
                                                {STATUS_PROJETO[p.status]}
                                            </span>
                                        </td>
                                        <td
                                            className={`px-4 py-2.5 whitespace-nowrap ${atrasado ? 'text-vermelho' : ''}`}
                                        >
                                            {p.inicio && (
                                                <span className="text-suave">{dataCurta(p.inicio, hoje)} – </span>
                                            )}
                                            {p.prazo ? dataCurta(p.prazo, hoje) : '—'}
                                            {atrasado && <span className="sr-only"> (atrasado)</span>}
                                        </td>
                                        <td className="hidden px-4 py-2.5 text-suave lg:table-cell">
                                            {p.estimativaMinutos != null
                                                ? formatarDuracao(Number(p.estimativaMinutos))
                                                : '—'}
                                        </td>
                                    </tr>
                                )
                            })}
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

            <JanelaProjeto
                aberta={Boolean(filtros.novo) && pode(perfil, 'projeto.gerenciar')}
                aoMudar={mudarNovo}
                aoSalvar={(p) =>
                    void navegar({
                        to: '/projetos/$id',
                        params: { id: p.id },
                        search: (atual: FiltrosProjetos) => ({ ...atual, novo: undefined }),
                    })
                }
            />
        </Pagina>
    )
}
