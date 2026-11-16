import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { Button, Form } from 'react-aria-components'
import { consultaTarefa } from '@/features/tarefas/consultas'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import {
    consultaCronometro,
    decorridoMinutos,
    descartarCronometro,
    iniciarCronometro,
    inicioPelaHora,
    pararCronometro,
    type Cronometro,
} from '@/shared/cronometro/cronometro'
import { formatarDuracao, lerDuracao } from '@/shared/format/duracao'
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
    // Relido a cada 5 min para o "ficou ligado" (F12-17) aparecer sem recarregar a página.
    const cronometro = useQuery({ ...consultaCronometro, refetchInterval: 5 * 60_000 })
    const agora = useAgora()
    const [parando, setParando] = useState(false)
    const c = cronometro.data
    if (!c) return null
    return (
        <div
            role="status"
            aria-label="Cronômetro ativo"
            className={`flex flex-wrap items-center gap-2 rounded-couro px-3 py-2 text-sm text-lateral-tinta ${
                c.esquecido ? 'bg-lateral-cartao ring-2 ring-ambar' : 'bg-lateral-cartao'
            }`}
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
                {c.esquecido ? 'Parar e ajustar' : 'Parar'}
            </Button>
            {c.esquecido && <span className="w-full text-xs font-semibold">Ficou ligado?</span>}
            <JanelaParada cronometro={c} aberta={parando} aoMudar={setParando} />
        </div>
    )
}

/**
 * Botão do cronômetro no detalhe da tarefa: iniciar aqui, ver o tempo e parar se já é desta tarefa, ou avisar que
 * está correndo em outra (um por pessoa; a API responderia 409).
 */
export function BotaoCronometro({ tarefaId, fuso }: { tarefaId: string; fuso: string }) {
    const consultas = useQueryClient()
    const cronometro = useQuery(consultaCronometro)
    const agora = useAgora()
    const [parando, setParando] = useState(false)
    const [antes, setAntes] = useState(false)
    const inicio = useMutation({
        mutationFn: (iniciadoEm?: string) => iniciarCronometro(tarefaId, iniciadoEm),
        // Iniciar põe a tarefa A_FAZER em FAZENDO e pode tirar a solicitação de ABERTA (F11-19): status e versão mudam.
        onSuccess: () =>
            Promise.all(
                ['cronometro', 'tarefas', 'solicitacoes'].map((chave) =>
                    consultas.invalidateQueries({ queryKey: [chave] }),
                ),
            ),
        onError: (erro) => {
            // O erro do horário fica na janela de "Começou antes"; o resto vira aviso.
            if (!antes) avisarErro(erro)
        },
    })
    if (cronometro.isPending) return null
    const c = cronometro.data
    if (c && c.tarefa.id === tarefaId)
        return (
            <>
                <Botao grava variante="secundario" onPress={() => setParando(true)}>
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
        <div className="flex flex-wrap items-center gap-3">
            <Botao
                grava
                variante="secundario"
                onPress={() => inicio.mutate(undefined)}
                isPending={inicio.isPending && !antes}
            >
                Iniciar cronômetro
            </Botao>
            <Botao
                grava
                variante="discreto"
                onPress={() => {
                    inicio.reset()
                    setAntes(true)
                }}
                className="!px-0 text-sm"
            >
                Começou antes?
            </Botao>
            <Janela titulo="Começou antes" aberta={antes} aoMudar={setAntes} ocupada={inicio.isPending}>
                {antes && (
                    <FormularioInicioAntes
                        fuso={fuso}
                        erro={
                            inicio.isError
                                ? (inicio.error instanceof ErroApi && inicio.error.doCampo('iniciadoEm')) ||
                                  mensagemDeErro(inicio.error)
                                : null
                        }
                        ocupado={inicio.isPending}
                        aoFechar={() => setAntes(false)}
                        aoIniciar={(iniciadoEm) => inicio.mutate(iniciadoEm, { onSuccess: () => setAntes(false) })}
                    />
                )}
            </Janela>
        </div>
    )
}

/** "Começou às" (F12-01): hoje ou ontem, até 12 h atrás; a API confere de novo. */
function FormularioInicioAntes({
    fuso,
    erro,
    ocupado,
    aoFechar,
    aoIniciar,
}: {
    fuso: string
    erro: string | null
    ocupado: boolean
    aoFechar: () => void
    aoIniciar: (iniciadoEm: string) => void
}) {
    const [invalida, setInvalida] = useState(false)
    return (
        <Form
            className="flex flex-col gap-4"
            onSubmit={(evento) => {
                evento.preventDefault()
                const hora = String(new FormData(evento.currentTarget).get('hora') ?? '')
                const inicio = inicioPelaHora(hora, fuso)
                setInvalida(inicio === null)
                if (inicio) aoIniciar(inicio.toISOString())
            }}
        >
            {erro && <Alerta>{erro}</Alerta>}
            <div className="flex flex-col gap-1">
                <label htmlFor="cronometro-hora" className="text-sm font-semibold">
                    Começou às
                </label>
                <input
                    id="cronometro-hora"
                    name="hora"
                    type="time"
                    required
                    autoFocus
                    aria-invalid={invalida || undefined}
                    aria-describedby="cronometro-hora-d"
                    className="w-32 rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
                />
                <p id="cronometro-hora-d" className={`text-xs ${invalida ? 'text-vermelho' : 'text-suave'}`}>
                    {invalida
                        ? 'Use o formato 08:40.'
                        : 'No horário da empresa: hoje, ou ontem se ainda não chegou. Até 12 horas atrás.'}
                </p>
            </div>
            <div className="flex justify-end gap-3">
                <Botao variante="secundario" onPress={aoFechar} isDisabled={ocupado}>
                    Cancelar
                </Botao>
                <Botao grava type="submit" isPending={ocupado}>
                    Iniciar cronômetro
                </Botao>
            </div>
        </Form>
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
    // Congelado ao abrir: o campo começa com ele, e só uma mudança da pessoa manda outra duração (F12-05).
    const [minutos] = useState(() => Math.max(decorridoMinutos(c), 1))
    // Esquecido (F12-17): o campo já vem com a duração até o fim do expediente, quando houver.
    const sugerida = c.esquecido && c.sugestaoMinutos != null ? Math.max(Number(c.sugestaoMinutos), 1) : null
    const inicial = formatarDuracao(sugerida ?? minutos)
    const [duracaoInvalida, setDuracaoInvalida] = useState(false)

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
                // Duração (F12-04, F12-05): vem com o decorrido; só vai à API se a pessoa mudou.
                const escrita = String(dados.get('duracao') ?? '').trim()
                const duracao = escrita ? lerDuracao(escrita) : null
                const invalida = duracao === null || duracao < 1 || duracao > 24 * 60
                setDuracaoInvalida(invalida)
                if (invalida) return
                parada.mutate({
                    descricao: texto('descricao'),
                    descricaoExterna: texto('descricaoExterna'),
                    faturavel,
                    visivelCliente: temCliente && visivelEfetivo,
                    // Vai à API quando difere do decorrido: mudada pela pessoa ou a sugerida e mantida.
                    ...(escrita !== formatarDuracao(minutos) ? { duracaoMinutos: duracao } : {}),
                })
            }}
        >
            <p className="text-sm text-suave">
                Em "{c.tarefa.nome}", com a data do dia em que começou. Contou{' '}
                <span className="font-semibold text-tinta tabular-nums">{formatarDuracao(minutos)}</span>.
            </p>
            {sugerida && (
                <Alerta tom="aviso">
                    Parece que ficou ligado: a duração sugerida vai até o fim do expediente do dia em que começou.
                </Alerta>
            )}
            {parada.isError && <Alerta>{mensagemDeErro(parada.error)}</Alerta>}
            <div className="flex flex-col gap-1">
                <label htmlFor="parada-duracao" className="text-sm font-semibold">
                    Duração
                </label>
                <input
                    id="parada-duracao"
                    name="duracao"
                    defaultValue={inicial}
                    aria-invalid={duracaoInvalida || undefined}
                    aria-describedby="parada-duracao-d"
                    className="w-32 rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta tabular-nums aria-[invalid]:border-vermelho"
                />
                <p id="parada-duracao-d" className={`text-xs ${duracaoInvalida ? 'text-vermelho' : 'text-suave'}`}>
                    {duracaoInvalida
                        ? 'De 1 minuto a 24 horas: 1h30, 45min ou 90.'
                        : 'Ajuste se esqueceu de iniciar ou de parar.'}
                </p>
            </div>
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
                <Botao grava type="submit" isPending={parada.isPending}>
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
