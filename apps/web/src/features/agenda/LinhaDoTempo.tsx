import { Link } from '@tanstack/react-router'
import type { CSSProperties } from 'react'
import { diaPorExtenso, somarDias } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import type { Agenda, CapacidadeSemana, ItemAgenda } from './consultas'

/** Uma faixa (pessoa ou cliente) com os itens dela. */
export interface Faixa {
    id: string
    nome: string
    itens: ItemAgenda[]
}

const SEM_RESPONSAVEL = 'sem-responsavel'
const SEM_CLIENTE = 'sem-cliente'
const GERAIS = 'gerais'

/**
 * Faixas da linha do tempo. Por pessoa: uma para cada pessoa da equipe, "Sem responsável" e, no topo, "Projetos e
 * contratos" (não têm responsável, contrato §11b). Por cliente: uma por cliente com item e "Sem cliente".
 */
export function montarFaixas(agenda: Agenda, por: 'pessoa' | 'cliente'): Faixa[] {
    if (por === 'cliente') {
        const mapa = new Map<string, Faixa>()
        for (const item of agenda.itens) {
            const id = item.cliente?.id ?? SEM_CLIENTE
            const faixa = mapa.get(id) ?? { id, nome: item.cliente?.nome ?? 'Sem cliente', itens: [] }
            faixa.itens.push(item)
            mapa.set(id, faixa)
        }
        return [...mapa.values()].sort((a, b) =>
            a.id === SEM_CLIENTE ? 1 : b.id === SEM_CLIENTE ? -1 : a.nome.localeCompare(b.nome, 'pt-BR'),
        )
    }
    const gerais = agenda.itens.filter((i) => i.tipo === 'PROJETO' || i.tipo === 'CONTRATO')
    const pessoais = agenda.itens.filter((i) => i.tipo !== 'PROJETO' && i.tipo !== 'CONTRATO')
    const naEquipe = new Set(agenda.equipe.map((p) => p.id))
    const faixas: Faixa[] = []
    if (gerais.length) faixas.push({ id: GERAIS, nome: 'Projetos e contratos', itens: gerais })
    for (const p of agenda.equipe)
        faixas.push({ id: p.id, nome: p.nome, itens: pessoais.filter((i) => i.responsavel?.id === p.id) })
    const sem = pessoais.filter((i) => !i.responsavel || !naEquipe.has(i.responsavel.id))
    if (sem.length) faixas.push({ id: SEM_RESPONSAVEL, nome: 'Sem responsável', itens: sem })
    return faixas
}

/** Os 10 dias úteis da janela são as segundas a sextas; feriado continua como coluna, marcado. */
export function colunasDaJanela(agenda: Agenda): Agenda['dias'] {
    return agenda.dias.filter((d) => {
        const semana = new Date(`${d.data}T12:00:00Z`).getUTCDay()
        return semana >= 1 && semana <= 5
    })
}

/** Coluna da data: fim de semana cai na sexta anterior; antes da janela, na coluna "Atrasados" (-1). */
function indiceDe(data: string, colunas: Agenda['dias']): number {
    if (data < colunas[0]!.data) return -1
    let alvo = data
    for (let i = 0; i < 3 && !colunas.some((c) => c.data === alvo); i++) alvo = somarDias(alvo, -1)
    const indice = colunas.findIndex((c) => c.data === alvo)
    return indice === -1 ? colunas.length - 1 : indice
}

const ESTILO_SITUACAO: Record<ItemAgenda['situacao'], string> = {
    NO_PRAZO: 'border-linha',
    EM_RISCO: 'border-ambar',
    VIOLADO: 'border-vermelho',
    VENCIDO: 'border-vermelho',
    PAUSADO: 'border-linha opacity-70',
    CONCLUIDO: 'border-verde opacity-60',
    PREVISTO: 'border-dashed border-linha',
}

const ROTULO_SITUACAO: Partial<Record<ItemAgenda['situacao'], string>> = {
    EM_RISCO: 'em risco',
    VIOLADO: 'violado',
    VENCIDO: 'vencido',
    PAUSADO: 'pausado',
    CONCLUIDO: 'concluído',
    PREVISTO: 'previsto',
}

function textoDoItem(item: ItemAgenda): string {
    switch (item.tipo) {
        case 'SOLICITACAO':
            return `#${item.numero} ${item.titulo}`
        case 'CONTRATO':
            return item.marco === 'FIM_VIGENCIA'
                ? `Fim do contrato · ${item.cliente?.nome ?? ''}`
                : `Consumo ${Number(item.percentual)}% · ${item.cliente?.nome ?? ''}`
        default:
            return item.titulo
    }
}

const TIPO_LEGIVEL: Record<ItemAgenda['tipo'], string> = {
    SOLICITACAO: 'Prazo da solicitação',
    TAREFA: 'Tarefa',
    PROJETO: 'Projeto',
    RECORRENCIA: 'Tarefa recorrente',
    CONTRATO: 'Contrato',
}

/** Chip de um item, com link para a tela dele. */
export function ChipItem({ item, mostrarCliente = false }: { item: ItemAgenda; mostrarCliente?: boolean }) {
    const classe = `block truncate rounded-couro border-l-4 bg-superficie px-2 py-1 text-xs shadow-sm hover:bg-trilho ${ESTILO_SITUACAO[item.situacao]}`
    const situacao = ROTULO_SITUACAO[item.situacao]
    const titulo = `${TIPO_LEGIVEL[item.tipo]}: ${textoDoItem(item)}${situacao ? ` (${situacao})` : ''}`
    const conteudo = (
        <>
            {textoDoItem(item)}
            {item.tipo === 'TAREFA' && item.estimativaMinutos != null && (
                <span className="text-suave"> · {formatarDuracao(Number(item.estimativaMinutos))}</span>
            )}
            {mostrarCliente && item.cliente && item.tipo !== 'CONTRATO' && (
                <span className="block truncate text-[10px] text-suave">{item.cliente.nome}</span>
            )}
            <span className="sr-only">
                {' '}
                · {TIPO_LEGIVEL[item.tipo]}
                {situacao ? `, ${situacao}` : ''}
            </span>
        </>
    )
    const props = { className: classe, title: titulo }
    switch (item.tipo) {
        case 'SOLICITACAO':
            return (
                <Link to="/solicitacoes/$id" params={{ id: item.id }} {...props}>
                    {conteudo}
                </Link>
            )
        case 'TAREFA':
            return (
                <Link to="/tarefas/$id" params={{ id: item.id }} {...props}>
                    {conteudo}
                </Link>
            )
        case 'PROJETO':
            return (
                <Link to="/projetos/$id" params={{ id: item.id }} {...props}>
                    {conteudo}
                </Link>
            )
        case 'RECORRENCIA':
            return (
                <Link to="/recorrencias/$id" params={{ id: item.id }} {...props}>
                    {conteudo}
                </Link>
            )
        case 'CONTRATO':
            return (
                <Link to="/contratos/$id" params={{ id: item.id }} {...props}>
                    {conteudo}
                </Link>
            )
    }
}

/**
 * Linha do tempo (F6c): faixas por pessoa ou cliente, 10 dias úteis em colunas, os atrasados numa coluna à esquerda,
 * projetos como barra de ponta a ponta e a coluna de hoje marcada. Na tela larga; no celular, `ListaPorDia`.
 */
export function LinhaDoTempo({
    agenda,
    faixas,
    capacidades,
    porCliente,
}: {
    agenda: Agenda
    faixas: Faixa[]
    capacidades: (CapacidadeSemana | undefined)[]
    porCliente: boolean
}) {
    const colunas = colunasDaJanela(agenda)
    const molde: CSSProperties = {
        gridTemplateColumns: `11rem 6rem repeat(${colunas.length}, minmax(7.5rem, 1fr))`,
    }
    const coluna = (indice: number) => ({ gridColumn: indice === -1 ? 2 : indice + 3 })
    const hojeIndice = colunas.findIndex((c) => c.data === agenda.hoje)

    return (
        <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
            <div role="table" aria-label="Linha do tempo" className="min-w-max">
                <div
                    role="row"
                    className="sticky top-0 z-10 grid border-b border-linha bg-superficie text-xs"
                    style={molde}
                >
                    <span role="columnheader" className="px-3 py-2 font-semibold text-suave">
                        {porCliente ? 'Cliente' : 'Pessoa'}
                    </span>
                    <span role="columnheader" className="px-2 py-2 font-semibold text-suave">
                        Atrasados
                    </span>
                    {colunas.map((d) => (
                        <span
                            role="columnheader"
                            key={d.data}
                            className={`px-2 py-2 ${d.data === agenda.hoje ? 'font-semibold text-acento-texto' : 'text-suave'} ${d.feriado ? 'bg-trilho' : ''}`}
                        >
                            {diaPorExtenso(d.data)}
                            {d.feriado && <span className="block text-[10px]">feriado</span>}
                        </span>
                    ))}
                </div>
                {faixas.map((f) => {
                    const projetos = f.itens.filter((i) => i.tipo === 'PROJETO')
                    const pontuais = f.itens.filter((i) => i.tipo !== 'PROJETO')
                    const carga = capacidades.map((c) => c?.pessoas.find((p) => p.pessoa.id === f.id))
                    return (
                        <div
                            role="row"
                            key={f.id}
                            aria-label={f.nome}
                            className="relative grid border-b border-linha last:border-0"
                            style={molde}
                        >
                            <div
                                role="rowheader"
                                className="flex flex-col gap-0.5 px-3 py-2 text-sm"
                                style={{ gridColumn: 1, gridRow: `1 / span ${projetos.length + 1}` }}
                            >
                                <span className="font-semibold">{f.nome}</span>
                                {!porCliente && carga.some(Boolean) && (
                                    <span className="text-[11px] text-suave">
                                        {carga
                                            .map((c) => (c?.percentual == null ? '—' : `${Number(c.percentual)}%`))
                                            .join(' · ')}{' '}
                                        da capacidade
                                    </span>
                                )}
                            </div>
                            {hojeIndice >= 0 && (
                                <span
                                    aria-hidden="true"
                                    className="pointer-events-none border-l-2 border-acento/60 bg-acento-suave/30"
                                    style={{ gridColumn: hojeIndice + 3, gridRow: `1 / span ${projetos.length + 1}` }}
                                />
                            )}
                            {projetos.map((p, linha) => {
                                const ini = Math.max(indiceDe(p.inicio, colunas), 0)
                                const fim = indiceDe(p.fim, colunas)
                                return (
                                    <div
                                        role="cell"
                                        key={p.id}
                                        className="px-1 pt-1"
                                        style={{
                                            gridColumn: `${ini + 3} / ${Math.max(fim, ini) + 4}`,
                                            gridRow: linha + 1,
                                        }}
                                    >
                                        <ChipItem item={p} />
                                    </div>
                                )
                            })}
                            {[-1, ...colunas.map((_, i) => i)].map((i) => {
                                const doDia = pontuais.filter((item) => indiceDe(item.inicio, colunas) === i)
                                return (
                                    <div
                                        role="cell"
                                        key={i}
                                        className={`flex min-h-12 flex-col gap-1 p-1 ${i >= 0 && colunas[i]!.feriado ? 'bg-trilho/60' : ''}`}
                                        style={{ ...coluna(i), gridRow: projetos.length + 1 }}
                                    >
                                        {doDia.map((item) => (
                                            <ChipItem
                                                key={`${item.tipo}-${item.id}-${item.inicio}`}
                                                item={item}
                                                mostrarCliente={!porCliente}
                                            />
                                        ))}
                                    </div>
                                )
                            })}
                        </div>
                    )
                })}
            </div>
        </div>
    )
}

/** No celular, a mesma janela vira uma lista por dia (decisão 3 do F6), com a faixa de cada item em texto. */
export function ListaPorDia({ agenda, faixas }: { agenda: Agenda; faixas: Faixa[] }) {
    const colunas = colunasDaJanela(agenda)
    const faixaDe = new Map(faixas.flatMap((f) => f.itens.map((i) => [i, f.nome] as const)))
    const grupos = [
        { chave: 'atrasados', titulo: 'Atrasados', indice: -1 },
        ...colunas.map((d, i) => ({
            chave: d.data,
            titulo: `${diaPorExtenso(d.data)}${d.data === agenda.hoje ? ' (hoje)' : ''}${d.feriado ? ' · feriado' : ''}`,
            indice: i,
        })),
    ]
    return (
        <ol aria-label="Agenda por dia" className="flex flex-col gap-4">
            {grupos.map((g) => {
                const itens = agenda.itens.filter((item) =>
                    item.tipo === 'PROJETO'
                        ? g.indice >= Math.max(indiceDe(item.inicio, colunas), 0) &&
                          g.indice <= indiceDe(item.fim, colunas)
                        : indiceDe(item.inicio, colunas) === g.indice,
                )
                if (itens.length === 0 && g.indice === -1) return null
                return (
                    <li key={g.chave} className="flex flex-col gap-1">
                        <h3 className="text-sm font-semibold first-letter:uppercase">{g.titulo}</h3>
                        {itens.length === 0 ? (
                            <p className="text-xs text-suave">Nada.</p>
                        ) : (
                            <ul className="flex flex-col gap-1">
                                {itens.map((item) => (
                                    <li key={`${item.tipo}-${item.id}-${item.inicio}`}>
                                        <ChipItem item={item} />
                                        <span className="text-[11px] text-suave">{faixaDe.get(item)}</span>
                                    </li>
                                ))}
                            </ul>
                        )}
                    </li>
                )
            })}
        </ol>
    )
}
