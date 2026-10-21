import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Form } from 'react-aria-components'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { PRIORIDADE, TIPO_CONTRATO, type Prioridade, type TipoContrato } from '@/features/solicitacoes/rotulos'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar } from '@/shared/avisos/avisos'
import { hojeNoFuso } from '@/shared/format/datas'
import { formatarDuracao, lerDuracao } from '@/shared/format/duracao'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { Janela } from '@/shared/ui/Janela'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import { criarContrato, editarContrato, SLA_PADRAO, type Contrato, type PrazoSla } from './consultas'

const PRIORIDADES: Prioridade[] = ['URGENTE', 'ALTA', 'MEDIA', 'BAIXA']

const DESCRICAO_TIPO: Record<TipoContrato, string> = {
    MENSAL_FIXO: 'Mensalidade com franquia de horas por mês (opcional); o que passa aparece como excedente.',
    BANCO_HORAS: 'Pacote de horas compradas que vai sendo consumido; o saldo acumula de um mês para o outro.',
    AVULSO: 'Sem franquia: as horas são registradas e cobradas à parte.',
}

/**
 * Criar ou editar contrato (RF-CTR-01, F4a): cliente, tipo e início ficam fixos depois de criado; franquia (obrigatória
 * no banco de horas, proibida no avulso), fim da vigência e SLA das quatro prioridades, que já vem preenchido com o
 * padrão sugerido (decisão 3 do F4) e aceita "4h" ou "1h30" (horas úteis).
 */
export function JanelaCadastroContrato({
    fuso,
    aberta,
    aoMudar,
    contrato,
    clienteInicial,
    aoSalvar,
}: {
    fuso: string
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    contrato?: Contrato
    clienteInicial?: string | null
    aoSalvar: (contrato: Contrato) => void
}) {
    return (
        <Janela titulo={contrato ? 'Editar contrato' : 'Novo contrato'} aberta={aberta} aoMudar={aoMudar} larga>
            {aberta && (
                <Formulario
                    fuso={fuso}
                    contrato={contrato}
                    clienteInicial={clienteInicial ?? null}
                    aoFechar={() => aoMudar(false)}
                    aoSalvar={aoSalvar}
                />
            )}
        </Janela>
    )
}

function slaInicial(contrato?: Contrato): Record<string, string> {
    const valores: Record<string, string> = {}
    for (const p of PRIORIDADES) {
        const atual = contrato?.sla.find((s) => s.prioridade === p)
        valores[`${p}.primeira`] = formatarDuracao(
            atual ? Number(atual.primeiraRespostaMinutos) : SLA_PADRAO[p].primeiraResposta,
        )
        valores[`${p}.resolucao`] = formatarDuracao(atual ? Number(atual.resolucaoMinutos) : SLA_PADRAO[p].resolucao)
    }
    return valores
}

function Formulario({
    fuso,
    contrato: c,
    clienteInicial,
    aoFechar,
    aoSalvar,
}: {
    fuso: string
    contrato?: Contrato
    clienteInicial: string | null
    aoFechar: () => void
    aoSalvar: (contrato: Contrato) => void
}) {
    const consultas = useQueryClient()
    const [clienteId, setClienteId] = useState<string | null>(c?.clienteId ?? clienteInicial)
    const [tipo, setTipo] = useState<TipoContrato>(c?.tipo ?? 'MENSAL_FIXO')
    const [sla, setSla] = useState(slaInicial(c))
    const [invalidos, setInvalidos] = useState<string[]>([])

    const envio = useMutation({
        mutationFn: (dados: { inicio: string; fim: string | null; franquiaMinutos: number | null; sla: PrazoSla[] }) =>
            c
                ? editarContrato(c, { fim: dados.fim, franquiaMinutos: dados.franquiaMinutos, sla: dados.sla })
                : criarContrato({ ...dados, clienteId: clienteId!, tipo }),
        onSuccess: async (salvo) => {
            await consultas.invalidateQueries({ queryKey: ['contratos'] })
            await consultas.invalidateQueries({ queryKey: ['clientes'] })
            avisar(c ? 'Contrato atualizado.' : `Contrato ${TIPO_CONTRATO[salvo.tipo].toLowerCase()} criado.`)
            aoFechar()
            aoSalvar(salvo)
        },
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null
    const erroDe = (campo: string) => (envio.error instanceof ErroApi ? envio.error.doCampo(campo) : undefined)
    const comFranquia = tipo !== 'AVULSO'

    return (
        <Form
            className="flex flex-col gap-4"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                if (!clienteId) return
                const dados = new FormData(evento.currentTarget)
                const ruins: string[] = []
                const textoFranquia = String(dados.get('franquia') ?? '').trim()
                const franquia = comFranquia && textoFranquia ? lerDuracao(textoFranquia) : null
                if (comFranquia && textoFranquia && franquia === null) ruins.push('franquia')
                if (tipo === 'BANCO_HORAS' && !textoFranquia) ruins.push('franquia')
                const prazos: PrazoSla[] = PRIORIDADES.map((p) => {
                    const primeira = lerDuracao(sla[`${p}.primeira`] ?? '')
                    const resolucao = lerDuracao(sla[`${p}.resolucao`] ?? '')
                    if (primeira === null) ruins.push(`${p}.primeira`)
                    if (resolucao === null) ruins.push(`${p}.resolucao`)
                    return { prioridade: p, primeiraRespostaMinutos: primeira ?? 0, resolucaoMinutos: resolucao ?? 0 }
                })
                setInvalidos(ruins)
                if (ruins.length) return
                envio.mutate({
                    inicio: String(dados.get('inicio') ?? c?.inicio),
                    fim: String(dados.get('fim') ?? '') || null,
                    franquiaMinutos: franquia,
                    sla: prazos,
                })
            }}
        >
            <LegendaObrigatorios />
            {mensagem && <Alerta>{mensagem}</Alerta>}
            {c ? (
                <p className="text-sm text-suave">
                    {TIPO_CONTRATO[c.tipo]} de {c.clienteNome}, desde {c.inicio.split('-').reverse().join('/')}
                </p>
            ) : (
                <div className="grid gap-4 sm:grid-cols-2">
                    <CampoCliente
                        name="clienteId"
                        rotulo="Cliente"
                        valor={clienteId}
                        aoMudar={setClienteId}
                        isRequired
                    />
                    <CampoSelecao
                        name="tipo"
                        rotulo="Tipo"
                        opcoes={(Object.keys(TIPO_CONTRATO) as TipoContrato[]).map((t) => ({
                            id: t,
                            rotulo: TIPO_CONTRATO[t],
                        }))}
                        valor={tipo}
                        aoMudar={(v) => v && setTipo(v as TipoContrato)}
                        descricao={DESCRICAO_TIPO[tipo]}
                        isRequired
                    />
                </div>
            )}
            <div className="grid gap-4 sm:grid-cols-3">
                {!c && (
                    <CampoData
                        nome="inicio"
                        rotulo="Início"
                        valor={hojeNoFuso(fuso)}
                        obrigatorio
                        erro={erroDe('inicio')}
                    />
                )}
                <CampoData
                    nome="fim"
                    rotulo="Fim"
                    valor={c?.fim ?? null}
                    erro={erroDe('fim')}
                    descricao="Vazio: sem data para acabar."
                />
                {comFranquia && (
                    <div className="flex flex-col gap-1">
                        <label htmlFor="contrato-franquia" className="text-sm font-semibold">
                            {tipo === 'BANCO_HORAS' ? 'Horas contratadas' : 'Franquia por mês'}
                            {tipo === 'BANCO_HORAS' && (
                                <span aria-hidden="true" className="text-acento-texto">
                                    {' '}
                                    *
                                </span>
                            )}
                        </label>
                        <input
                            id="contrato-franquia"
                            name="franquia"
                            placeholder="20h"
                            aria-required={tipo === 'BANCO_HORAS' || undefined}
                            defaultValue={c?.franquiaMinutos != null ? formatarDuracao(Number(c.franquiaMinutos)) : ''}
                            aria-invalid={
                                invalidos.includes('franquia') || erroDe('franquiaMinutos') ? true : undefined
                            }
                            aria-describedby="contrato-franquia-d"
                            className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
                        />
                        <p
                            id="contrato-franquia-d"
                            className={`text-xs ${invalidos.includes('franquia') || erroDe('franquiaMinutos') ? 'text-vermelho' : 'text-suave'}`}
                        >
                            {erroDe('franquiaMinutos') ??
                                (invalidos.includes('franquia')
                                    ? 'Informe as horas, ex.: 20h.'
                                    : 'Em horas: 20h, 40h…')}
                        </p>
                    </div>
                )}
            </div>

            <fieldset className="flex flex-col gap-2">
                <legend className="text-sm font-semibold">Prazos de SLA (horas úteis)</legend>
                <p className="text-xs text-suave">
                    Contados no expediente da empresa, sem fins de semana e feriados. Já vem com um padrão; ajuste ao
                    combinado com o cliente.
                </p>
                {erroDe('sla') && <p className="text-sm text-vermelho">{erroDe('sla')}</p>}
                <table className="w-full text-sm">
                    <thead className="text-left text-xs text-suave">
                        <tr>
                            <th className="py-1 font-semibold">Prioridade</th>
                            <th className="py-1 font-semibold">1ª resposta</th>
                            <th className="py-1 font-semibold">Solução</th>
                        </tr>
                    </thead>
                    <tbody>
                        {PRIORIDADES.map((p) => (
                            <tr key={p}>
                                <th scope="row" className="py-1 pr-3 text-left font-normal">
                                    {PRIORIDADE[p]}
                                </th>
                                {(['primeira', 'resolucao'] as const).map((campo) => {
                                    const chave = `${p}.${campo}`
                                    return (
                                        <td key={campo} className="py-1 pr-3">
                                            <input
                                                aria-label={`${campo === 'primeira' ? '1ª resposta' : 'Solução'} ${PRIORIDADE[p]}`}
                                                value={sla[chave] ?? ''}
                                                onChange={(e) =>
                                                    setSla((atual) => ({ ...atual, [chave]: e.target.value }))
                                                }
                                                aria-invalid={invalidos.includes(chave) || undefined}
                                                className="w-24 rounded-couro border border-linha bg-superficie px-2 py-1.5 text-sm text-tinta aria-[invalid]:border-vermelho"
                                            />
                                        </td>
                                    )
                                })}
                            </tr>
                        ))}
                    </tbody>
                </table>
                {invalidos.some((i) => i.includes('.')) && (
                    <p className="text-sm text-vermelho">Confira os prazos marcados: use 4h, 1h30 ou 90.</p>
                )}
            </fieldset>

            <div className="flex justify-end gap-3">
                <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                    Cancelar
                </Botao>
                <Botao type="submit" isPending={envio.isPending}>
                    {c ? 'Salvar' : 'Criar contrato'}
                </Botao>
            </div>
        </Form>
    )
}

function CampoData({
    nome,
    rotulo,
    valor,
    obrigatorio = false,
    erro,
    descricao,
}: {
    nome: string
    rotulo: string
    valor: string | null
    obrigatorio?: boolean
    erro?: string
    descricao?: string
}) {
    const id = `contrato-${nome}`
    return (
        <div className="flex flex-col gap-1">
            <label htmlFor={id} className="text-sm font-semibold">
                {rotulo}
                {obrigatorio && (
                    <span aria-hidden="true" className="text-acento-texto">
                        {' '}
                        *
                    </span>
                )}
            </label>
            <input
                id={id}
                name={nome}
                type="date"
                required={obrigatorio}
                defaultValue={valor ?? ''}
                aria-invalid={erro ? true : undefined}
                aria-describedby={`${id}-d`}
                className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
            />
            <p id={`${id}-d`} className={`text-xs ${erro ? 'text-vermelho' : 'text-suave'}`}>
                {erro ?? descricao}
            </p>
        </div>
    )
}
