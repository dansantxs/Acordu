import { useSugestaoResolucao } from '@/features/solicitacoes/sugestaoResolucao'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
    Button,
    GridList,
    GridListItem,
    isTextDropItem,
    Menu,
    MenuItem,
    MenuTrigger,
    Popover,
    useDragAndDrop,
} from 'react-aria-components'
import { useNavigate } from '@tanstack/react-router'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { intervaloDePolling } from '@/shared/config/ambiente'
import { Alerta } from '@/shared/ui/Alerta'
import {
    consultaQuadro,
    DIAS_CONCLUIDAS_NO_QUADRO,
    moverTarefa,
    type Coluna,
    type FiltrosTarefas,
    type TarefaResumo,
} from './consultas'
import { STATUS_TAREFA } from './rotulos'
import { OrigemTarefa, Prazo } from './TelaTarefas'

const COLUNAS: Coluna[] = ['A_FAZER', 'FAZENDO', 'CONCLUIDA']
const TIPO_ARRASTO = 'application/x-acordu-tarefa'

type Pagina = { itens: TarefaResumo[]; total: number | string }

/**
 * Quadro (RF-TAR-02, plano do F3): colunas por status; arrastar muda o status pela máquina de estados e o aviso
 * oferece desfazer (decidido em 20/10). Cancelar fica fora do quadro. O menu "Mover para" de cada cartão faz o mesmo pelo
 * teclado e no toque.
 */
export function Quadro({
    filtros,
    hoje,
    podeEditar,
    podeEditarSolicitacao = false,
}: {
    filtros: FiltrosTarefas
    hoje: string
    podeEditar: boolean
    podeEditarSolicitacao?: boolean
}) {
    const consultas = useQueryClient()
    const sugerirResolucao = useSugestaoResolucao(podeEditarSolicitacao)
    const consulta = consultaQuadro(filtros)
    const quadro = useQuery({ ...consulta, refetchInterval: intervaloDePolling() })

    function colocar(id: string, status: Coluna) {
        consultas.setQueryData<Pagina>(consulta.queryKey, (atual) =>
            atual ? { ...atual, itens: atual.itens.map((t) => (t.id === id ? { ...t, status } : t)) } : atual,
        )
    }

    const movimento = useMutation({
        mutationFn: ({ tarefa, para }: { tarefa: TarefaResumo; para: Coluna; desfazendo?: boolean }) =>
            moverTarefa(tarefa.id, tarefa.status as Coluna, para),
        onMutate: async ({ tarefa, para }) => {
            // Um recarregamento em andamento (polling ou foco) não pode desfazer na tela a movimentação otimista.
            await consultas.cancelQueries({ queryKey: consulta.queryKey })
            colocar(tarefa.id, para)
        },
        onSuccess: (_, { tarefa, para, desfazendo }) => {
            void consultas.invalidateQueries({ queryKey: ['tarefas'] })
            if (desfazendo) return
            // Concluída no quadro e era a última da solicitação: sugere resolver depois do aviso (F11-15).
            if (para === 'CONCLUIDA') void sugerirResolucao(tarefa)
            avisar(`"${tarefa.titulo}" em ${STATUS_TAREFA[para].toLowerCase()}.`, {
                desfazer: () =>
                    movimento.mutate({
                        tarefa: { ...tarefa, status: para },
                        para: tarefa.status as Coluna,
                        desfazendo: true,
                    }),
            })
        },
        onError: (erro, { tarefa }) => {
            colocar(tarefa.id, tarefa.status as Coluna)
            avisarErro(erro)
            void consultas.invalidateQueries({ queryKey: ['tarefas'] })
        },
    })

    const mover = (tarefa: TarefaResumo, para: Coluna) => {
        if (tarefa.status !== para) movimento.mutate({ tarefa, para })
    }

    if (quadro.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (quadro.isError) return <Alerta>{mensagemDeErro(quadro.error)}</Alerta>

    const itens = quadro.data.itens
    return (
        <div className="grid gap-4 md:grid-cols-3">
            {COLUNAS.map((coluna) => (
                <ColunaDoQuadro
                    key={coluna}
                    coluna={coluna}
                    tarefas={itens.filter((t) => t.status === coluna)}
                    todas={itens}
                    hoje={hoje}
                    podeEditar={podeEditar}
                    aoMover={mover}
                />
            ))}
        </div>
    )
}

function ColunaDoQuadro({
    coluna,
    tarefas,
    todas,
    hoje,
    podeEditar,
    aoMover,
}: {
    coluna: Coluna
    tarefas: TarefaResumo[]
    todas: TarefaResumo[]
    hoje: string
    podeEditar: boolean
    aoMover: (tarefa: TarefaResumo, para: Coluna) => void
}) {
    const titulo = STATUS_TAREFA[coluna]
    const navegar = useNavigate()
    const { dragAndDropHooks } = useDragAndDrop({
        isDisabled: !podeEditar,
        getItems: (chaves) =>
            [...chaves].map((chave) => ({ [TIPO_ARRASTO]: String(chave), 'text/plain': String(chave) })),
        acceptedDragTypes: [TIPO_ARRASTO],
        getDropOperation: () => 'move',
        onRootDrop: async (evento) => {
            for (const item of evento.items.filter(isTextDropItem)) {
                const id = await item.getText(TIPO_ARRASTO)
                const tarefa = todas.find((t) => t.id === id)
                if (tarefa) aoMover(tarefa, coluna)
            }
        },
    })

    return (
        <section aria-labelledby={`coluna-${coluna}`} className="flex flex-col gap-2 rounded-couro bg-trilho/60 p-2">
            <h2 id={`coluna-${coluna}`} className="flex items-baseline justify-between px-1 text-sm font-semibold">
                {titulo}
                <span className="text-xs font-normal text-suave">
                    {tarefas.length}
                    {coluna === 'CONCLUIDA' && ` · últimos ${DIAS_CONCLUIDAS_NO_QUADRO} dias`}
                </span>
            </h2>
            <GridList
                aria-labelledby={`coluna-${coluna}`}
                // Enter ou clique no cartão abre o detalhe em painel, mantendo os filtros.
                onAction={(chave) =>
                    void navegar({
                        to: '/tarefas/$id',
                        params: { id: String(chave) },
                        search: (atual: FiltrosTarefas) => atual,
                    })
                }
                items={tarefas}
                dragAndDropHooks={dragAndDropHooks}
                renderEmptyState={() => <p className="px-2 py-6 text-center text-xs text-suave">Nada aqui.</p>}
                className="flex min-h-24 flex-col gap-2 rounded-couro outline-none data-[drop-target]:bg-acento-suave data-[drop-target]:ring-2 data-[drop-target]:ring-acento"
            >
                {(t) => (
                    <GridListItem
                        id={t.id}
                        textValue={t.titulo}
                        data-tarefa={t.id}
                        className="flex flex-col gap-1 rounded-couro border border-linha bg-superficie p-3 text-sm shadow-sm outline-none data-[dragging]:opacity-50 data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento"
                    >
                        <div className="flex items-start gap-2">
                            {podeEditar && (
                                <Button
                                    slot="drag"
                                    aria-label={`Arrastar ${t.titulo}`}
                                    className="mt-0.5 cursor-grab text-suave"
                                >
                                    ⋮⋮
                                </Button>
                            )}
                            <span className="mr-auto font-semibold">{t.titulo}</span>
                            {podeEditar && <MoverPara tarefa={t} aoMover={aoMover} />}
                        </div>
                        <OrigemTarefa tarefa={t} />
                        <div className="flex items-center justify-between gap-2 text-xs text-suave">
                            <span>{t.responsavel?.nome ?? 'Sem responsável'}</span>
                            <Prazo tarefa={t} hoje={hoje} />
                        </div>
                    </GridListItem>
                )}
            </GridList>
        </section>
    )
}

function MoverPara({ tarefa, aoMover }: { tarefa: TarefaResumo; aoMover: (t: TarefaResumo, para: Coluna) => void }) {
    return (
        <MenuTrigger>
            <Button
                aria-label={`Mover ${tarefa.titulo}`}
                className="rounded-couro px-1 text-suave hover:bg-trilho hover:text-tinta"
            >
                ⋯
            </Button>
            <Popover className="min-w-44 rounded-couro border border-linha bg-superficie py-1 shadow-lg">
                <Menu
                    aria-label="Mover para"
                    onAction={(chave) => aoMover(tarefa, chave as Coluna)}
                    className="outline-none"
                >
                    {COLUNAS.filter((c) => c !== tarefa.status).map((c) => (
                        <MenuItem
                            key={c}
                            id={c}
                            className="cursor-default px-3 py-1.5 text-sm outline-none data-[focused]:bg-acento-suave"
                        >
                            Mover para {STATUS_TAREFA[c].toLowerCase()}
                        </MenuItem>
                    ))}
                </Menu>
            </Popover>
        </MenuTrigger>
    )
}
