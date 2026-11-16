import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Form, Radio, RadioGroup } from 'react-aria-components'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { opcional } from '@/features/clientes/consultas'
import { consultaContratosAtivos } from '@/features/solicitacoes/consultas'
import { TIPO_CONTRATO } from '@/features/solicitacoes/rotulos'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar } from '@/shared/avisos/avisos'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { dataCurta, diaPorExtenso, hojeNoFuso } from '@/shared/format/datas'
import { formatarDuracao, lerDuracao } from '@/shared/format/duracao'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import { Marcador } from '@/shared/ui/Marcador'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import {
    criarRecorrencia,
    descreverAntecedencia,
    descreverRegra,
    editarRecorrencia,
    NOME_DIA_SEMANA,
    PERIODICIDADE,
    previaDaRegra,
    type Periodicidade,
    type Recorrencia,
    type Regra,
} from './consultas'

/**
 * Criar ou editar recorrência (RF-REC-01/02, F4c): o modelo da tarefa que vai ser gerada e a regra, com as próximas
 * datas calculadas pela API enquanto se digita (decisão 5 do F4). O cliente fica fixo depois de criada.
 */
export function JanelaRecorrencia({
    fuso,
    aberta,
    aoMudar,
    recorrencia,
    aoSalvar,
}: {
    fuso: string
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    recorrencia?: Recorrencia
    aoSalvar: (r: Recorrencia) => void
}) {
    return (
        <Janela
            titulo={recorrencia ? 'Editar recorrência' : 'Nova recorrência'}
            aberta={aberta}
            aoMudar={aoMudar}
            larga
        >
            {aberta && (
                <Formulario fuso={fuso} recorrencia={recorrencia} aoFechar={() => aoMudar(false)} aoSalvar={aoSalvar} />
            )}
        </Janela>
    )
}

/** O valor depois de `ms` sem mudar: a prévia não chama a API a cada tecla. */
function useAssentado<T>(valor: T, ms = 300): T {
    const [assentado, setAssentado] = useState(valor)
    useEffect(() => {
        const t = setTimeout(() => setAssentado(valor), ms)
        return () => clearTimeout(t)
    }, [valor, ms])
    return assentado
}

const inteiro = (texto: string, min: number, max: number): number | null => {
    const n = Number(texto)
    return texto.trim() !== '' && Number.isInteger(n) && n >= min && n <= max ? n : null
}

const estiloEntrada =
    'rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho'

function Formulario({
    fuso,
    recorrencia: r,
    aoFechar,
    aoSalvar,
}: {
    fuso: string
    recorrencia?: Recorrencia
    aoFechar: () => void
    aoSalvar: (r: Recorrencia) => void
}) {
    const consultas = useQueryClient()
    const hoje = hojeNoFuso(fuso)
    const [clienteId, setClienteId] = useState<string | null>(r?.clienteId ?? null)
    const [contrato, setContrato] = useState<string | null>(r?.contratoId ?? null)
    const [responsavel, setResponsavel] = useState<string | null>(r?.modelo.responsavelId ?? null)
    const [visivel, setVisivel] = useState(r?.modelo.visivelCliente ?? false)
    const [periodicidade, setPeriodicidade] = useState<Periodicidade>(r?.regra.periodicidade ?? 'MENSAL')
    const [dia, setDia] = useState(String(r?.regra.diaReferencia ?? Number(hoje.slice(8, 10))))
    const [antecedencia, setAntecedencia] = useState(String(r?.regra.antecedenciaDias ?? 3))
    const [inicio, setInicio] = useState(r?.regra.inicio ?? '')
    const [fim, setFim] = useState(r?.regra.fim ?? '')
    const [estimativaInvalida, setEstimativaInvalida] = useState(false)
    const equipe = useQuery(consultaEquipe)
    const contratos = useQuery({ ...consultaContratosAtivos(clienteId ?? ''), enabled: clienteId !== null })

    const semanal = periodicidade === 'SEMANAL'
    const diaValido = inteiro(dia, 1, semanal ? 7 : 31)
    const antecedenciaValida = inteiro(antecedencia, 0, 365)
    const regra: Regra | null =
        diaValido !== null && antecedenciaValida !== null
            ? {
                  periodicidade,
                  diaReferencia: diaValido,
                  antecedenciaDias: antecedenciaValida,
                  inicio: inicio || null,
                  fim: fim || null,
              }
            : null
    // Texto, não o objeto (que é novo a cada render): a espera só recomeça quando a regra muda de fato.
    const regraAssentada = useAssentado(regra ? JSON.stringify(regra) : null)
    const previa = useQuery({
        queryKey: ['recorrencias', 'previa-regra', regraAssentada],
        queryFn: () => previaDaRegra(JSON.parse(regraAssentada!) as Regra),
        enabled: regraAssentada !== null,
        placeholderData: keepPreviousData,
        retry: false,
    })

    const envio = useMutation({
        mutationFn: (modelo: Recorrencia['modelo']) => {
            const corpo = { contratoId: contrato, modelo, regra: regra! }
            return r ? editarRecorrencia(r, corpo) : criarRecorrencia({ ...corpo, clienteId: clienteId! })
        },
        onSuccess: async (salva) => {
            await consultas.invalidateQueries({ queryKey: ['recorrencias'] })
            avisar(
                r
                    ? 'Recorrência atualizada. Vale para as próximas tarefas.'
                    : `Recorrência "${salva.modelo.titulo}" criada.`,
            )
            aoFechar()
            aoSalvar(salva)
        },
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null
    const erroDe = (campo: string) => {
        for (const erro of [envio.error, previa.error])
            if (erro instanceof ErroApi && erro.doCampo(campo)) return erro.doCampo(campo)
        return undefined
    }
    const erroDia =
        diaValido === null ? (semanal ? 'Escolha o dia da semana.' : 'De 1 a 31.') : erroDe('regra.diaReferencia')
    const erroAntecedencia = antecedenciaValida === null ? 'De 0 a 365 dias.' : erroDe('regra.antecedenciaDias')

    return (
        <Form
            className="flex flex-col gap-5"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                if (!clienteId || !regra) return
                const dados = new FormData(evento.currentTarget)
                const textoEstimativa = String(dados.get('estimativa') ?? '').trim()
                const estimativa = textoEstimativa ? lerDuracao(textoEstimativa) : null
                setEstimativaInvalida(Boolean(textoEstimativa) && estimativa === null)
                if (textoEstimativa && estimativa === null) return
                const checklist = String(dados.get('checklist') ?? '')
                    .split('\n')
                    .map((l) => l.trim())
                    .filter(Boolean)
                envio.mutate({
                    titulo: String(dados.get('modelo.titulo')).trim(),
                    descricao: opcional(dados.get('modelo.descricao')),
                    responsavelId: responsavel,
                    estimativaMinutos: estimativa,
                    visivelCliente: visivel,
                    checklist: checklist.length ? checklist : null,
                })
            }}
        >
            <LegendaObrigatorios />
            {mensagem && <Alerta>{mensagem}</Alerta>}

            <fieldset className="flex flex-col gap-4">
                <legend className="mb-3 font-titulo text-lg font-semibold">A tarefa</legend>
                <div className="grid gap-4 sm:grid-cols-2">
                    {r ? (
                        <p className="text-sm text-suave sm:col-span-2">Cliente: {r.clienteNome}</p>
                    ) : (
                        <CampoCliente
                            name="clienteId"
                            rotulo="Cliente"
                            valor={clienteId}
                            aoMudar={(v) => {
                                setClienteId(v)
                                setContrato(null)
                            }}
                            isRequired
                        />
                    )}
                    {clienteId && (
                        <CampoSelecao
                            name="contratoId"
                            rotulo="Contrato"
                            descricao="As horas das tarefas geradas consomem deste contrato."
                            opcoes={(contratos.data ?? []).map((c) => ({
                                id: c.id,
                                rotulo: TIPO_CONTRATO[c.tipo],
                                detalhe: `desde ${dataCurta(c.inicio)}`,
                            }))}
                            valor={contrato}
                            aoMudar={setContrato}
                            vazio="Sem contrato (horas avulsas)"
                        />
                    )}
                </div>
                <CampoTexto
                    name="modelo.titulo"
                    rotulo="Título"
                    defaultValue={r?.modelo.titulo ?? ''}
                    maxLength={200}
                    isRequired
                    autoFocus={Boolean(r)}
                />
                <CampoTexto
                    name="modelo.descricao"
                    rotulo="Descrição"
                    multilinha
                    defaultValue={r?.modelo.descricao ?? ''}
                />
                <div className="grid gap-4 sm:grid-cols-2">
                    <CampoSelecao
                        name="modelo.responsavelId"
                        rotulo="Responsável"
                        opcoes={(equipe.data ?? []).map((p) => ({ id: p.id, rotulo: p.nome }))}
                        valor={responsavel}
                        aoMudar={setResponsavel}
                        vazio="Sem responsável"
                    />
                    <div className="flex flex-col gap-1">
                        <label htmlFor="rec-estimativa" className="text-sm font-semibold">
                            Estimativa
                        </label>
                        <input
                            id="rec-estimativa"
                            name="estimativa"
                            placeholder="1h30"
                            defaultValue={
                                r?.modelo.estimativaMinutos != null
                                    ? formatarDuracao(Number(r.modelo.estimativaMinutos))
                                    : ''
                            }
                            aria-invalid={estimativaInvalida || undefined}
                            aria-describedby="rec-estimativa-d"
                            className={estiloEntrada}
                        />
                        <p
                            id="rec-estimativa-d"
                            className={`text-xs ${estimativaInvalida ? 'text-vermelho' : 'text-suave'}`}
                        >
                            {estimativaInvalida ? 'Não entendi. Use 1h30, 1:30 ou 90.' : '1h30, 1:30 ou 90 (minutos).'}
                        </p>
                    </div>
                </div>
                <CampoTexto
                    name="checklist"
                    rotulo="Checklist"
                    descricao="Um item por linha. Cada tarefa gerada nasce com a lista desmarcada."
                    multilinha
                    defaultValue={(r?.modelo.checklist ?? []).join('\n')}
                />
                <Marcador valor={visivel} aoMudar={setVisivel}>
                    O cliente vê as tarefas geradas no portal
                </Marcador>
            </fieldset>

            <fieldset className="flex flex-col gap-4">
                <legend className="mb-3 font-titulo text-lg font-semibold">Quando se repete</legend>
                <RadioGroup
                    aria-label="Periodicidade"
                    orientation="horizontal"
                    value={periodicidade}
                    onChange={(v) => {
                        const nova = v as Periodicidade
                        // O dia da semana (1–7) e o do mês (1–31) não se traduzem: trocar de tipo começa no 1.
                        if ((nova === 'SEMANAL') !== semanal) setDia('1')
                        setPeriodicidade(nova)
                    }}
                    className="flex w-fit flex-wrap gap-1 rounded-couro bg-trilho p-1"
                >
                    {(Object.keys(PERIODICIDADE) as Periodicidade[]).map((p) => (
                        <Radio
                            key={p}
                            value={p}
                            className="cursor-default rounded-couro px-3 py-1 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {PERIODICIDADE[p]}
                        </Radio>
                    ))}
                </RadioGroup>
                <div className="grid gap-4 sm:grid-cols-2">
                    {semanal ? (
                        <CampoSelecao
                            rotulo="Dia da semana"
                            opcoes={NOME_DIA_SEMANA.map((nome, i) => ({ id: String(i + 1), rotulo: nome }))}
                            valor={diaValido !== null ? String(diaValido) : null}
                            aoMudar={(v) => setDia(v ?? '1')}
                            isRequired
                        />
                    ) : (
                        <CampoNumero
                            id="rec-dia"
                            rotulo="Dia do mês"
                            valor={dia}
                            aoMudar={setDia}
                            min={1}
                            max={31}
                            erro={erroDia}
                            dica="Mês sem esse dia usa o último dia."
                        />
                    )}
                    <CampoNumero
                        id="rec-antecedencia"
                        rotulo="Criar a tarefa quantos dias antes"
                        valor={antecedencia}
                        aoMudar={setAntecedencia}
                        min={0}
                        max={365}
                        erro={erroAntecedencia}
                        dica="0 cria no próprio dia; o prazo é sempre a data."
                    />
                    <CampoDataRegra
                        id="rec-inicio"
                        rotulo="Começa em"
                        valor={inicio}
                        aoMudar={setInicio}
                        erro={erroDe('regra.inicio')}
                        dica={
                            periodicidade === 'TRIMESTRAL' || periodicidade === 'ANUAL'
                                ? 'O mês do início marca o ciclo. Vazio: hoje.'
                                : 'Vazio: hoje.'
                        }
                    />
                    <CampoDataRegra
                        id="rec-fim"
                        rotulo="Termina em"
                        valor={fim}
                        aoMudar={setFim}
                        erro={erroDe('regra.fim')}
                        dica="Vazio: sem fim."
                    />
                </div>
                <section
                    aria-labelledby="rec-previa"
                    aria-live="polite"
                    className="flex flex-col gap-2 rounded-couro border border-linha bg-trilho/40 p-4 text-sm"
                >
                    <h3 id="rec-previa" className="font-semibold">
                        Próximas datas
                    </h3>
                    {regra && (
                        <p className="text-suave">
                            {descreverRegra(regra, hoje)}. {descreverAntecedencia(Number(regra.antecedenciaDias))}
                        </p>
                    )}
                    {!regra ? (
                        <p className="text-suave">Complete a regra para ver as datas.</p>
                    ) : previa.isError ? (
                        <p className="text-vermelho">{mensagemDeErro(previa.error) ?? 'Confira os campos da regra.'}</p>
                    ) : previa.data === undefined ? (
                        <p className="text-suave">Calculando…</p>
                    ) : previa.data.length === 0 ? (
                        <p className="text-suave">Nenhuma data: a regra termina antes da próxima ocorrência.</p>
                    ) : (
                        <ol className={`flex flex-wrap gap-2 ${previa.isFetching ? 'opacity-60' : ''}`}>
                            {previa.data.map((d) => (
                                <li key={d} className="rounded-couro bg-superficie px-2 py-1 tabular-nums">
                                    {diaPorExtenso(d)}
                                    {d.slice(0, 4) !== hoje.slice(0, 4) && `/${d.slice(2, 4)}`}
                                </li>
                            ))}
                        </ol>
                    )}
                </section>
            </fieldset>

            <div className="flex justify-end gap-3">
                <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                    Cancelar
                </Botao>
                <Botao grava type="submit" isPending={envio.isPending} isDisabled={!regra}>
                    {r ? 'Salvar' : 'Criar recorrência'}
                </Botao>
            </div>
        </Form>
    )
}

function CampoNumero({
    id,
    rotulo,
    valor,
    aoMudar,
    min,
    max,
    erro,
    dica,
}: {
    id: string
    rotulo: string
    valor: string
    aoMudar: (v: string) => void
    min: number
    max: number
    erro?: string
    dica: string
}) {
    return (
        <div className="flex flex-col gap-1">
            <label htmlFor={id} className="text-sm font-semibold">
                {rotulo}
            </label>
            <input
                id={id}
                type="number"
                inputMode="numeric"
                min={min}
                max={max}
                value={valor}
                onChange={(e) => aoMudar(e.target.value)}
                aria-invalid={erro ? true : undefined}
                aria-describedby={`${id}-d`}
                className={`${estiloEntrada} w-28`}
            />
            <p id={`${id}-d`} className={`text-xs ${erro ? 'text-vermelho' : 'text-suave'}`}>
                {erro ?? dica}
            </p>
        </div>
    )
}

function CampoDataRegra({
    id,
    rotulo,
    valor,
    aoMudar,
    erro,
    dica,
}: {
    id: string
    rotulo: string
    valor: string
    aoMudar: (v: string) => void
    erro?: string
    dica: string
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
                onChange={(e) => aoMudar(e.target.value)}
                aria-invalid={erro ? true : undefined}
                aria-describedby={`${id}-d`}
                className={estiloEntrada}
            />
            <p id={`${id}-d`} className={`text-xs ${erro ? 'text-vermelho' : 'text-suave'}`}>
                {erro ?? dica}
            </p>
        </div>
    )
}
