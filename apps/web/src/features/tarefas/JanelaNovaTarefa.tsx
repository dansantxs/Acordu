import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Checkbox, Form } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { opcional } from '@/features/clientes/consultas'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { lerDuracao } from '@/shared/format/duracao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import { criarTarefa, type Tarefa } from './consultas'

/** De onde a tarefa nasce quando vem do painel da solicitação (o cliente é herdado pela API). */
export interface OrigemSolicitacao {
    id: string
    numero: number | string
    clienteNome: string
}

/**
 * Nova tarefa (RF-TAR-01, F3b): avulsa (com ou sem cliente) pela tela de tarefas ou pelo Ctrl+K, ou de uma solicitação
 * pelo painel dela. Projeto entra no F3e.
 */
export function JanelaNovaTarefa({
    perfil,
    aberta,
    aoMudar,
    solicitacao,
    aoCriar,
}: {
    perfil: Perfil
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    solicitacao?: OrigemSolicitacao
    aoCriar: (tarefa: Tarefa) => void
}) {
    return (
        <Janela titulo="Nova tarefa" aberta={aberta} aoMudar={aoMudar} larga>
            {aberta && (
                <Formulario
                    perfil={perfil}
                    solicitacao={solicitacao}
                    aoFechar={() => aoMudar(false)}
                    aoCriar={aoCriar}
                />
            )}
        </Janela>
    )
}

function Formulario({
    perfil,
    solicitacao,
    aoFechar,
    aoCriar,
}: {
    perfil: Perfil
    solicitacao?: OrigemSolicitacao
    aoFechar: () => void
    aoCriar: (tarefa: Tarefa) => void
}) {
    const consultas = useQueryClient()
    const equipe = useQuery(consultaEquipe)
    const [clienteId, setClienteId] = useState<string | null>(null)
    const [responsavel, setResponsavel] = useState<string | null>(null)
    // Tarefa de solicitação aparece no portal por padrão (o cliente acompanha o andamento); avulsa não.
    const [visivel, setVisivel] = useState(Boolean(solicitacao))
    const [estimativaInvalida, setEstimativaInvalida] = useState(false)
    const temCliente = Boolean(solicitacao) || clienteId !== null

    const envio = useMutation({
        mutationFn: criarTarefa,
        onSuccess: async (tarefa) => {
            await consultas.invalidateQueries({ queryKey: ['tarefas'] })
            if (solicitacao) await consultas.invalidateQueries({ queryKey: ['solicitacoes'] })
            avisar(`Tarefa "${tarefa.titulo}" criada.`)
            aoFechar()
            aoCriar(tarefa)
        },
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null

    return (
        <Form
            className="flex flex-col gap-4"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                const dados = new FormData(evento.currentTarget)
                const textoEstimativa = String(dados.get('estimativa') ?? '').trim()
                const estimativa = textoEstimativa ? lerDuracao(textoEstimativa) : null
                setEstimativaInvalida(Boolean(textoEstimativa) && estimativa === null)
                if (textoEstimativa && estimativa === null) return
                envio.mutate({
                    titulo: String(dados.get('titulo')).trim(),
                    descricao: opcional(dados.get('descricao')),
                    solicitacaoId: solicitacao?.id ?? null,
                    projetoId: null,
                    clienteId: solicitacao ? null : clienteId,
                    responsavelId: responsavel,
                    prazo: opcional(dados.get('prazo')),
                    planejadaPara: opcional(dados.get('planejadaPara')),
                    estimativaMinutos: estimativa,
                    visivelCliente: temCliente && visivel,
                })
            }}
        >
            <LegendaObrigatorios />
            {mensagem && <Alerta>{mensagem}</Alerta>}
            {solicitacao && (
                <p className="text-sm text-suave">
                    Da solicitação #{solicitacao.numero} · {solicitacao.clienteNome}
                </p>
            )}
            <CampoTexto name="titulo" rotulo="Título" maxLength={200} isRequired autoFocus />
            <CampoTexto name="descricao" rotulo="Descrição" maxLength={10_000} multilinha />
            <div className="grid gap-4 sm:grid-cols-2">
                {!solicitacao && pode(perfil, 'cliente.ver') && (
                    <CampoCliente
                        name="clienteId"
                        rotulo="Cliente"
                        valor={clienteId}
                        aoMudar={setClienteId}
                        vazio="Sem cliente (interna)"
                        descricao="Sem cliente, a tarefa é interna."
                    />
                )}
                <CampoSelecao
                    name="responsavelId"
                    rotulo="Responsável"
                    opcoes={(equipe.data ?? []).map((p) => ({ id: p.id, rotulo: p.nome }))}
                    valor={responsavel}
                    aoMudar={setResponsavel}
                    vazio="Sem responsável"
                />
            </div>
            <div className="grid gap-4 sm:grid-cols-3">
                <div className="flex flex-col gap-1">
                    <label htmlFor="nova-prazo" className="text-sm font-semibold">
                        Prazo
                    </label>
                    <input
                        id="nova-prazo"
                        name="prazo"
                        type="date"
                        className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta"
                    />
                </div>
                <div className="flex flex-col gap-1">
                    <label htmlFor="nova-planejada" className="text-sm font-semibold">
                        Planejada para
                    </label>
                    <input
                        id="nova-planejada"
                        name="planejadaPara"
                        type="date"
                        className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta"
                    />
                </div>
                <div className="flex flex-col gap-1">
                    <label htmlFor="nova-estimativa" className="text-sm font-semibold">
                        Estimativa
                    </label>
                    <input
                        id="nova-estimativa"
                        name="estimativa"
                        placeholder="1h30"
                        aria-invalid={estimativaInvalida || undefined}
                        aria-describedby="nova-estimativa-d"
                        className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
                    />
                    <p
                        id="nova-estimativa-d"
                        className={`text-xs ${estimativaInvalida ? 'text-vermelho' : 'text-suave'}`}
                    >
                        {estimativaInvalida ? 'Não entendi. Use 1h30, 1:30 ou 90.' : '1h30, 1:30 ou 90 (minutos).'}
                    </p>
                </div>
            </div>
            {temCliente && (
                <Checkbox isSelected={visivel} onChange={setVisivel} className="group flex items-center gap-2 text-sm">
                    <span
                        aria-hidden="true"
                        className="grid size-4 place-items-center rounded border border-linha bg-superficie group-data-[selected]:border-acento group-data-[selected]:bg-acento group-data-[focus-visible]:ring-2 group-data-[focus-visible]:ring-acento"
                    >
                        <svg
                            viewBox="0 0 12 12"
                            className="size-3 text-botao-tinta opacity-0 group-data-[selected]:opacity-100"
                        >
                            <path d="M2.5 6.5 5 9l4.5-6" fill="none" stroke="currentColor" strokeWidth="2" />
                        </svg>
                    </span>
                    O cliente vê esta tarefa no portal
                </Checkbox>
            )}
            <div className="flex justify-end gap-3">
                <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                    Cancelar
                </Botao>
                <Botao type="submit" isPending={envio.isPending}>
                    Criar tarefa
                </Botao>
            </div>
        </Form>
    )
}
