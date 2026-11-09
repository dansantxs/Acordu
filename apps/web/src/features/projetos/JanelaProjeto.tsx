import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Form } from 'react-aria-components'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { consultaContratosAtivos } from '@/features/solicitacoes/consultas'
import { TIPO_CONTRATO } from '@/features/solicitacoes/rotulos'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar } from '@/shared/avisos/avisos'
import { dataCurta } from '@/shared/format/datas'
import { formatarDuracao, lerDuracao } from '@/shared/format/duracao'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import { Marcador } from '@/shared/ui/Marcador'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import { criarProjeto, editarProjeto, type Projeto } from './consultas'

/** Projeto novo a partir de uma solicitação (F11-21, F11-22): cliente fixo, contrato e nome sugeridos. */
export interface ProjetoDaSolicitacao {
    cliente: { id: string; nome: string }
    contratoId: string | null
    nome: string
    /** Abertas e sem projeto: vão marcadas com "Levar para o projeto". */
    tarefas: { id: string; titulo: string }[]
    /** Levar tarefas pede `tarefa.editar`; sem, a lista não aparece. */
    podeLevarTarefas: boolean
}

/**
 * Criar ou editar projeto (RF-PRJ-01): cliente (fixo depois de criado), nome, contrato do cliente (as horas das
 * tarefas consomem dele), início planejado, prazo e estimativa total.
 */
export function JanelaProjeto({
    aberta,
    aoMudar,
    projeto,
    daSolicitacao,
    aoSalvar,
}: {
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    projeto?: Projeto
    daSolicitacao?: ProjetoDaSolicitacao
    aoSalvar: (projeto: Projeto) => void
}) {
    return (
        <Janela titulo={projeto ? 'Editar projeto' : 'Novo projeto'} aberta={aberta} aoMudar={aoMudar} larga>
            {aberta && (
                <Formulario
                    projeto={projeto}
                    {...(daSolicitacao ? { daSolicitacao } : {})}
                    aoFechar={() => aoMudar(false)}
                    aoSalvar={aoSalvar}
                />
            )}
        </Janela>
    )
}

function Formulario({
    projeto: p,
    daSolicitacao: origem,
    aoFechar,
    aoSalvar,
}: {
    projeto?: Projeto
    daSolicitacao?: ProjetoDaSolicitacao
    aoFechar: () => void
    aoSalvar: (projeto: Projeto) => void
}) {
    const consultas = useQueryClient()
    const [clienteId, setClienteId] = useState<string | null>(p?.cliente.id ?? origem?.cliente.id ?? null)
    const [escolhido, setContrato] = useState<string | null>(p?.contratoId ?? origem?.contratoId ?? null)
    const [estimativaInvalida, setEstimativaInvalida] = useState(false)
    const [levar, setLevar] = useState(true)
    const contratos = useQuery({ ...consultaContratosAtivos(clienteId ?? ''), enabled: clienteId !== null })
    // O contrato da solicitação pode ter encerrado: só vale o sugerido se ainda estiver ativo.
    const contrato =
        origem && !p && contratos.isSuccess && !contratos.data.some((c) => c.id === escolhido) ? null : escolhido
    const tarefasLevadas = origem?.podeLevarTarefas && levar ? origem.tarefas.map((t) => t.id) : []

    const envio = useMutation({
        mutationFn: (dados: {
            nome: string
            inicio: string | null
            prazo: string | null
            estimativaMinutos: number | null
        }) => {
            const comuns = { ...dados, contratoId: contrato }
            if (p) return editarProjeto(p, comuns)
            return criarProjeto({
                ...comuns,
                clienteId: clienteId!,
                ...(tarefasLevadas.length > 0 ? { tarefaIds: tarefasLevadas } : {}),
            })
        },
        onSuccess: async (salvo) => {
            await consultas.invalidateQueries({ queryKey: ['projetos'] })
            avisar(p ? 'Projeto atualizado.' : `Projeto "${salvo.nome}" criado.`)
            aoFechar()
            aoSalvar(salvo)
        },
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null
    // Datas são <input type="date"> nativos: o erro de campo da API (início depois do prazo) vai embaixo à mão.
    const erroDe = (campo: string) => (envio.error instanceof ErroApi ? envio.error.doCampo(campo) : undefined)

    return (
        <Form
            className="flex flex-col gap-4"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                if (!clienteId) return
                const dados = new FormData(evento.currentTarget)
                const textoEstimativa = String(dados.get('estimativa') ?? '').trim()
                const estimativa = textoEstimativa ? lerDuracao(textoEstimativa) : null
                setEstimativaInvalida(Boolean(textoEstimativa) && estimativa === null)
                if (textoEstimativa && estimativa === null) return
                const data = (nome: string) => String(dados.get(nome) ?? '') || null
                envio.mutate({
                    nome: String(dados.get('nome')).trim(),
                    inicio: data('inicio'),
                    prazo: data('prazo'),
                    estimativaMinutos: estimativa,
                })
            }}
        >
            <LegendaObrigatorios />
            {mensagem && <Alerta>{mensagem}</Alerta>}
            {p || origem ? (
                <p className="text-sm text-suave">Cliente: {(p?.cliente ?? origem!.cliente).nome}</p>
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
            <CampoTexto
                name="nome"
                rotulo="Nome"
                defaultValue={p?.nome ?? origem?.nome ?? ''}
                maxLength={200}
                isRequired
                autoFocus
            />
            {clienteId && (
                <CampoSelecao
                    name="contratoId"
                    rotulo="Contrato"
                    descricao="As horas das tarefas do projeto consomem deste contrato."
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
            <div className="grid gap-4 sm:grid-cols-3">
                <CampoData nome="inicio" rotulo="Início planejado" valor={p?.inicio ?? null} erro={erroDe('inicio')} />
                <CampoData nome="prazo" rotulo="Prazo" valor={p?.prazo ?? null} erro={erroDe('prazo')} />
                <div className="flex flex-col gap-1">
                    <label htmlFor="projeto-estimativa" className="text-sm font-semibold">
                        Estimativa total
                    </label>
                    <input
                        id="projeto-estimativa"
                        name="estimativa"
                        placeholder="120h"
                        defaultValue={p?.estimativaMinutos != null ? formatarDuracao(Number(p.estimativaMinutos)) : ''}
                        aria-invalid={estimativaInvalida || undefined}
                        aria-describedby="projeto-estimativa-d"
                        className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
                    />
                    <p
                        id="projeto-estimativa-d"
                        className={`text-xs ${estimativaInvalida ? 'text-vermelho' : 'text-suave'}`}
                    >
                        {estimativaInvalida ? 'Não entendi. Use 120h, 1h30 ou 90.' : 'Em horas: 120h, 1h30…'}
                    </p>
                </div>
            </div>
            {origem?.podeLevarTarefas && origem.tarefas.length > 0 && (
                <section
                    aria-label="Tarefas da solicitação"
                    className="flex flex-col gap-2 rounded-couro border border-linha p-3"
                >
                    <p className="text-sm">
                        {origem.tarefas.length === 1
                            ? 'Uma tarefa da solicitação está aberta e sem projeto:'
                            : `${origem.tarefas.length} tarefas da solicitação estão abertas e sem projeto:`}
                    </p>
                    <ul className="list-disc pl-5 text-sm text-suave">
                        {origem.tarefas.map((t) => (
                            <li key={t.id}>{t.titulo}</li>
                        ))}
                    </ul>
                    <Marcador valor={levar} aoMudar={setLevar}>
                        Levar para o projeto
                    </Marcador>
                    <p className="text-xs text-suave">Elas continuam ligadas à solicitação.</p>
                </section>
            )}
            <div className="flex justify-end gap-3">
                <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                    Cancelar
                </Botao>
                <Botao type="submit" isPending={envio.isPending}>
                    {p ? 'Salvar' : 'Criar projeto'}
                </Botao>
            </div>
        </Form>
    )
}

/** Data com o erro de campo da API embaixo (o `inicio` depois do prazo volta em `errors.inicio`). */
function CampoData({
    nome,
    rotulo,
    valor,
    erro,
}: {
    nome: string
    rotulo: string
    valor: string | null
    erro?: string
}) {
    const id = `projeto-${nome}`
    return (
        <div className="flex flex-col gap-1">
            <label htmlFor={id} className="text-sm font-semibold">
                {rotulo}
            </label>
            <input
                id={id}
                name={nome}
                type="date"
                defaultValue={valor ?? ''}
                aria-invalid={erro ? true : undefined}
                aria-describedby={erro ? `${id}-erro` : undefined}
                className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
            />
            {erro && (
                <p id={`${id}-erro`} className="text-sm text-vermelho">
                    {erro}
                </p>
            )}
        </div>
    )
}
