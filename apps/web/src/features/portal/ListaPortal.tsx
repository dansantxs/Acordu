import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { Tab, TabList, Tabs } from 'react-aria-components'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { quando } from '@/shared/format/quando'
import { Alerta } from '@/shared/ui/Alerta'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Icone } from '@/shared/ui/Icone'
import { Paginacao } from '@/shared/ui/Paginacao'
import {
    consultaSolicitacoesPortal,
    ESTILO_STATUS_EXTERNO,
    GRUPOS,
    STATUS_EXTERNO,
    TAMANHO_PAGINA,
    type FiltrosPortal,
    type Grupo,
    type PortalEu,
} from './consultas'
import { BotaoNovaSolicitacao } from './BotaoNovaSolicitacao'
import { TituloPortal } from './CascaPortal'

const VAZIO: Record<Grupo, string> = {
    esperando: 'Nada esperando por você agora.',
    andamento: 'Nenhuma solicitação em andamento.',
    concluidas: 'Nenhuma solicitação concluída ainda.',
}

/**
 * Solicitações do portal (F7a): três grupos pelo status externo, com "Esperando você" contado em destaque quando há
 * algo (decisão 3). No escopo "só as próprias", só as do contato (o filtro é da API).
 */
export function ListaPortal({ eu, filtros, fuso }: { eu: PortalEu; filtros: FiltrosPortal; fuso: string }) {
    const navegar = useNavigate()
    const grupo = filtros.grupo ?? 'andamento'
    const lista = useQuery(consultaSolicitacoesPortal(grupo, filtros.pagina))
    const esperando = useQuery(consultaSolicitacoesPortal('esperando'))
    const quantasEsperando = Number(esperando.data?.total ?? 0)

    return (
        <>
            <TituloPortal
                titulo="Solicitações"
                contexto={eu.escopoPortal === 'PROPRIAS' ? `As suas, em ${eu.cliente.nome}` : eu.cliente.nome}
                acoes={
                    <BotaoNovaSolicitacao className="inline-flex items-center gap-2 rounded-couro bg-acento px-4 py-2 text-sm font-semibold text-botao-tinta hover:brightness-110">
                        <Icone nome="mais" />
                        Nova solicitação
                    </BotaoNovaSolicitacao>
                }
            />
            <Tabs
                selectedKey={grupo}
                onSelectionChange={(v) =>
                    void navegar({ to: '/portal', search: { grupo: v === 'andamento' ? undefined : (v as Grupo) } })
                }
            >
                <TabList
                    aria-label="Situação"
                    className="flex w-full gap-1 overflow-x-auto rounded-couro bg-trilho p-1 sm:w-fit"
                >
                    {(Object.keys(GRUPOS) as Grupo[]).map((g) => (
                        <Tab
                            key={g}
                            id={g}
                            className="cursor-default rounded-couro px-3 py-1.5 text-sm whitespace-nowrap text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {GRUPOS[g].rotulo}
                            {g === 'esperando' && quantasEsperando > 0 && (
                                <span className="ml-1.5 rounded-full bg-ambar px-1.5 text-xs font-semibold text-white tabular-nums">
                                    {quantasEsperando}
                                </span>
                            )}
                        </Tab>
                    ))}
                </TabList>
            </Tabs>
            {lista.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : lista.isError ? (
                <Alerta>{mensagemDeErro(lista.error)}</Alerta>
            ) : lista.data.itens.length === 0 ? (
                <EstadoVazio titulo={VAZIO[grupo]} />
            ) : (
                <ul className="flex flex-col gap-2">
                    {lista.data.itens.map((s) => (
                        <li key={s.id}>
                            <Link
                                to="/portal/solicitacoes/$id"
                                params={{ id: s.id }}
                                className="flex flex-col gap-1 rounded-couro border border-linha bg-superficie p-4 hover:bg-trilho/50"
                            >
                                <span className="flex items-start justify-between gap-3">
                                    <span className="font-semibold">
                                        <span className="font-normal text-suave tabular-nums">#{s.numero}</span>{' '}
                                        {s.titulo}
                                    </span>
                                    <span
                                        className={`shrink-0 rounded-couro px-2 py-0.5 text-xs ${ESTILO_STATUS_EXTERNO[s.statusExterno]}`}
                                    >
                                        {STATUS_EXTERNO[s.statusExterno]}
                                    </span>
                                </span>
                                <span className="text-xs text-suave">
                                    Aberta {quando(s.abertaEm, fuso)}
                                    {s.podeAprovar && (
                                        <span className="font-semibold text-ambar-texto">
                                            {' '}
                                            · entrega para você aprovar
                                        </span>
                                    )}
                                </span>
                            </Link>
                        </li>
                    ))}
                </ul>
            )}
            {lista.data && (
                <Paginacao
                    pagina={filtros.pagina ?? 1}
                    tamanhoPagina={TAMANHO_PAGINA}
                    total={Number(lista.data.total)}
                />
            )}
        </>
    )
}
