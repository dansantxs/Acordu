import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button, Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar } from '@/shared/avisos/avisos'
import { diaPorExtenso, hojeNoFuso } from '@/shared/format/datas'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { Marcador } from '@/shared/ui/Marcador'
import { consultaCalendario, salvarCalendario, type Calendario, type DiaDaSemana } from './consultas'
import { feriadosNacionais } from './feriados'
import { CabecalhoSecao } from './TelaConfiguracoes'

const DIAS: { dia: DiaDaSemana; rotulo: string }[] = [
    { dia: 'SEGUNDA', rotulo: 'Segunda' },
    { dia: 'TERCA', rotulo: 'Terça' },
    { dia: 'QUARTA', rotulo: 'Quarta' },
    { dia: 'QUINTA', rotulo: 'Quinta' },
    { dia: 'SEXTA', rotulo: 'Sexta' },
    { dia: 'SABADO', rotulo: 'Sábado' },
    { dia: 'DOMINGO', rotulo: 'Domingo' },
]

interface Janela {
    ligado: boolean
    inicio: string
    fim: string
}

const FUSOS = (() => {
    try {
        return Intl.supportedValuesOf('timeZone')
    } catch {
        return ['America/Sao_Paulo']
    }
})()

/**
 * Calendário (F5b, ADR 0003): fuso, expediente por dia e feriados, que contam as horas úteis do SLA. Mudar não recalcula
 * prazos de solicitações já abertas (contrato §6).
 */
export function SecaoCalendario() {
    const calendario = useQuery(consultaCalendario)
    return (
        <>
            <CabecalhoSecao titulo="Calendário">
                O SLA conta só as horas do expediente, sem os feriados. Mudar aqui vale para as solicitações novas; as
                já abertas mantêm o prazo que tinham.
            </CabecalhoSecao>
            {calendario.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : calendario.isError ? (
                <Alerta>{mensagemDeErro(calendario.error)}</Alerta>
            ) : (
                <Formulario calendario={calendario.data} />
            )}
        </>
    )
}

function Formulario({ calendario }: { calendario: Calendario }) {
    const consultas = useQueryClient()
    const [fuso, setFuso] = useState(calendario.fusoHorario)
    const [janelas, setJanelas] = useState<Record<DiaDaSemana, Janela>>(() => {
        const inicial = {} as Record<DiaDaSemana, Janela>
        for (const { dia } of DIAS) {
            const j = calendario.expediente.find((e) => e.diaSemana === dia)
            inicial[dia] = j
                ? { ligado: true, inicio: j.inicio, fim: j.fim }
                : { ligado: false, inicio: '08:00', fim: '18:00' }
        }
        return inicial
    })
    const [feriados, setFeriados] = useState<string[]>(() => [...calendario.feriados].sort())
    const [novoFeriado, setNovoFeriado] = useState('')
    const ano = Number(hojeNoFuso(calendario.fusoHorario).slice(0, 4))
    const nomes = new Map([...feriadosNacionais(ano), ...feriadosNacionais(ano + 1)].map((f) => [f.data, f.nome]))

    const envio = useMutation({
        mutationFn: salvarCalendario,
        onSuccess: (salvo) => {
            consultas.setQueryData(consultaCalendario.queryKey, salvo)
            avisar('Calendário salvo. Vale para as solicitações novas.')
        },
    })
    const erroDe = (campo: string) => (envio.error instanceof ErroApi ? envio.error.doCampo(campo) : undefined)
    const invertidos = DIAS.filter(({ dia }) => janelas[dia].ligado && janelas[dia].fim <= janelas[dia].inicio)
    const nenhumDia = DIAS.every(({ dia }) => !janelas[dia].ligado)

    const adicionar = (datas: string[]) => {
        const novas = datas.filter((d) => !feriados.includes(d))
        setFeriados([...feriados, ...novas].sort())
        return novas.length
    }
    const sugerir = (alvo: number) => {
        const quantos = adicionar(feriadosNacionais(alvo).map((f) => f.data))
        avisar(
            quantos === 0
                ? `Os feriados nacionais de ${alvo} já estavam na lista.`
                : `${quantos} feriado${quantos > 1 ? 's' : ''} de ${alvo} adicionado${quantos > 1 ? 's' : ''}. Confira e salve.`,
        )
    }
    const mudar = (dia: DiaDaSemana, mudanca: Partial<Janela>) =>
        setJanelas((atual) => ({ ...atual, [dia]: { ...atual[dia], ...mudanca } }))

    return (
        <Form
            className="flex max-w-2xl flex-col gap-6"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                if (invertidos.length || nenhumDia) return
                envio.mutate({
                    fusoHorario: fuso,
                    expediente: DIAS.filter(({ dia }) => janelas[dia].ligado).map(({ dia }) => ({
                        diaSemana: dia,
                        inicio: janelas[dia].inicio,
                        fim: janelas[dia].fim,
                    })),
                    feriados,
                })
            }}
        >
            {envio.isError && mensagemDeErro(envio.error) && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
            <CampoSelecao
                name="fusoHorario"
                rotulo="Fuso horário"
                opcoes={FUSOS.map((f) => ({ id: f, rotulo: f.replaceAll('_', ' ') }))}
                valor={fuso}
                aoMudar={(v) => v && setFuso(v)}
                className="sm:w-80"
            />

            <fieldset className="flex flex-col gap-2">
                <legend className="mb-1 text-sm font-semibold">Expediente</legend>
                {(erroDe('expediente') || nenhumDia) && (
                    <p className="text-sm text-vermelho">
                        {nenhumDia ? 'Marque ao menos um dia útil.' : erroDe('expediente')}
                    </p>
                )}
                <ul className="flex flex-col divide-y divide-linha rounded-couro border border-linha bg-superficie">
                    {DIAS.map(({ dia, rotulo }) => {
                        const j = janelas[dia]
                        const invertido = invertidos.some((i) => i.dia === dia)
                        return (
                            <li key={dia} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-2.5 text-sm">
                                <span className="w-28">
                                    <Marcador valor={j.ligado} aoMudar={(ligado) => mudar(dia, { ligado })}>
                                        {rotulo}
                                    </Marcador>
                                </span>
                                {j.ligado ? (
                                    <span className="flex items-center gap-2">
                                        <input
                                            type="time"
                                            aria-label={`Início de ${rotulo.toLowerCase()}`}
                                            value={j.inicio}
                                            onChange={(e) => mudar(dia, { inicio: e.target.value })}
                                            className="rounded-couro border border-linha bg-superficie px-2 py-1 text-sm text-tinta"
                                        />
                                        às
                                        <input
                                            type="time"
                                            aria-label={`Fim de ${rotulo.toLowerCase()}`}
                                            value={j.fim}
                                            onChange={(e) => mudar(dia, { fim: e.target.value })}
                                            aria-invalid={invertido || undefined}
                                            className="rounded-couro border border-linha bg-superficie px-2 py-1 text-sm text-tinta aria-[invalid]:border-vermelho"
                                        />
                                        {invertido && (
                                            <span className="text-xs text-vermelho">O fim vem depois do início.</span>
                                        )}
                                    </span>
                                ) : (
                                    <span className="text-suave">Não útil</span>
                                )}
                            </li>
                        )
                    })}
                </ul>
            </fieldset>

            <fieldset className="flex flex-col gap-3">
                <legend className="mb-1 text-sm font-semibold">Feriados</legend>
                <div className="flex flex-wrap items-end gap-2">
                    <div className="flex flex-col gap-1">
                        <label htmlFor="feriado-novo" className="text-xs text-suave">
                            Data
                        </label>
                        <input
                            id="feriado-novo"
                            type="date"
                            value={novoFeriado}
                            onChange={(e) => setNovoFeriado(e.target.value)}
                            className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta"
                        />
                    </div>
                    <Botao
                        variante="secundario"
                        isDisabled={!novoFeriado}
                        onPress={() => {
                            adicionar([novoFeriado])
                            setNovoFeriado('')
                        }}
                    >
                        Adicionar
                    </Botao>
                    <span className="ml-auto flex flex-wrap gap-2">
                        {[ano, ano + 1].map((a) => (
                            <Botao key={a} variante="discreto" onPress={() => sugerir(a)}>
                                Sugerir feriados nacionais de {a}
                            </Botao>
                        ))}
                    </span>
                </div>
                <p className="text-xs text-suave">
                    A sugestão traz os feriados nacionais. Carnaval, Corpus Christi e os feriados do estado e da cidade
                    entram à mão, se a empresa folgar.
                </p>
                {erroDe('feriados') && <p className="text-sm text-vermelho">{erroDe('feriados')}</p>}
                {feriados.length === 0 ? (
                    <p className="text-sm text-suave">Nenhum feriado.</p>
                ) : (
                    <ul className="flex flex-col divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                        {feriados.map((f) => (
                            <li key={f} className="flex items-center justify-between gap-3 px-4 py-2">
                                <span>
                                    <span className="tabular-nums">
                                        {diaPorExtenso(f)}/{f.slice(2, 4)}
                                    </span>
                                    {nomes.get(f) && <span className="text-suave"> · {nomes.get(f)}</span>}
                                </span>
                                <Button
                                    onPress={() => setFeriados(feriados.filter((x) => x !== f))}
                                    aria-label={`Tirar ${f.split('-').reverse().join('/')}`}
                                    className="text-suave hover:text-vermelho hover:underline"
                                >
                                    Tirar
                                </Button>
                            </li>
                        ))}
                    </ul>
                )}
            </fieldset>

            <div>
                <Botao type="submit" isPending={envio.isPending}>
                    Salvar calendário
                </Botao>
            </div>
        </Form>
    )
}
