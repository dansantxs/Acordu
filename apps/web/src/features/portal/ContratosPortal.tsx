import { useQuery } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { dataCurta, mesAtual, nomeDoMes, somarMeses } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { consultaContratosPortal, consultaExtratoPortal, type ContratoPortal, type ExtratoPortal } from './consultas'
import { TituloPortal } from './CascaPortal'

const TIPO: Record<ContratoPortal['tipo'], string> = {
    MENSAL_FIXO: 'Mensal fixo',
    BANCO_HORAS: 'Banco de horas',
    AVULSO: 'Avulso',
}

/** `?contrato=&mes=`: o contrato escolhido (padrão: o primeiro) e o mês do extrato (padrão: o corrente). */
export interface FiltrosContratosPortal {
    contrato?: string
    mes?: string
}

/**
 * Contratos e extrato no portal (F7e, RF-POR-06): os contratos ativos do cliente e, do escolhido, o consumo do mês na
 * régua (horas usadas × contratadas, saldo) e as linhas que a equipe deixou visíveis. O total soma só o faturável; linha
 * faturável não visível aparece como "Atendimento" (contrato §12: o cliente confere o total linha a linha).
 */
export function ContratosPortal({ filtros, fuso }: { filtros: FiltrosContratosPortal; fuso: string }) {
    const navegar = useNavigate()
    const contratos = useQuery(consultaContratosPortal)
    const mesCorrente = mesAtual(fuso)
    const mes = filtros.mes ?? mesCorrente
    const escolhido = contratos.data?.find((c) => c.id === filtros.contrato) ?? contratos.data?.[0]
    const ir = (mudanca: FiltrosContratosPortal) =>
        void navegar({
            to: '/portal/contratos',
            search: (atual: FiltrosContratosPortal) => ({ ...atual, ...mudanca }),
            replace: true,
        })

    return (
        <>
            <TituloPortal titulo="Contratos" />
            {contratos.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : contratos.isError ? (
                <Alerta>{mensagemDeErro(contratos.error)}</Alerta>
            ) : contratos.data.length === 0 ? (
                <EstadoVazio titulo="Nenhum contrato ativo">
                    O atendimento segue normalmente; as horas são combinadas à parte.
                </EstadoVazio>
            ) : (
                <>
                    {contratos.data.length > 1 && (
                        <ul aria-label="Contratos" className="flex flex-wrap gap-2">
                            {contratos.data.map((c) => (
                                <li key={c.id}>
                                    <button
                                        type="button"
                                        aria-pressed={c.id === escolhido?.id}
                                        onClick={() => ir({ contrato: c.id })}
                                        className="rounded-couro border border-linha bg-superficie px-3 py-1.5 text-sm aria-pressed:border-acento aria-pressed:bg-acento-suave"
                                    >
                                        {TIPO[c.tipo]} · desde {dataCurta(c.inicio)}
                                    </button>
                                </li>
                            ))}
                        </ul>
                    )}
                    {escolhido && (
                        <Extrato
                            contrato={escolhido}
                            mes={mes}
                            podeAvancar={mes < mesCorrente}
                            aoMudarMes={(m) => ir({ mes: m === mesCorrente ? undefined : m })}
                        />
                    )}
                </>
            )}
        </>
    )
}

function Extrato({
    contrato: c,
    mes,
    podeAvancar,
    aoMudarMes,
}: {
    contrato: ContratoPortal
    mes: string
    podeAvancar: boolean
    aoMudarMes: (mes: string) => void
}) {
    const extrato = useQuery(consultaExtratoPortal(c.id, mes))
    const antesDoInicio = mes <= c.inicio.slice(0, 7)
    return (
        <section
            aria-labelledby="extrato"
            className="flex flex-col gap-4 rounded-couro border border-linha bg-superficie p-4"
        >
            <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 id="extrato" className="font-titulo text-xl font-semibold">
                    {TIPO[c.tipo]}
                    {c.franquiaMinutos != null && (
                        <span className="font-sans text-sm font-normal text-suave">
                            {' '}
                            · {formatarDuracao(Number(c.franquiaMinutos))}
                            {c.tipo === 'BANCO_HORAS' ? ' contratadas' : ' por mês'}
                        </span>
                    )}
                </h2>
                <div className="flex items-center gap-2">
                    <Botao
                        variante="secundario"
                        aria-label="Mês anterior"
                        isDisabled={antesDoInicio}
                        onPress={() => aoMudarMes(somarMeses(mes, -1))}
                    >
                        ‹
                    </Botao>
                    <span className="min-w-36 text-center text-sm font-semibold first-letter:uppercase">
                        {nomeDoMes(mes)}
                    </span>
                    <Botao
                        variante="secundario"
                        aria-label="Mês seguinte"
                        isDisabled={!podeAvancar}
                        onPress={() => aoMudarMes(somarMeses(mes, 1))}
                    >
                        ›
                    </Botao>
                </div>
            </div>
            {extrato.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : extrato.isError ? (
                <Alerta>{mensagemDeErro(extrato.error)}</Alerta>
            ) : (
                <ConteudoExtrato
                    extrato={extrato.data}
                    banco={c.tipo === 'BANCO_HORAS'}
                    esmaecido={extrato.isPlaceholderData}
                />
            )}
        </section>
    )
}

function ConteudoExtrato({
    extrato: e,
    banco,
    esmaecido,
}: {
    extrato: ExtratoPortal
    banco: boolean
    esmaecido: boolean
}) {
    const consumido = Number(e.consumidoMinutos)
    const pct = e.percentual == null ? null : Number(e.percentual)
    const saldo = e.saldoMinutos == null ? null : Number(e.saldoMinutos)
    return (
        <div className={`flex flex-col gap-4 ${esmaecido ? 'opacity-60' : ''}`}>
            <div className="flex flex-col gap-2">
                <p className="text-sm">
                    <span className="font-semibold tabular-nums">{formatarDuracao(consumido)}</span>{' '}
                    {banco ? 'usadas até este mês' : 'usadas no mês'}
                    {saldo != null && (
                        <span className={saldo < 0 ? 'text-vermelho' : 'text-suave'}>
                            {' '}
                            ·{' '}
                            {saldo < 0
                                ? `${formatarDuracao(-saldo)} além do contratado`
                                : `restam ${formatarDuracao(saldo)}`}
                        </span>
                    )}
                </p>
                {pct != null && (
                    <div
                        role="meter"
                        aria-label="Consumo do contrato"
                        aria-valuemin={0}
                        aria-valuemax={100}
                        aria-valuenow={Math.min(pct, 100)}
                        aria-valuetext={`${pct}%`}
                        className="relative h-2.5 rounded-full bg-trilho"
                    >
                        <div
                            className={`h-full rounded-full ${pct >= 100 ? 'bg-vermelho' : pct >= 80 ? 'bg-ambar' : 'bg-acento'}`}
                            style={{ width: `${Math.min(pct, 100)}%` }}
                        />
                        <span aria-hidden="true" className="absolute inset-y-[-2px] left-[80%] w-px bg-tinta/30" />
                    </div>
                )}
            </div>
            {e.itens.length === 0 ? (
                <p className="text-sm text-suave">Nenhuma hora registrada neste mês.</p>
            ) : (
                <table className="w-full text-sm">
                    <caption className="sr-only">Horas do mês</caption>
                    <thead className="text-left text-xs text-suave">
                        <tr>
                            <th className="py-1 pr-3 font-semibold">Dia</th>
                            <th className="py-1 pr-3 font-semibold">O que foi feito</th>
                            <th className="py-1 text-right font-semibold">Horas</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-linha">
                        {e.itens.map((i, n) => (
                            <tr key={n}>
                                <td className="py-2 pr-3 align-top whitespace-nowrap tabular-nums">
                                    {dataCurta(i.data)}
                                </td>
                                <td className="py-2 pr-3">
                                    {i.descricao}
                                    {i.solicitacaoNumero != null && (
                                        <span className="block text-xs text-suave">
                                            #{Number(i.solicitacaoNumero)} {i.solicitacaoTitulo}
                                        </span>
                                    )}
                                </td>
                                <td className="py-2 text-right align-top tabular-nums">
                                    {formatarDuracao(Number(i.duracaoMinutos))}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
            <p className="text-xs text-suave">
                O total conta as horas cobráveis do contrato. Atendimentos que a equipe não detalhou aparecem como
                "Atendimento".
            </p>
        </div>
    )
}
