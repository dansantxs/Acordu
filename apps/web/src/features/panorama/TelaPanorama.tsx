import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import { Tab, TabList, Tabs } from 'react-aria-components'
import { TIPO_CONTRATO } from '@/features/solicitacoes/rotulos'
import type { components } from '@/shared/api/interno'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { dataCurta, hojeNoFuso } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Pagina } from '@/shared/ui/Pagina'
import { consultaPanorama, intervalo, modoDe, type FiltrosPanorama, type ModoPeriodo } from './consultas'

const ROTULO_MODO: Record<ModoPeriodo, string> = { atual: 'Este mês', anterior: 'Mês passado', intervalo: 'Intervalo' }

/**
 * Panorama (F6b, contrato §11): fila e SLA do momento, horas e cumprimento do SLA no período, consumo dos contratos e
 * tarefas. Barras desenhadas aqui, sem biblioteca de gráficos (decisão 6 do F6). Bloco sem permissão vem nulo e some.
 */
export function TelaPanorama({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosPanorama }) {
    const navegar = useNavigate()
    const fuso = perfil.tenant.fusoHorario
    const hoje = hojeNoFuso(fuso)
    const { de, ate } = intervalo(filtros, fuso)
    const painel = useQuery(consultaPanorama(de, ate))
    const modo = modoDe(filtros)
    const ir = (f: FiltrosPanorama) => void navegar({ to: '/panorama', search: f, replace: true })
    const erroAte = painel.error instanceof ErroApi ? painel.error.doCampo('ate') : undefined
    const d = painel.data

    return (
        <Pagina ajuda="panorama" titulo="Panorama" contexto={`${dataCurta(de, hoje)} a ${dataCurta(ate, hoje)}`}>
            <div className="flex flex-wrap items-end gap-3">
                <Tabs
                    selectedKey={modo}
                    onSelectionChange={(v) =>
                        ir(v === 'anterior' ? { periodo: 'anterior' } : v === 'intervalo' ? { de, ate } : {})
                    }
                >
                    <TabList aria-label="Período" className="flex w-fit gap-1 rounded-couro bg-trilho p-1">
                        {(Object.keys(ROTULO_MODO) as ModoPeriodo[]).map((m) => (
                            <Tab
                                key={m}
                                id={m}
                                className="cursor-default rounded-couro px-3 py-1.5 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                            >
                                {ROTULO_MODO[m]}
                            </Tab>
                        ))}
                    </TabList>
                </Tabs>
                {modo === 'intervalo' && (
                    <>
                        <CampoData id="pan-de" rotulo="De" valor={de} aoMudar={(v) => ir({ de: v, ate })} />
                        <CampoData id="pan-ate" rotulo="Até" valor={ate} aoMudar={(v) => ir({ de, ate: v })} />
                    </>
                )}
            </div>
            {erroAte && <p className="text-sm text-vermelho">{erroAte}</p>}
            {painel.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : painel.isError && !erroAte ? (
                <Alerta>{mensagemDeErro(painel.error)}</Alerta>
            ) : d ? (
                <div className={`grid gap-6 lg:grid-cols-2 ${painel.isPlaceholderData ? 'opacity-60' : ''}`}>
                    {d.fila && (
                        <Bloco titulo="Fila agora">
                            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                                <Numero valor={d.fila.abertas} rotulo="Abertas" para={{ to: '/solicitacoes' }} />
                                <Numero
                                    valor={d.fila.emAndamento}
                                    rotulo="Em andamento"
                                    para={{ to: '/solicitacoes' }}
                                />
                                <Numero
                                    valor={d.fila.aguardandoCliente}
                                    rotulo="Aguardando cliente"
                                    para={{ to: '/solicitacoes', search: { visao: 'aguardando-cliente' } }}
                                />
                                <Numero valor={d.fila.aguardandoTerceiro} rotulo="Aguardando terceiro" />
                                <Numero
                                    valor={d.fila.semResponsavel}
                                    rotulo="Sem responsável"
                                    alerta
                                    para={{ to: '/solicitacoes', search: { responsavel: 'ninguem' } }}
                                />
                            </div>
                        </Bloco>
                    )}
                    {d.sla && (
                        <Bloco titulo="SLA">
                            <div className="grid grid-cols-3 gap-3">
                                <Numero
                                    valor={d.sla.emRisco}
                                    rotulo="Em risco agora"
                                    alerta
                                    para={{ to: '/solicitacoes', search: { sla: 'EM_RISCO' } }}
                                />
                                <Numero
                                    valor={d.sla.violadas}
                                    rotulo="Violadas agora"
                                    alerta
                                    para={{ to: '/solicitacoes', search: { sla: 'VIOLADO' } }}
                                />
                                <Numero
                                    valor={
                                        d.sla.cumprimentoPeriodo == null ? '—' : `${Number(d.sla.cumprimentoPeriodo)}%`
                                    }
                                    rotulo="Cumprido no período"
                                />
                            </div>
                            <p className="text-xs text-suave">
                                Cumprido: das resolvidas no período com SLA, quantas dentro do prazo.
                            </p>
                        </Bloco>
                    )}
                    {d.horas && <Horas horas={d.horas} />}
                    {d.contratos && (
                        <Bloco titulo="Contratos este mês">
                            {d.contratos.length === 0 ? (
                                <p className="text-sm text-suave">Nenhum contrato ativo.</p>
                            ) : (
                                <ul className="flex flex-col gap-3">
                                    {d.contratos.map((c) => {
                                        const pct = c.percentual == null ? null : Number(c.percentual)
                                        return (
                                            <li key={c.contratoId} className="flex flex-col gap-1 text-sm">
                                                <div className="flex items-baseline justify-between gap-3">
                                                    <Link
                                                        to="/contratos/$id"
                                                        params={{ id: c.contratoId }}
                                                        className="hover:underline"
                                                    >
                                                        {c.cliente}{' '}
                                                        <span className="text-xs text-suave">
                                                            {TIPO_CONTRATO[c.tipo]}
                                                        </span>
                                                    </Link>
                                                    <span className="text-xs text-suave tabular-nums">
                                                        {formatarDuracao(Number(c.consumidoMinutos))}
                                                        {c.franquiaMinutos != null &&
                                                            ` de ${formatarDuracao(Number(c.franquiaMinutos))}`}
                                                    </span>
                                                </div>
                                                {pct != null && (
                                                    <Barra
                                                        rotulo={`Consumo de ${c.cliente}`}
                                                        percentual={pct}
                                                        texto={`${pct}%`}
                                                        marcas
                                                    />
                                                )}
                                            </li>
                                        )
                                    })}
                                </ul>
                            )}
                        </Bloco>
                    )}
                    {d.tarefas && (
                        <Bloco titulo="Tarefas">
                            <div className="grid grid-cols-2 gap-3">
                                <Numero valor={d.tarefas.vencidas} rotulo="Vencidas" alerta para={{ to: '/tarefas' }} />
                            </div>
                            {d.tarefas.proximasRecorrentes && d.tarefas.proximasRecorrentes.length > 0 && (
                                <div className="flex flex-col gap-1">
                                    <h3 className="text-xs font-semibold text-suave">Próximas recorrentes</h3>
                                    <ul className="divide-y divide-linha rounded-couro border border-linha text-sm">
                                        {d.tarefas.proximasRecorrentes.map((r) => (
                                            <li
                                                key={`${r.recorrenciaId}-${r.data}`}
                                                className="flex items-baseline justify-between gap-3 px-3 py-2"
                                            >
                                                <Link
                                                    to="/recorrencias/$id"
                                                    params={{ id: r.recorrenciaId }}
                                                    className="min-w-0 truncate hover:underline"
                                                >
                                                    {r.titulo} <span className="text-xs text-suave">{r.cliente}</span>
                                                </Link>
                                                <span className="text-xs text-suave tabular-nums">
                                                    {dataCurta(r.data, hoje)}
                                                </span>
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                            )}
                        </Bloco>
                    )}
                </div>
            ) : null}
        </Pagina>
    )
}

function Bloco({ titulo, children }: { titulo: string; children: ReactNode }) {
    const id = `pan-${titulo.toLowerCase().replace(/\s+/g, '-')}`
    return (
        <section
            aria-labelledby={id}
            className="flex flex-col gap-3 rounded-couro border border-linha bg-superficie p-5"
        >
            <h2 id={id} className="font-titulo text-xl font-semibold">
                {titulo}
            </h2>
            {children}
        </section>
    )
}

type Destino =
    | {
          to: '/solicitacoes'
          search?: { visao?: 'aguardando-cliente'; responsavel?: 'ninguem'; sla?: 'EM_RISCO' | 'VIOLADO' }
      }
    | { to: '/tarefas' }

function Numero({
    valor,
    rotulo,
    para,
    alerta = false,
}: {
    valor: number | string
    rotulo: string
    para?: Destino
    alerta?: boolean
}) {
    const n = typeof valor === 'number' ? valor : Number(valor)
    const destaque = alerta && !Number.isNaN(n) && n > 0
    const conteudo = (
        <>
            <span
                className={`block font-titulo text-3xl font-semibold tabular-nums ${destaque ? 'text-vermelho' : ''}`}
            >
                {valor}
            </span>
            <span className="text-xs text-suave">{rotulo}</span>
        </>
    )
    return para ? (
        <Link {...para} className="rounded-couro p-2 hover:bg-trilho">
            {conteudo}
        </Link>
    ) : (
        <div className="p-2">{conteudo}</div>
    )
}

/** Barra horizontal acessível (role="meter"); com `marcas`, os traços de 80% e 100% da régua do contrato. */
function Barra({
    rotulo,
    percentual,
    texto,
    marcas = false,
}: {
    rotulo: string
    percentual: number
    texto: string
    marcas?: boolean
}) {
    const largura = Math.min(Math.max(percentual, 0), 100)
    const cor = marcas && percentual >= 100 ? 'bg-vermelho' : marcas && percentual >= 80 ? 'bg-ambar' : 'bg-acento'
    return (
        <div
            role="meter"
            aria-label={rotulo}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={largura}
            aria-valuetext={texto}
            className="relative h-2 rounded-full bg-trilho"
        >
            <div className={`h-full rounded-full ${cor}`} style={{ width: `${largura}%` }} />
            {marcas && <span aria-hidden="true" className="absolute inset-y-[-2px] left-[80%] w-px bg-tinta/30" />}
        </div>
    )
}

function Horas({ horas }: { horas: components['schemas']['HorasDashboardDto'] }) {
    const faturavel = Number(horas.faturavelMinutos)
    const nao = Number(horas.naoFaturavelMinutos)
    const total = faturavel + nao
    const maior = Math.max(1, ...horas.porCliente.map((c) => Number(c.minutos)))
    return (
        <Bloco titulo="Horas no período">
            <p className="text-sm">
                <span className="font-semibold tabular-nums">{formatarDuracao(total)}</span> apontadas:{' '}
                <span className="tabular-nums">{formatarDuracao(faturavel)}</span> faturáveis e{' '}
                <span className="tabular-nums">{formatarDuracao(nao)}</span> não faturáveis
            </p>
            {total > 0 && (
                <Barra
                    rotulo="Horas faturáveis"
                    percentual={Math.round((faturavel / total) * 100)}
                    texto={`${Math.round((faturavel / total) * 100)}% faturáveis`}
                />
            )}
            {horas.porCliente.length > 0 && (
                <div className="flex flex-col gap-2">
                    <h3 className="text-xs font-semibold text-suave">Faturáveis por cliente</h3>
                    <ul className="flex flex-col gap-2">
                        {horas.porCliente.map((c) => {
                            const minutos = Number(c.minutos)
                            return (
                                <li
                                    key={c.clienteId}
                                    className="grid grid-cols-[minmax(0,10rem)_1fr_auto] items-center gap-3 text-sm"
                                >
                                    <Link
                                        to="/clientes/$id"
                                        params={{ id: c.clienteId }}
                                        className="truncate hover:underline"
                                    >
                                        {c.nome}
                                    </Link>
                                    <Barra
                                        rotulo={`Horas de ${c.nome}`}
                                        percentual={(minutos / maior) * 100}
                                        texto={formatarDuracao(minutos)}
                                    />
                                    <span className="text-xs text-suave tabular-nums">{formatarDuracao(minutos)}</span>
                                </li>
                            )
                        })}
                    </ul>
                </div>
            )}
        </Bloco>
    )
}

function CampoData({
    id,
    rotulo,
    valor,
    aoMudar,
}: {
    id: string
    rotulo: string
    valor: string
    aoMudar: (v: string) => void
}) {
    return (
        <div className="flex flex-col gap-1">
            <label htmlFor={id} className="text-sm font-semibold">
                {rotulo}
            </label>
            <input
                id={id}
                type="date"
                value={valor}
                onChange={(e) => e.target.value && aoMudar(e.target.value)}
                className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta"
            />
        </div>
    )
}
