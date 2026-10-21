import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { Form, Radio, RadioGroup } from 'react-aria-components'
import { LinhasApontamento } from '@/features/apontamentos/Apontamentos'
import { consultaApontamentos } from '@/features/apontamentos/consultas'
import { pode } from '@/features/casca/navegacao'
import { PRIORIDADE, TIPO_CONTRATO, type Prioridade } from '@/features/solicitacoes/rotulos'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { dataCurta, fimDoMes, hojeNoFuso, mesAtual, nomeDoMes, somarMeses } from '@/shared/format/datas'
import { formatarDuracao, lerDuracao } from '@/shared/format/duracao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { Icone } from '@/shared/ui/Icone'
import { Janela } from '@/shared/ui/Janela'
import { Pagina } from '@/shared/ui/Pagina'
import { Dado } from '@/shared/ui/PainelLateral'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import {
    ajustarSaldo,
    consultaConsumo,
    consultaContrato,
    ESTILO_STATUS_CONTRATO,
    fecharPeriodo,
    STATUS_CONTRATO,
    transicionarContrato,
    type AcaoContrato,
    type Consumo,
    type Contrato,
} from './consultas'
import { JanelaCadastroContrato } from './JanelaCadastroContrato'

const PRIORIDADES: Prioridade[] = ['URGENTE', 'ALTA', 'MEDIA', 'BAIXA']

/**
 * Detalhe do contrato (F4a, página própria): dados, consumo por mês com navegação, fechamento do mês, ajuste de saldo
 * do banco de horas, SLA e as horas do mês. Suspender e reativar valem na hora; encerrar pede confirmação (é final).
 */
export function PaginaContrato({ perfil, id, mes }: { perfil: Perfil; id: string; mes?: string }) {
    const consulta = useQuery(consultaContrato(id))
    if (consulta.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (consulta.isError) throw consulta.error
    return <Detalhe contrato={consulta.data} perfil={perfil} mes={mes ?? mesAtual(perfil.tenant.fusoHorario)} />
}

function Detalhe({ contrato: c, perfil, mes }: { contrato: Contrato; perfil: Perfil; mes: string }) {
    const consultas = useQueryClient()
    const navegar = useNavigate()
    const fuso = perfil.tenant.fusoHorario
    const hoje = hojeNoFuso(fuso)
    const [editando, setEditando] = useState(false)
    const [encerrando, setEncerrando] = useState(false)
    const [ajustando, setAjustando] = useState(false)
    const gerencia = pode(perfil, 'contrato.gerenciar')
    const encerrado = c.status === 'ENCERRADO'

    async function guardar(novo: Contrato) {
        consultas.setQueryData(consultaContrato(c.id).queryKey, novo)
        await consultas.invalidateQueries({ queryKey: ['contratos'] })
    }
    const transicao = useMutation({
        mutationFn: ({ acao, data }: { acao: AcaoContrato; data?: string }) => transicionarContrato(c, acao, data),
        onSuccess: async (novo) => {
            await guardar(novo)
            setEncerrando(false)
            avisar(`Contrato ${STATUS_CONTRATO[novo.status].toLowerCase()}.`)
        },
        onError: (erro) => {
            if (!encerrando) avisarErro(erro)
            void consultas.invalidateQueries({ queryKey: consultaContrato(c.id).queryKey })
        },
    })

    const irParaMes = (novo: string) =>
        void navegar({
            to: '/contratos/$id',
            params: { id: c.id },
            search: { mes: novo === mesAtual(fuso) ? undefined : novo },
        })

    return (
        <Pagina
            titulo={`${TIPO_CONTRATO[c.tipo]} · ${c.clienteNome}`}
            contexto={
                <Link to="/contratos" className="hover:underline">
                    Contratos
                </Link>
            }
            acoes={
                gerencia &&
                !encerrado && (
                    <div className="flex flex-wrap gap-2">
                        <Botao variante="secundario" onPress={() => setEditando(true)}>
                            Editar
                        </Botao>
                        <Botao
                            variante="secundario"
                            isPending={transicao.isPending && !encerrando}
                            onPress={() =>
                                transicao.mutate({ acao: c.status === 'ATIVO' ? 'suspensao' : 'reativacao' })
                            }
                        >
                            {c.status === 'ATIVO' ? 'Suspender' : 'Reativar'}
                        </Botao>
                        <Botao
                            variante="secundario"
                            onPress={() => {
                                transicao.reset()
                                setEncerrando(true)
                            }}
                        >
                            Encerrar
                        </Botao>
                    </div>
                )
            }
        >
            <div className="flex items-center gap-3">
                <span className={`rounded-couro px-2 py-0.5 text-xs ${ESTILO_STATUS_CONTRATO[c.status]}`}>
                    {STATUS_CONTRATO[c.status]}
                </span>
                {c.status === 'SUSPENSO' && (
                    <span className="text-sm text-suave">
                        Suspenso: horas lançadas agora ficam avulsas e solicitações novas não usam este contrato.
                    </span>
                )}
            </div>

            <section
                aria-label="Dados"
                className="grid gap-4 rounded-couro border border-linha bg-superficie p-5 sm:grid-cols-3"
            >
                <Dado rotulo="Cliente">
                    {pode(perfil, 'cliente.ver') ? (
                        <Link to="/clientes/$id" params={{ id: c.clienteId }} className="hover:underline">
                            {c.clienteNome}
                        </Link>
                    ) : (
                        c.clienteNome
                    )}
                </Dado>
                <Dado rotulo="Vigência">
                    {dataCurta(c.inicio, hoje)} a {c.fim ? dataCurta(c.fim, hoje) : 'sem fim'}
                </Dado>
                <Dado rotulo={c.tipo === 'BANCO_HORAS' ? 'Horas contratadas' : 'Franquia'}>
                    {c.franquiaMinutos != null
                        ? `${formatarDuracao(Number(c.franquiaMinutos))}${c.tipo === 'MENSAL_FIXO' ? ' por mês' : ''}`
                        : c.tipo === 'AVULSO'
                          ? 'Sem franquia (avulso)'
                          : 'Sem franquia'}
                </Dado>
            </section>

            <ConsumoDoMes
                contrato={c}
                perfil={perfil}
                mes={mes}
                aoMudarMes={irParaMes}
                aoAjustar={
                    c.tipo === 'BANCO_HORAS' && !encerrado && pode(perfil, 'contrato.ajustar_saldo')
                        ? () => setAjustando(true)
                        : undefined
                }
            />

            <section aria-labelledby="sla" className="flex flex-col gap-2">
                <h2 id="sla" className="font-titulo text-xl font-semibold">
                    Prazos de SLA <span className="text-sm font-normal text-suave">(horas úteis)</span>
                </h2>
                <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
                    <table className="w-full text-sm">
                        <thead className="border-b border-linha text-left text-xs text-suave">
                            <tr>
                                <th className="px-4 py-2 font-semibold">Prioridade</th>
                                <th className="px-4 py-2 font-semibold">1ª resposta</th>
                                <th className="px-4 py-2 font-semibold">Solução</th>
                            </tr>
                        </thead>
                        <tbody>
                            {PRIORIDADES.map((p) => {
                                const prazo = c.sla.find((s) => s.prioridade === p)
                                return (
                                    <tr key={p} className="border-b border-linha last:border-0">
                                        <th scope="row" className="px-4 py-2 text-left font-normal">
                                            {PRIORIDADE[p]}
                                        </th>
                                        <td className="px-4 py-2">
                                            {prazo ? formatarDuracao(Number(prazo.primeiraRespostaMinutos)) : '—'}
                                        </td>
                                        <td className="px-4 py-2">
                                            {prazo ? formatarDuracao(Number(prazo.resolucaoMinutos)) : '—'}
                                        </td>
                                    </tr>
                                )
                            })}
                        </tbody>
                    </table>
                </div>
            </section>

            <HorasDoMes contrato={c} perfil={perfil} mes={mes} />

            <JanelaCadastroContrato
                fuso={fuso}
                aberta={editando}
                aoMudar={setEditando}
                contrato={c}
                aoSalvar={(novo) => void guardar(novo)}
            />
            <JanelaAjuste contrato={c} fuso={fuso} aberta={ajustando} aoMudar={setAjustando} />
            <JanelaEncerramento
                aberta={encerrando}
                aoMudar={setEncerrando}
                hoje={hoje}
                ocupada={transicao.isPending}
                erro={transicao.isError ? transicao.error : null}
                aoEncerrar={(data) => transicao.mutate({ acao: 'encerramento', data })}
            />
        </Pagina>
    )
}

function Barra({ rotulo, valor, maximo, texto }: { rotulo: string; valor: number; maximo: number; texto: string }) {
    const pct = maximo > 0 ? Math.min(Math.round((valor / maximo) * 100), 100) : 0
    const estourou = valor > maximo
    return (
        <div className="flex flex-col gap-1">
            <div className="flex items-baseline justify-between text-sm">
                <span className="font-semibold">{rotulo}</span>
                <span className={estourou ? 'text-vermelho' : 'text-suave'}>{texto}</span>
            </div>
            <div
                role="meter"
                aria-label={rotulo}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={pct}
                aria-valuetext={texto}
                className="h-2.5 overflow-hidden rounded-full bg-trilho"
            >
                <div
                    className={`h-full ${estourou ? 'bg-vermelho' : pct >= 80 ? 'bg-ambar' : 'bg-acento'}`}
                    style={{ width: `${pct}%` }}
                />
            </div>
        </div>
    )
}

/** Régua do mês: franquia × consumo (mensal), saldo acumulado (banco de horas) ou só o consumo (avulso). */
function Regua({ contrato: c, consumo: k }: { contrato: Contrato; consumo: Consumo }) {
    const consumido = Number(k.consumidoMinutos)
    const franquia = k.franquiaMinutos != null ? Number(k.franquiaMinutos) : null
    if (c.tipo === 'BANCO_HORAS' && franquia != null) {
        const saldo = Number(k.saldoMinutos ?? 0)
        const total = franquia + Number(k.ajustesMinutos)
        const usado = Number(k.consumidoAcumuladoMinutos ?? consumido)
        return (
            <div className="flex flex-col gap-3">
                <Barra
                    rotulo="Banco de horas"
                    valor={usado}
                    maximo={total}
                    texto={`${formatarDuracao(usado)} usadas de ${formatarDuracao(total)}`}
                />
                <p className="text-sm">
                    Saldo:{' '}
                    <span className={saldo < 0 ? 'font-semibold text-vermelho' : 'font-semibold'}>
                        {formatarDuracao(saldo)}
                    </span>
                    <span className="text-suave"> · {formatarDuracao(consumido)} neste mês</span>
                </p>
            </div>
        )
    }
    if (franquia != null)
        return (
            <div className="flex flex-col gap-3">
                <Barra
                    rotulo="Franquia do mês"
                    valor={consumido}
                    maximo={franquia}
                    texto={`${formatarDuracao(consumido)} de ${formatarDuracao(franquia)}`}
                />
                {consumido > franquia && (
                    <p className="text-sm text-vermelho">Excedente: {formatarDuracao(consumido - franquia)}</p>
                )}
            </div>
        )
    return (
        <p className="text-sm">
            <span className="font-semibold">{formatarDuracao(consumido)}</span> faturáveis neste mês
        </p>
    )
}

function ConsumoDoMes({
    contrato: c,
    perfil,
    mes,
    aoMudarMes,
    aoAjustar,
}: {
    contrato: Contrato
    perfil: Perfil
    mes: string
    aoMudarMes: (mes: string) => void
    aoAjustar?: () => void
}) {
    const consultas = useQueryClient()
    const consumo = useQuery(consultaConsumo(c.id, mes))
    const [fechando, setFechando] = useState(false)
    const atual = mesAtual(perfil.tenant.fusoHorario)
    const fechamento = useMutation({
        mutationFn: () => fecharPeriodo(c.id, mes),
        onSuccess: async () => {
            await consultas.invalidateQueries({ queryKey: ['contratos'] })
            setFechando(false)
            avisar(`${nomeDoMes(mes)} fechado para edição de horas.`)
        },
    })
    const inicioMes = c.inicio.slice(0, 7)
    const fimMes = c.fim?.slice(0, 7)
    // Só mês já encerrado e dentro da vigência pode ser fechado (contrato §5).
    const podeFechar =
        pode(perfil, 'contrato.fechar_periodo') &&
        mes < atual &&
        mes >= inicioMes &&
        (!fimMes || mes <= fimMes) &&
        consumo.data &&
        !consumo.data.periodoFechado

    return (
        <section
            aria-labelledby="consumo"
            className="flex flex-col gap-3 rounded-couro border border-linha bg-superficie p-5"
        >
            <div className="flex flex-wrap items-center gap-2">
                <h2 id="consumo" className="mr-auto font-titulo text-xl font-semibold">
                    Consumo
                </h2>
                <button
                    type="button"
                    onClick={() => aoMudarMes(somarMeses(mes, -1))}
                    aria-label="Mês anterior"
                    className="grid size-8 place-items-center rounded-couro border border-linha text-suave hover:bg-trilho hover:text-tinta"
                >
                    <Icone nome="seta" className="size-4 rotate-180" />
                </button>
                <span className="min-w-36 text-center text-sm font-semibold first-letter:uppercase" aria-live="polite">
                    {nomeDoMes(mes)}
                </span>
                <button
                    type="button"
                    onClick={() => aoMudarMes(somarMeses(mes, 1))}
                    aria-label="Próximo mês"
                    disabled={mes >= atual}
                    className="grid size-8 place-items-center rounded-couro border border-linha text-suave hover:bg-trilho hover:text-tinta disabled:opacity-40"
                >
                    <Icone nome="seta" className="size-4" />
                </button>
            </div>
            {consumo.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : consumo.isError ? (
                <Alerta>{mensagemDeErro(consumo.error)}</Alerta>
            ) : (
                <>
                    <Regua contrato={c} consumo={consumo.data} />
                    <p className="text-xs text-suave">
                        {formatarDuracao(Number(consumo.data.naoFaturavelMinutos))} não faturáveis no mês
                        {Number(consumo.data.ajustesMinutos) !== 0 &&
                            ` · ajustes: ${formatarDuracao(Number(consumo.data.ajustesMinutos))}`}
                    </p>
                    <div className="flex flex-wrap items-center gap-3">
                        {consumo.data.periodoFechado && (
                            <span className="rounded-couro bg-trilho px-2 py-0.5 text-xs text-suave">
                                Mês fechado: as horas não podem mais ser editadas
                            </span>
                        )}
                        {podeFechar && (
                            <Botao variante="secundario" onPress={() => setFechando(true)}>
                                Fechar o mês
                            </Botao>
                        )}
                        {aoAjustar && (
                            <Botao variante="secundario" onPress={aoAjustar}>
                                Ajustar saldo
                            </Botao>
                        )}
                    </div>
                </>
            )}
            <Confirmacao
                titulo={`Fechar ${nomeDoMes(mes)}?`}
                acao="Fechar o mês"
                aberta={fechando}
                aoMudar={setFechando}
                aoConfirmar={() => fechamento.mutate()}
                ocupada={fechamento.isPending}
                erro={fechamento.isError ? (mensagemDeErro(fechamento.error) ?? 'Não deu para fechar.') : null}
            >
                <p>Depois de fechado, ninguém lança, edita ou exclui horas deste contrato em {nomeDoMes(mes)}.</p>
                <p>Use depois de conferir e cobrar o mês. Não dá para reabrir.</p>
            </Confirmacao>
        </section>
    )
}

function HorasDoMes({ contrato: c, perfil, mes }: { contrato: Contrato; perfil: Perfil; mes: string }) {
    const podeVer = pode(perfil, 'apontamento.ver')
    const horas = useQuery({
        ...consultaApontamentos({ contratoId: c.id, de: `${mes}-01`, ate: fimDoMes(mes) }),
        enabled: podeVer,
    })
    if (!podeVer) return null
    return (
        <section aria-labelledby="horas-contrato" className="flex flex-col gap-2">
            <h2 id="horas-contrato" className="font-titulo text-xl font-semibold">
                Horas do mês
            </h2>
            {horas.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : horas.isError ? (
                <Alerta>{mensagemDeErro(horas.error)}</Alerta>
            ) : horas.data.itens.length === 0 ? (
                <p className="text-sm text-suave">Nenhuma hora lançada neste contrato no mês.</p>
            ) : (
                <LinhasApontamento
                    itens={horas.data.itens}
                    fuso={perfil.tenant.fusoHorario}
                    usuarioId={perfil.usuarioId}
                    podeEditarTodos={pode(perfil, 'apontamento.editar_todos')}
                    podeCriar={pode(perfil, 'apontamento.criar')}
                    mostrarTarefa
                    mostrarPessoa
                    mostrarData
                />
            )}
        </section>
    )
}

/** Ajuste de saldo do banco de horas (RF-CTR-05): adicionar ou descontar, com motivo (fica na auditoria). */
function JanelaAjuste({
    contrato: c,
    fuso,
    aberta,
    aoMudar,
}: {
    contrato: Contrato
    fuso: string
    aberta: boolean
    aoMudar: (aberta: boolean) => void
}) {
    const consultas = useQueryClient()
    const [sentido, setSentido] = useState<'adicionar' | 'descontar'>('adicionar')
    const [invalido, setInvalido] = useState(false)
    const envio = useMutation({
        mutationFn: (dados: { minutos: number; motivo: string; data: string | null }) =>
            ajustarSaldo(c.id, sentido === 'adicionar' ? dados.minutos : -dados.minutos, dados.motivo, dados.data),
        onSuccess: async (_, dados) => {
            await consultas.invalidateQueries({ queryKey: ['contratos'] })
            avisar(
                `${sentido === 'adicionar' ? 'Adicionadas' : 'Descontadas'} ${formatarDuracao(dados.minutos)} do saldo.`,
            )
            aoMudar(false)
        },
    })
    return (
        <Janela
            titulo="Ajustar saldo"
            aberta={aberta}
            aoMudar={(a) => {
                envio.reset()
                aoMudar(a)
            }}
            ocupada={envio.isPending}
        >
            <Form
                className="flex flex-col gap-4"
                onSubmit={(evento) => {
                    evento.preventDefault()
                    const dados = new FormData(evento.currentTarget)
                    const minutos = lerDuracao(String(dados.get('horas') ?? ''))
                    setInvalido(minutos === null)
                    if (minutos === null) return
                    envio.mutate({
                        minutos,
                        motivo: String(dados.get('motivo')).trim(),
                        data: String(dados.get('data') ?? '') || null,
                    })
                }}
            >
                <p className="text-sm text-suave">
                    Para compra de horas extras, cortesia ou correção. O motivo fica registrado.
                </p>
                <LegendaObrigatorios />
                {envio.isError && <Alerta>{mensagemDeErro(envio.error) ?? 'Confira os campos.'}</Alerta>}
                <RadioGroup
                    aria-label="Sentido do ajuste"
                    orientation="horizontal"
                    value={sentido}
                    onChange={(v) => setSentido(v as 'adicionar' | 'descontar')}
                    className="flex w-fit gap-1 rounded-couro bg-trilho p-1"
                >
                    {(['adicionar', 'descontar'] as const).map((v) => (
                        <Radio
                            key={v}
                            value={v}
                            className="cursor-default rounded-couro px-3 py-1 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {v === 'adicionar' ? 'Adicionar horas' : 'Descontar horas'}
                        </Radio>
                    ))}
                </RadioGroup>
                <div className="grid gap-4 sm:grid-cols-2">
                    <div className="flex flex-col gap-1">
                        <label htmlFor="ajuste-horas" className="text-sm font-semibold">
                            Horas
                            <span aria-hidden="true" className="text-acento-texto">
                                {' '}
                                *
                            </span>
                        </label>
                        <input
                            id="ajuste-horas"
                            name="horas"
                            required
                            autoFocus
                            placeholder="10h"
                            aria-invalid={invalido || undefined}
                            aria-describedby="ajuste-horas-d"
                            className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
                        />
                        <p id="ajuste-horas-d" className={`text-xs ${invalido ? 'text-vermelho' : 'text-suave'}`}>
                            {invalido ? 'Não entendi. Use 10h, 1h30 ou 90.' : '10h, 1h30 ou 90 (minutos).'}
                        </p>
                    </div>
                    <div className="flex flex-col gap-1">
                        <label htmlFor="ajuste-data" className="text-sm font-semibold">
                            Data
                        </label>
                        <input
                            id="ajuste-data"
                            name="data"
                            type="date"
                            defaultValue={hojeNoFuso(fuso)}
                            className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta"
                        />
                    </div>
                </div>
                <CampoTexto name="motivo" rotulo="Motivo" maxLength={1_000} isRequired multilinha />
                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={() => aoMudar(false)} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao type="submit" isPending={envio.isPending}>
                        {sentido === 'adicionar' ? 'Adicionar' : 'Descontar'}
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}

/** Encerrar é final (contrato §5): pede confirmação e aceita a data (sem ela, hoje); nunca estende a vigência. */
function JanelaEncerramento({
    aberta,
    aoMudar,
    hoje,
    ocupada,
    erro,
    aoEncerrar,
}: {
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    hoje: string
    ocupada: boolean
    erro: unknown
    aoEncerrar: (data: string) => void
}) {
    const [data, setData] = useState(hoje)
    const erroData = erro instanceof ErroApi ? erro.doCampo('data') : undefined
    return (
        <Confirmacao
            titulo="Encerrar o contrato?"
            acao="Encerrar contrato"
            aberta={aberta}
            aoMudar={aoMudar}
            aoConfirmar={() => aoEncerrar(data)}
            ocupada={ocupada}
            erro={erro && !erroData ? (mensagemDeErro(erro) ?? 'Não deu para encerrar.') : null}
        >
            <p>Contrato encerrado não volta a valer: horas e solicitações novas não usam mais ele.</p>
            <div className="flex flex-col gap-1">
                <label htmlFor="encerramento-data" className="text-sm font-semibold text-tinta">
                    Encerrar em
                </label>
                <input
                    id="encerramento-data"
                    type="date"
                    value={data}
                    onChange={(e) => setData(e.target.value || hoje)}
                    aria-invalid={erroData ? true : undefined}
                    className="w-fit rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
                />
                {erroData && <p className="text-sm text-vermelho">{erroData}</p>}
            </div>
        </Confirmacao>
    )
}
