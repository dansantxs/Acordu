import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { Form } from 'react-aria-components'
import { CampoTarefa } from '@/features/tarefas/CampoTarefa'
import type { TarefaResumo } from '@/features/tarefas/consultas'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { hojeNoFuso } from '@/shared/format/datas'
import { formatarDuracao, lerDuracao } from '@/shared/format/duracao'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao, BotaoTexto } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { Janela } from '@/shared/ui/Janela'
import { Marcador } from '@/shared/ui/Marcador'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import { criarApontamento, editarApontamento, excluirApontamento, type Apontamento } from './consultas'

/** A tarefa já conhecida (lançar pelo painel dela) ou nenhuma (escolher na tela "Horas trabalhadas"). */
export interface TarefaDoApontamento {
    id: string
    titulo: string
    temCliente: boolean
}

async function atualizar(consultas: ReturnType<typeof useQueryClient>) {
    await Promise.all([
        consultas.invalidateQueries({ queryKey: ['apontamentos'] }),
        consultas.invalidateQueries({ queryKey: ['tarefas'] }),
        consultas.invalidateQueries({ queryKey: ['solicitacoes'] }),
    ])
}

/**
 * Lançar ou editar horas (RF-APT-01/04): data, duração num campo só ("1h30", decisão 5 do F3), o que foi feito,
 * descrição para o cliente, faturável e visível (segue o faturável até a pessoa mexer). Período fechado volta como
 * erro da API (422).
 */
export function JanelaApontamento({
    fuso,
    aberta,
    aoMudar,
    tarefa,
    apontamento,
}: {
    fuso: string
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    /** Fixa a tarefa (lançar pelo painel dela). Sem ela e sem apontamento, a janela pede a tarefa. */
    tarefa?: TarefaDoApontamento
    /** Editar este apontamento. */
    apontamento?: Apontamento
}) {
    return (
        <Janela titulo={apontamento ? 'Editar horas' : 'Lançar horas'} aberta={aberta} aoMudar={aoMudar} larga>
            {aberta && (
                <Formulario fuso={fuso} tarefa={tarefa} apontamento={apontamento} aoFechar={() => aoMudar(false)} />
            )}
        </Janela>
    )
}

function Formulario({
    fuso,
    tarefa,
    apontamento: a,
    aoFechar,
}: {
    fuso: string
    tarefa?: TarefaDoApontamento
    apontamento?: Apontamento
    aoFechar: () => void
}) {
    const consultas = useQueryClient()
    const [escolhida, setEscolhida] = useState<TarefaResumo | null>(null)
    const [faturavel, setFaturavel] = useState(a?.faturavel ?? true)
    const [visivel, setVisivel] = useState<boolean | null>(a ? a.visivelCliente : null)
    const [duracaoInvalida, setDuracaoInvalida] = useState(false)
    const temCliente = a ? a.clienteId !== null : tarefa ? tarefa.temCliente : escolhida?.clienteId != null
    const visivelEfetivo = visivel ?? faturavel

    const envio = useMutation({
        mutationFn: async (dados: {
            data: string
            duracaoMinutos: number
            descricao: string
            descricaoExterna: string | null
        }) => {
            const comuns = { ...dados, faturavel, visivelCliente: temCliente && visivelEfetivo }
            if (a) return editarApontamento(a, comuns)
            const tarefaId = tarefa?.id ?? escolhida?.id
            if (!tarefaId) throw new Error('Escolha a tarefa.')
            return criarApontamento({ ...comuns, tarefaId })
        },
        onSuccess: async (salvo) => {
            await atualizar(consultas)
            avisar(
                a
                    ? 'Horas atualizadas.'
                    : `${formatarDuracao(Number(salvo.duracaoMinutos))} apontadas em "${salvo.tarefa.nome}".`,
            )
            aoFechar()
        },
    })

    return (
        <Form
            className="flex flex-col gap-4"
            onSubmit={(evento) => {
                evento.preventDefault()
                const dados = new FormData(evento.currentTarget)
                const duracao = lerDuracao(String(dados.get('duracao') ?? ''))
                setDuracaoInvalida(duracao === null)
                if (duracao === null) return
                envio.mutate({
                    data: String(dados.get('data')),
                    duracaoMinutos: duracao,
                    descricao: String(dados.get('descricao')).trim(),
                    descricaoExterna: String(dados.get('descricaoExterna') ?? '').trim() || null,
                })
            }}
        >
            <LegendaObrigatorios />
            {envio.isError && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
            {a ? (
                <p className="text-sm text-suave">Em "{a.tarefa.nome}"</p>
            ) : tarefa ? (
                <p className="text-sm text-suave">Em "{tarefa.titulo}"</p>
            ) : (
                <CampoTarefa rotulo="Tarefa" valor={escolhida} aoMudar={setEscolhida} isRequired />
            )}
            <div className="grid gap-4 sm:grid-cols-2">
                <div className="flex flex-col gap-1">
                    <label htmlFor="apontamento-data" className="text-sm font-semibold">
                        Data
                        <span aria-hidden="true" className="text-acento-texto">
                            {' '}
                            *
                        </span>
                    </label>
                    <input
                        id="apontamento-data"
                        name="data"
                        type="date"
                        required
                        defaultValue={a?.data ?? hojeNoFuso(fuso)}
                        className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta"
                    />
                </div>
                <div className="flex flex-col gap-1">
                    <label htmlFor="apontamento-duracao" className="text-sm font-semibold">
                        Duração
                        <span aria-hidden="true" className="text-acento-texto">
                            {' '}
                            *
                        </span>
                    </label>
                    <input
                        id="apontamento-duracao"
                        name="duracao"
                        required
                        autoFocus={Boolean(tarefa) || Boolean(a)}
                        placeholder="1h30"
                        defaultValue={a ? formatarDuracao(Number(a.duracaoMinutos)) : ''}
                        aria-invalid={duracaoInvalida || undefined}
                        aria-describedby="apontamento-duracao-d"
                        className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
                    />
                    <p
                        id="apontamento-duracao-d"
                        className={`text-xs ${duracaoInvalida ? 'text-vermelho' : 'text-suave'}`}
                    >
                        {duracaoInvalida ? 'Não entendi. Use 1h30, 1:30 ou 90.' : '1h30, 1:30 ou 90 (minutos).'}
                    </p>
                </div>
            </div>
            <CampoTexto
                name="descricao"
                rotulo="O que foi feito"
                defaultValue={a?.descricao ?? ''}
                maxLength={2_000}
                isRequired
                descricao="Para a equipe."
            />
            {temCliente && (
                <CampoTexto
                    name="descricaoExterna"
                    rotulo="Descrição para o cliente"
                    defaultValue={a?.descricaoExterna ?? ''}
                    maxLength={2_000}
                    descricao="Aparece no extrato do portal. Vazia, o cliente vê a descrição acima."
                />
            )}
            <div className="flex flex-wrap gap-x-6 gap-y-2">
                <Marcador valor={faturavel} aoMudar={setFaturavel}>
                    Faturável (consome o contrato)
                </Marcador>
                {temCliente && (
                    <Marcador valor={visivelEfetivo} aoMudar={setVisivel}>
                        Visível ao cliente
                    </Marcador>
                )}
            </div>
            <div className="flex justify-end gap-3">
                <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                    Cancelar
                </Botao>
                <Botao grava type="submit" isPending={envio.isPending}>
                    {a ? 'Salvar' : 'Lançar'}
                </Botao>
            </div>
        </Form>
    )
}

/**
 * Linhas de apontamento com editar e excluir para quem pode (o autor, ou `apontamento.editar_todos`). Excluir pede
 * confirmação (não tem volta). `mostrarTarefa` é para a tela "Horas trabalhadas"; no painel da tarefa ela já é conhecida.
 */
export function LinhasApontamento({
    itens,
    fuso,
    usuarioId,
    podeEditarTodos,
    podeCriar,
    mostrarTarefa = false,
    mostrarPessoa = false,
    mostrarData = false,
}: {
    itens: Apontamento[]
    fuso: string
    usuarioId: string
    podeEditarTodos: boolean
    podeCriar: boolean
    mostrarTarefa?: boolean
    mostrarPessoa?: boolean
    /** A data mesmo com a tarefa visível (horas do mês no contrato). */
    mostrarData?: boolean
}) {
    const consultas = useQueryClient()
    const [editando, setEditando] = useState<Apontamento | null>(null)
    const [excluindo, setExcluindo] = useState<Apontamento | null>(null)
    const exclusao = useMutation({
        mutationFn: (a: Apontamento) => excluirApontamento(a.id),
        onSuccess: async () => {
            await atualizar(consultas)
            setExcluindo(null)
            avisar('Apontamento excluído.')
        },
    })
    const podeMexer = (a: Apontamento) => podeCriar && (a.usuario.id === usuarioId || podeEditarTodos)

    return (
        <>
            <ul className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                {itens.map((a) => (
                    <li key={a.id} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 px-3 py-2">
                        <span className="w-14 font-semibold tabular-nums">
                            {formatarDuracao(Number(a.duracaoMinutos))}
                        </span>
                        <span className="min-w-0 flex-1">
                            {mostrarTarefa && (
                                <Link
                                    to="/tarefas/$id"
                                    params={{ id: a.tarefa.id }}
                                    className="block font-semibold hover:underline"
                                >
                                    {a.tarefa.nome}
                                </Link>
                            )}
                            <span className="block">{a.descricao}</span>
                            {a.descricaoExterna && (
                                <span className="block text-xs text-suave">Cliente vê: {a.descricaoExterna}</span>
                            )}
                        </span>
                        <span className="flex items-center gap-2 text-xs text-suave">
                            {mostrarPessoa && <span>{a.usuario.nome}</span>}
                            {(mostrarData || !mostrarTarefa) && (
                                <span>{a.data.split('-').reverse().slice(0, 2).join('/')}</span>
                            )}
                            <span>{a.faturavel ? 'faturável' : 'não faturável'}</span>
                            {podeMexer(a) && (
                                <>
                                    <BotaoTexto
                                        grava
                                        onPress={() => setEditando(a)}
                                        className="text-acento-texto hover:underline"
                                        aria-label={`Editar ${formatarDuracao(Number(a.duracaoMinutos))} de ${a.descricao}`}
                                    >
                                        Editar
                                    </BotaoTexto>
                                    <BotaoTexto
                                        grava
                                        onPress={() => {
                                            exclusao.reset()
                                            setExcluindo(a)
                                        }}
                                        className="hover:text-vermelho hover:underline"
                                        aria-label={`Excluir ${formatarDuracao(Number(a.duracaoMinutos))} de ${a.descricao}`}
                                    >
                                        Excluir
                                    </BotaoTexto>
                                </>
                            )}
                        </span>
                    </li>
                ))}
            </ul>
            <JanelaApontamento
                fuso={fuso}
                aberta={editando !== null}
                aoMudar={(aberta) => !aberta && setEditando(null)}
                apontamento={editando ?? undefined}
            />
            <Confirmacao
                titulo="Excluir este apontamento?"
                acao="Excluir apontamento"
                aberta={excluindo !== null}
                aoMudar={(aberta) => !aberta && setExcluindo(null)}
                aoConfirmar={() => excluindo && exclusao.mutate(excluindo)}
                ocupada={exclusao.isPending}
                erro={exclusao.isError ? mensagemDeErro(exclusao.error) : null}
            >
                <p>
                    {excluindo && `${formatarDuracao(Number(excluindo.duracaoMinutos))} de "${excluindo.descricao}"`}{' '}
                    saem das horas e do saldo do contrato. Não dá para desfazer.
                </p>
            </Confirmacao>
        </>
    )
}
