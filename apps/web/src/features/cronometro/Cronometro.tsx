import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { Button, Form } from 'react-aria-components'
import { consultaTarefa } from '@/features/tarefas/consultas'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import {
    consultaCronometro,
    decorridoMinutos,
    descartarCronometro,
    iniciarCronometro,
    pararCronometro,
    type Cronometro,
} from '@/shared/cronometro/cronometro'
import { formatarDuracao } from '@/shared/format/duracao'
import { useAgora } from '@/shared/hooks/useAgora'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { Janela } from '@/shared/ui/Janela'
import { Marcador } from '@/shared/ui/Marcador'

/** Depois de parar ou descartar: o cronômetro some e as horas da tarefa e das listas mudam. */
async function atualizar(consultas: ReturnType<typeof useQueryClient>) {
    await Promise.all([
        consultas.invalidateQueries({ queryKey: ['cronometro'] }),
        consultas.invalidateQueries({ queryKey: ['tarefas'] }),
        consultas.invalidateQueries({ queryKey: ['apontamentos'] }),
        consultas.invalidateQueries({ queryKey: ['solicitacoes'] }),
    ])
}

/**
 * Cronômetro ativo na capa lateral (plano do F3, decisão 4): visível em qualquer tela, com o tempo correndo, a tarefa
 * e o botão de parar. Some quando não há cronômetro.
 */
export function IndicadorCronometro() {
    const cronometro = useQuery(consultaCronometro)
    const agora = useAgora()
    const [parando, setParando] = useState(false)
    const c = cronometro.data
    if (!c) return null
    return (
        <div
            role="status"
            aria-label="Cronômetro ativo"
            className="flex items-center gap-2 rounded-couro bg-lateral-cartao px-3 py-2 text-sm text-lateral-tinta"
        >
            <span aria-hidden="true" className="size-2 shrink-0 animate-pulse rounded-full bg-vermelho" />
            <span className="min-w-0 flex-1">
                <span className="block font-semibold tabular-nums">{formatarDuracao(decorridoMinutos(c, agora))}</span>
                <Link
                    to="/tarefas/$id"
                    params={{ id: c.tarefa.id }}
                    className="block truncate text-xs text-lateral-suave hover:underline"
                >
                    {c.tarefa.nome}
                </Link>
            </span>
            <Button
                onPress={() => setParando(true)}
                className="rounded-couro border border-lateral-linha px-2 py-1 text-xs font-semibold hover:bg-lateral-ativo"
            >
                Parar
            </Button>
            <JanelaParada cronometro={c} aberta={parando} aoMudar={setParando} />
        </div>
    )
}

/**
 * Botão do cronômetro no detalhe da tarefa: iniciar aqui, ver o tempo e parar se já é desta tarefa, ou avisar que
 * está correndo em outra (um por pessoa; a API responderia 409).
 */
export function BotaoCronometro({ tarefaId }: { tarefaId: string }) {
    const consultas = useQueryClient()
    const cronometro = useQuery(consultaCronometro)
    const agora = useAgora()
    const [parando, setParando] = useState(false)
    const inicio = useMutation({
        mutationFn: () => iniciarCronometro(tarefaId),
        // Iniciar põe a tarefa A_FAZER em FAZENDO e pode tirar a solicitação de ABERTA (F11-19): status e versão mudam.
        onSuccess: () =>
            Promise.all(
                ['cronometro', 'tarefas', 'solicitacoes'].map((chave) =>
                    consultas.invalidateQueries({ queryKey: [chave] }),
                ),
            ),
        onError: avisarErro,
    })
    if (cronometro.isPending) return null
    const c = cronometro.data
    if (c && c.tarefa.id === tarefaId)
        return (
            <>
                <Botao variante="secundario" onPress={() => setParando(true)}>
                    <span aria-hidden="true" className="size-2 animate-pulse rounded-full bg-vermelho" />
                    Parar cronômetro · {formatarDuracao(decorridoMinutos(c, agora))}
                </Botao>
                <JanelaParada cronometro={c} aberta={parando} aoMudar={setParando} />
            </>
        )
    if (c)
        return (
            <p className="text-xs text-suave">
                Cronômetro correndo em "{c.tarefa.nome}". Pare aquele para começar aqui.
            </p>
        )
    return (
        <Botao variante="secundario" onPress={() => inicio.mutate()} isPending={inicio.isPending}>
            Iniciar cronômetro
        </Botao>
    )
}

/**
 * Parar abre um formulário curto já preenchido (decisão 4 do F3): descrição interna, descrição para o cliente,
 * faturável e visível. Enter grava. Descartar não aponta nada e pede confirmação.
 */
export function JanelaParada({
    cronometro: c,
    aberta,
    aoMudar,
}: {
    cronometro: Cronometro
    aberta: boolean
    aoMudar: (aberta: boolean) => void
}) {
    return (
        <Janela titulo="Parar cronômetro" aberta={aberta} aoMudar={aoMudar}>
            {aberta && <FormularioParada cronometro={c} aoFechar={() => aoMudar(false)} />}
        </Janela>
    )
}

function FormularioParada({ cronometro: c, aoFechar }: { cronometro: Cronometro; aoFechar: () => void }) {
    const consultas = useQueryClient()
    const tarefa = useQuery(consultaTarefa(c.tarefa.id))
    const temCliente = Boolean(tarefa.data?.clienteId)
    const [faturavel, setFaturavel] = useState(true)
    // Visível segue o faturável até a pessoa mexer (RF-APT-04: padrão visível se faturável).
    const [visivel, setVisivel] = useState<boolean | null>(null)
    const visivelEfetivo = visivel ?? faturavel
    const [descartando, setDescartando] = useState(false)
    const minutos = Math.max(decorridoMinutos(c), 1)

    const parada = useMutation({
        mutationFn: pararCronometro,
        onSuccess: async (apontamento) => {
            await atualizar(consultas)
            avisar(`${formatarDuracao(Number(apontamento.duracaoMinutos))} apontadas em "${c.tarefa.nome}".`)
            aoFechar()
        },
    })
    const descarte = useMutation({
        mutationFn: descartarCronometro,
        onSuccess: async () => {
            await atualizar(consultas)
            setDescartando(false)
            avisar('Cronômetro descartado, sem apontar.')
            aoFechar()
        },
    })

    return (
        <Form
            className="flex flex-col gap-4"
            onSubmit={(evento) => {
                evento.preventDefault()
                const dados = new FormData(evento.currentTarget)
                const texto = (nome: string) => String(dados.get(nome) ?? '').trim() || null
                parada.mutate({
                    descricao: texto('descricao'),
                    descricaoExterna: texto('descricaoExterna'),
                    faturavel,
                    visivelCliente: temCliente && visivelEfetivo,
                })
            }}
        >
            <p className="text-sm">
                <span className="font-semibold tabular-nums">{formatarDuracao(minutos)}</span>{' '}
                <span className="text-suave">em "{c.tarefa.nome}", com a data de hoje.</span>
            </p>
            {parada.isError && <Alerta>{mensagemDeErro(parada.error)}</Alerta>}
            <CampoTexto
                name="descricao"
                rotulo="O que foi feito"
                defaultValue={c.tarefa.nome}
                maxLength={2_000}
                autoFocus
                descricao="Para a equipe."
            />
            {temCliente && (
                <CampoTexto
                    name="descricaoExterna"
                    rotulo="Descrição para o cliente"
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
            <div className="flex flex-wrap items-center justify-end gap-3">
                <Button
                    onPress={() => setDescartando(true)}
                    isDisabled={parada.isPending}
                    className="mr-auto text-sm text-suave hover:text-vermelho hover:underline"
                >
                    Descartar sem apontar
                </Button>
                <Botao variante="secundario" onPress={aoFechar} isDisabled={parada.isPending}>
                    Continuar contando
                </Botao>
                <Botao type="submit" isPending={parada.isPending}>
                    Parar e apontar
                </Botao>
            </div>
            <Confirmacao
                titulo="Descartar o cronômetro?"
                acao="Descartar"
                aberta={descartando}
                aoMudar={setDescartando}
                aoConfirmar={() => descarte.mutate()}
                ocupada={descarte.isPending}
                erro={descarte.isError ? mensagemDeErro(descarte.error) : null}
            >
                <p>O tempo contado ({formatarDuracao(minutos)}) é perdido e nada é apontado.</p>
            </Confirmacao>
        </Form>
    )
}
