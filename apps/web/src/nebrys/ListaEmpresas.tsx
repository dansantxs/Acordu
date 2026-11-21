import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useEffect } from 'react'
import { Tab, TabList, Tabs } from 'react-aria-components'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { formatarDocumento } from '@/shared/format/mascaras'
import { Alerta } from '@/shared/ui/Alerta'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Pagina } from '@/shared/ui/Pagina'
import { Paginacao } from '@/shared/ui/Paginacao'
import { encerrarSeRecusada, type SituacaoEmpresa } from './api'
import { consultaEmpresas, TAMANHO_PAGINA, type FiltrosEmpresas } from './consultas'
import { data, ESTILO_SITUACAO, FILTRO_SITUACAO, marcoDaSituacao, ROTULO_SITUACAO, SITUACOES } from './situacao'

const TODAS = 'todas'

/**
 * Empresas (F14-61): só os campos da relação com a Nebrys, das criadas por último, com filtro por situação. Nenhum dado
 * de negócio (F14-62): a API não manda, e a tela não tem para onde levar.
 */
export function ListaEmpresas({ filtros }: { filtros: FiltrosEmpresas }) {
    const navegar = useNavigate()
    const lista = useQuery(consultaEmpresas(filtros))
    // A lista só recebe 404 da barreira (sessão vencida ou conta fora da lista): volta para a entrada.
    useEffect(() => encerrarSeRecusada(lista.error), [lista.error])
    const filtrar = (fase: SituacaoEmpresa | undefined) =>
        void navegar({ to: '/nebrys/empresas', search: { fase, pagina: undefined } })

    return (
        <Pagina titulo="Empresas" contexto="Área da Nebrys">
            <Tabs
                selectedKey={filtros.fase ?? TODAS}
                onSelectionChange={(v) => filtrar(v === TODAS ? undefined : (v as SituacaoEmpresa))}
            >
                <TabList aria-label="Situação" className="flex w-fit flex-wrap gap-1 rounded-couro bg-trilho p-1">
                    {[TODAS, ...SITUACOES].map((aba) => (
                        <Tab
                            key={aba}
                            id={aba}
                            className="cursor-default rounded-couro px-3 py-1.5 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {aba === TODAS ? 'Todas' : FILTRO_SITUACAO[aba as SituacaoEmpresa]}
                        </Tab>
                    ))}
                </TabList>
            </Tabs>

            {lista.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : lista.isError ? (
                <Alerta>{mensagemDeErro(lista.error)}</Alerta>
            ) : lista.data.itens.length === 0 ? (
                <EstadoVazio titulo={filtros.fase ? 'Nenhuma empresa nesta situação' : 'Nenhuma empresa ainda'} />
            ) : (
                <>
                    <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
                        <table className="w-full text-sm">
                            <thead className="border-b border-linha text-left text-xs text-suave">
                                <tr>
                                    <th className="px-4 py-2.5 font-semibold">Empresa</th>
                                    <th className="px-4 py-2.5 font-semibold">Situação</th>
                                    <th className="px-4 py-2.5 font-semibold">Criada em</th>
                                    <th className="px-4 py-2.5 font-semibold">Expurgo previsto</th>
                                    <th className="px-4 py-2.5 font-semibold">Criada por</th>
                                    <th className="px-4 py-2.5 text-right font-semibold">Equipe</th>
                                </tr>
                            </thead>
                            <tbody>
                                {lista.data.itens.map((e) => {
                                    const marco = marcoDaSituacao(e)
                                    return (
                                        <tr key={e.id} className="border-b border-linha last:border-0">
                                            <td className="px-4 py-2.5">
                                                <Link
                                                    to="/nebrys/empresas/$id"
                                                    params={{ id: e.id }}
                                                    className="block font-semibold hover:underline"
                                                >
                                                    {e.nome}
                                                </Link>
                                                <span className="text-xs text-suave tabular-nums">
                                                    {e.documento ? formatarDocumento(e.documento) : 'Sem documento'}
                                                </span>
                                            </td>
                                            <td className="px-4 py-2.5">
                                                <span
                                                    className={`rounded-couro px-2 py-0.5 text-xs whitespace-nowrap ${ESTILO_SITUACAO[e.situacao]}`}
                                                >
                                                    {ROTULO_SITUACAO[e.situacao]}
                                                </span>
                                                {marco && (
                                                    <span className="mt-1 block text-xs text-suave">{marco}</span>
                                                )}
                                            </td>
                                            <td className="px-4 py-2.5 whitespace-nowrap">{data(e.criadaEm)}</td>
                                            <td className="px-4 py-2.5 whitespace-nowrap">
                                                {e.expurgoPrevistoEm ? data(e.expurgoPrevistoEm) : '—'}
                                            </td>
                                            <td className="px-4 py-2.5">
                                                {e.criador ? (
                                                    <>
                                                        <span className="block">{e.criador.nome}</span>
                                                        <span className="text-xs text-suave">{e.criador.email}</span>
                                                    </>
                                                ) : (
                                                    <span className="text-suave">Conta removida</span>
                                                )}
                                            </td>
                                            <td className="px-4 py-2.5 text-right tabular-nums">
                                                {Number(e.pessoasNaEquipe)}
                                            </td>
                                        </tr>
                                    )
                                })}
                            </tbody>
                        </table>
                    </div>
                    <Paginacao
                        pagina={Number(lista.data.pagina)}
                        tamanhoPagina={Number(lista.data.tamanhoPagina) || TAMANHO_PAGINA}
                        total={Number(lista.data.total)}
                    />
                </>
            )}
        </Pagina>
    )
}
