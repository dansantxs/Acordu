import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import type { CSSProperties } from 'react'
import {
    Button,
    GridList,
    GridListItem,
    Header,
    isTextDropItem,
    Menu,
    MenuItem,
    MenuSection,
    MenuTrigger,
    Popover,
    Separator,
    useDragAndDrop,
} from 'react-aria-components'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { diaPorExtenso, somarDias } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import type { Agenda, CapacidadeSemana, ItemAgenda } from './consultas'
import { planejarTarefa } from './consultas'
import { ChipItem } from './LinhaDoTempo'

const TIPO_ARRASTO = 'application/x-acordu-tarefa-agenda'
const SEM_RESPONSAVEL = 'ninguem'
const ATRASADAS = 'atrasadas'

interface Coluna {
    id: string
    nome: string
}

interface Destino {
    dia: string
    pessoa: string
}

/**
 * Plano da semana (F6d, decisão 4): uma coluna por pessoa (mais "Sem responsável") e uma linha por dia útil. Arrastar
 * uma tarefa muda o dia planejado e, entre colunas, o responsável, com desfazer no aviso; o menu "Mover para" faz o
 * mesmo pelo teclado e no toque. Solicitações e recorrências aparecem, mas não se movem (prazo do SLA e regra).
 */
export function PlanoDaSemana({
    agenda,
    inicio,
    capacidade,
    podeEditar,
}: {
    agenda: Agenda
    inicio: string
    capacidade: CapacidadeSemana | undefined
    podeEditar: boolean
}) {
    const consultas = useQueryClient()
    const chave = ['agenda', inicio]
    const dias = [0, 1, 2, 3, 4].map((i) => somarDias(inicio, i))
    const sexta = dias[4]!
    const naEquipe = new Set(agenda.equipe.map((p) => p.id))
    const colunas: Coluna[] = [...agenda.equipe, { id: SEM_RESPONSAVEL, nome: 'Sem responsável' }]
    const pessoaDe = (i: ItemAgenda) =>
        i.responsavel && naEquipe.has(i.responsavel.id) ? i.responsavel.id : SEM_RESPONSAVEL
    const daSemana = agenda.itens.filter(
        (i) => i.tipo === 'TAREFA' || i.tipo === 'SOLICITACAO' || i.tipo === 'RECORRENCIA',
    )
    // Fim de semana cai na sexta, como na linha do tempo; antes da semana, na linha "Atrasadas".
    const linhaDe = (i: ItemAgenda) =>
        i.inicio < inicio ? ATRASADAS : i.inicio > sexta ? (i.inicio <= somarDias(inicio, 6) ? sexta : null) : i.inicio
    const doCruzamento = (linha: string, pessoa: string) =>
        daSemana.filter((i) => linhaDe(i) === linha && pessoaDe(i) === pessoa)

    function colocar(id: string, destino: Destino, nome: string | null) {
        consultas.setQueryData<Agenda>(chave, (atual) =>
            atual
                ? {
                      ...atual,
                      itens: atual.itens.map((i) =>
                          i.tipo === 'TAREFA' && i.id === id
                              ? {
                                    ...i,
                                    inicio: destino.dia,
                                    fim: destino.dia,
                                    responsavel:
                                        destino.pessoa === SEM_RESPONSAVEL
                                            ? null
                                            : { id: destino.pessoa, nome: nome ?? '' },
                                }
                              : i,
                      ),
                  }
                : atual,
        )
    }

    const movimento = useMutation({
        mutationFn: ({
            tarefa,
            destino,
        }: {
            tarefa: ItemAgenda
            destino: { planejadaPara: string | null; responsavelId: string | null }
            desfazendo?: boolean
        }) => planejarTarefa(tarefa.id, destino),
        onMutate: ({ tarefa, destino }) => {
            if (destino.planejadaPara)
                colocar(
                    tarefa.id,
                    { dia: destino.planejadaPara, pessoa: destino.responsavelId ?? SEM_RESPONSAVEL },
                    colunas.find((c) => c.id === destino.responsavelId)?.nome ?? null,
                )
        },
        onSuccess: (antes, { tarefa, destino, desfazendo }) => {
            void consultas.invalidateQueries({ queryKey: ['agenda'] })
            void consultas.invalidateQueries({ queryKey: ['tarefas'] })
            if (desfazendo) return
            const pessoa = colunas.find((c) => c.id === (destino.responsavelId ?? SEM_RESPONSAVEL))?.nome
            avisar(`"${tarefa.titulo}" planejada para ${diaPorExtenso(destino.planejadaPara!)} (${pessoa}).`, {
                desfazer: () => movimento.mutate({ tarefa, destino: antes, desfazendo: true }),
            })
        },
        onError: (erro) => {
            avisarErro(erro)
            void consultas.invalidateQueries({ queryKey: ['agenda'] })
        },
    })

    const mover = (tarefa: ItemAgenda, destino: Destino) => {
        if (tarefa.tipo !== 'TAREFA') return
        if (linhaDe(tarefa) === destino.dia && pessoaDe(tarefa) === destino.pessoa) return
        movimento.mutate({
            tarefa,
            destino: {
                planejadaPara: destino.dia,
                responsavelId: destino.pessoa === SEM_RESPONSAVEL ? null : destino.pessoa,
            },
        })
    }

    const molde: CSSProperties = { gridTemplateColumns: `7rem repeat(${colunas.length}, minmax(10rem, 1fr))` }
    const atrasadas = daSemana.some((i) => linhaDe(i) === ATRASADAS)
    const linhas = [...(atrasadas ? [ATRASADAS] : []), ...dias]

    return (
        <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
            <div role="table" aria-label="Plano da semana" className="min-w-max">
                <div
                    role="row"
                    className="sticky top-0 z-10 grid border-b border-linha bg-superficie text-xs"
                    style={molde}
                >
                    <span role="columnheader" className="px-2 py-2 text-suave">
                        Dia
                    </span>
                    {colunas.map((c) => {
                        const carga = capacidade?.pessoas.find((p) => p.pessoa.id === c.id)
                        return (
                            <span role="columnheader" key={c.id} className="px-2 py-2">
                                <span className="block font-semibold">{c.nome}</span>
                                {carga && (
                                    <span className={Number(carga.percentual) > 100 ? 'text-vermelho' : 'text-suave'}>
                                        {formatarDuracao(Number(carga.carga.planejadoMinutos))} de{' '}
                                        {formatarDuracao(Number(carga.capacidadeMinutos))}
                                    </span>
                                )}
                            </span>
                        )
                    })}
                </div>
                {linhas.map((linha) => {
                    const feriado = agenda.dias.find((d) => d.data === linha)?.feriado
                    return (
                        <div
                            role="row"
                            key={linha}
                            className={`grid border-b border-linha last:border-0 ${linha === agenda.hoje ? 'bg-acento-suave/30' : ''}`}
                            style={molde}
                        >
                            <span role="rowheader" className="px-2 py-2 text-xs">
                                <span
                                    className={`block first-letter:uppercase ${linha === agenda.hoje ? 'font-semibold text-acento-texto' : ''}`}
                                >
                                    {linha === ATRASADAS ? 'Atrasadas' : diaPorExtenso(linha)}
                                </span>
                                {feriado && <span className="text-suave">feriado</span>}
                            </span>
                            {colunas.map((c) => (
                                <Celula
                                    key={c.id}
                                    linha={linha}
                                    coluna={c}
                                    itens={doCruzamento(linha, c.id)}
                                    todas={daSemana}
                                    dias={dias}
                                    colunas={colunas}
                                    planejado={
                                        capacidade?.pessoas
                                            .find((p) => p.pessoa.id === c.id)
                                            ?.carga.porDia.find((d) => d.data === linha)?.planejadoMinutos
                                    }
                                    podeEditar={podeEditar}
                                    aoMover={mover}
                                />
                            ))}
                        </div>
                    )
                })}
            </div>
        </div>
    )
}

function Celula({
    linha,
    coluna,
    itens,
    todas,
    dias,
    colunas,
    planejado,
    podeEditar,
    aoMover,
}: {
    linha: string
    coluna: Coluna
    itens: ItemAgenda[]
    todas: ItemAgenda[]
    dias: string[]
    colunas: Coluna[]
    planejado: number | string | undefined
    podeEditar: boolean
    aoMover: (tarefa: ItemAgenda, destino: Destino) => void
}) {
    const navegar = useNavigate()
    const tarefas = itens.filter((i) => i.tipo === 'TAREFA')
    const fixos = itens.filter((i) => i.tipo !== 'TAREFA')
    const recebe = linha !== ATRASADAS && podeEditar
    const rotulo = `${coluna.nome}, ${linha === ATRASADAS ? 'atrasadas' : diaPorExtenso(linha)}`
    const { dragAndDropHooks } = useDragAndDrop({
        isDisabled: !podeEditar,
        getItems: (chaves) =>
            [...chaves].map((chave) => ({ [TIPO_ARRASTO]: String(chave), 'text/plain': String(chave) })),
        acceptedDragTypes: [TIPO_ARRASTO],
        getDropOperation: () => (recebe ? 'move' : 'cancel'),
        onRootDrop: async (evento) => {
            if (!recebe) return
            for (const item of evento.items.filter(isTextDropItem)) {
                const id = await item.getText(TIPO_ARRASTO)
                const tarefa = todas.find((t) => t.tipo === 'TAREFA' && t.id === id)
                if (tarefa) aoMover(tarefa, { dia: linha, pessoa: coluna.id })
            }
        },
    })
    return (
        <div role="cell" className="flex min-h-16 flex-col gap-1 border-l border-linha p-1">
            {planejado != null && Number(planejado) > 0 && (
                <span className="text-right text-[10px] text-suave tabular-nums">
                    {formatarDuracao(Number(planejado))}
                </span>
            )}
            {fixos.map((i) => (
                <ChipItem key={`${i.tipo}-${i.id}-${i.inicio}`} item={i} />
            ))}
            <GridList
                aria-label={rotulo}
                items={tarefas}
                dragAndDropHooks={dragAndDropHooks}
                onAction={(chave) => void navegar({ to: '/tarefas/$id', params: { id: String(chave) } })}
                renderEmptyState={() => <span className="block min-h-6" />}
                className="flex flex-1 flex-col gap-1 rounded-couro outline-none data-[drop-target]:bg-acento-suave data-[drop-target]:ring-2 data-[drop-target]:ring-acento"
            >
                {(t) => (
                    <GridListItem
                        id={t.id}
                        textValue={t.titulo}
                        className={`flex items-start gap-1 rounded-couro border-l-4 bg-superficie px-1.5 py-1 text-xs shadow-sm outline-none data-[dragging]:opacity-50 data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento ${t.situacao === 'VENCIDO' ? 'border-vermelho' : 'border-linha'}`}
                    >
                        {podeEditar && (
                            <Button slot="drag" aria-label={`Arrastar ${t.titulo}`} className="cursor-grab text-suave">
                                ⋮⋮
                            </Button>
                        )}
                        <span className="min-w-0 flex-1">
                            <span className="block truncate">{t.titulo}</span>
                            {(t.estimativaMinutos != null || t.cliente) && (
                                <span className="block truncate text-[10px] text-suave">
                                    {t.estimativaMinutos != null && formatarDuracao(Number(t.estimativaMinutos))}
                                    {t.estimativaMinutos != null && t.cliente && ' · '}
                                    {t.cliente?.nome}
                                </span>
                            )}
                        </span>
                        {podeEditar && (
                            <MoverPara
                                tarefa={t}
                                dias={dias}
                                colunas={colunas}
                                atual={{ dia: linha, pessoa: coluna.id }}
                                aoMover={aoMover}
                            />
                        )}
                    </GridListItem>
                )}
            </GridList>
        </div>
    )
}

function MoverPara({
    tarefa,
    dias,
    colunas,
    atual,
    aoMover,
}: {
    tarefa: ItemAgenda
    dias: string[]
    colunas: Coluna[]
    atual: Destino
    aoMover: (tarefa: ItemAgenda, destino: Destino) => void
}) {
    const item = 'cursor-default px-3 py-1.5 text-sm outline-none data-[focused]:bg-trilho'
    return (
        <MenuTrigger>
            <Button aria-label={`Mover ${tarefa.titulo}`} className="rounded-couro px-1 text-suave hover:bg-trilho">
                ⋯
            </Button>
            <Popover className="max-h-96 min-w-48 overflow-y-auto rounded-couro border border-linha bg-superficie py-1 shadow-lg">
                <Menu
                    aria-label="Mover para"
                    className="outline-none"
                    onAction={(chave) => {
                        const [tipo, valor] = String(chave).split(':') as [string, string]
                        // Sair de "Atrasadas" sem escolher dia vai para o primeiro dia da semana.
                        const dia = atual.dia === ATRASADAS ? dias[0]! : atual.dia
                        aoMover(tarefa, tipo === 'dia' ? { dia: valor, pessoa: atual.pessoa } : { dia, pessoa: valor })
                    }}
                >
                    <MenuSection>
                        <Header className="px-3 pt-1 pb-0.5 text-xs font-semibold text-suave">Dia</Header>
                        {dias
                            .filter((d) => d !== atual.dia)
                            .map((d) => (
                                <MenuItem key={d} id={`dia:${d}`} className={item}>
                                    <span className="first-letter:uppercase">{diaPorExtenso(d)}</span>
                                </MenuItem>
                            ))}
                    </MenuSection>
                    <Separator className="my-1 border-t border-linha" />
                    <MenuSection>
                        <Header className="px-3 pt-1 pb-0.5 text-xs font-semibold text-suave">Pessoa</Header>
                        {colunas
                            .filter((c) => c.id !== atual.pessoa)
                            .map((c) => (
                                <MenuItem key={c.id} id={`pessoa:${c.id}`} className={item}>
                                    {c.nome}
                                </MenuItem>
                            ))}
                    </MenuSection>
                </Menu>
            </Popover>
        </MenuTrigger>
    )
}
