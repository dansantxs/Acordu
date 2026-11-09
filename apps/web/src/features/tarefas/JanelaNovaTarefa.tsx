import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Checkbox, Form } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { opcional } from '@/features/clientes/consultas'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { consultaCronometro, iniciarCronometro } from '@/shared/cronometro/cronometro'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { lerDuracao } from '@/shared/format/duracao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import { Marcador } from '@/shared/ui/Marcador'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import { criarTarefa, type Tarefa } from './consultas'

/** De onde a tarefa nasce quando vem do painel da solicitação ou do projeto (o cliente é herdado pela API). */
export type OrigemTarefa =
    | { tipo: 'solicitacao'; id: string; numero: number | string; clienteNome: string }
    | { tipo: 'projeto'; id: string; nome: string; clienteNome: string }

/**
 * Nova tarefa (RF-TAR-01, F3b): avulsa (com ou sem cliente) pela tela de tarefas ou pelo Ctrl+K, ou de uma solicitação
 * ou de um projeto pelo painel deles.
 */
export function JanelaNovaTarefa({
    perfil,
    aberta,
    aoMudar,
    origem,
    aoCriar,
}: {
    perfil: Perfil
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    origem?: OrigemTarefa
    aoCriar: (tarefa: Tarefa) => void
}) {
    return (
        <Janela titulo="Nova tarefa" aberta={aberta} aoMudar={aoMudar} larga>
            {aberta && <Formulario perfil={perfil} origem={origem} aoFechar={() => aoMudar(false)} aoCriar={aoCriar} />}
        </Janela>
    )
}

function Formulario({
    perfil,
    origem,
    aoFechar,
    aoCriar,
}: {
    perfil: Perfil
    origem?: OrigemTarefa
    aoFechar: () => void
    aoCriar: (tarefa: Tarefa) => void
}) {
    const consultas = useQueryClient()
    const equipe = useQuery(consultaEquipe)
    const [clienteId, setClienteId] = useState<string | null>(null)
    const [responsavel, setResponsavel] = useState<string | null>(null)
    // Tarefa de solicitação aparece no portal por padrão (o cliente acompanha o andamento); avulsa e de projeto não.
    const [visivel, setVisivel] = useState(origem?.tipo === 'solicitacao')
    const [estimativaInvalida, setEstimativaInvalida] = useState(false)
    const temCliente = Boolean(origem) || clienteId !== null
    // Iniciar junto (F12-09 a F12-12): só com `apontamento.criar`, e desabilitado com outro cronômetro correndo.
    const podeCronometrar = pode(perfil, 'apontamento.criar')
    const cronometro = useQuery({ ...consultaCronometro, enabled: podeCronometrar })
    const outroCronometro = cronometro.data ?? null
    const [cronometrar, setCronometrar] = useState(false)
    const iniciarJunto = podeCronometrar && cronometrar && !outroCronometro

    const envio = useMutation({
        mutationFn: async (dados: Parameters<typeof criarTarefa>[0]) => {
            const tarefa = await criarTarefa(dados)
            if (!iniciarJunto) return { tarefa, cronometro: 'nao-pedido' as const }
            // A tarefa já existe: se iniciar falhar, ela continua criada e o aviso diz isso (F12-12, decisão D3).
            try {
                await iniciarCronometro(tarefa.id)
                return { tarefa, cronometro: 'iniciado' as const }
            } catch (erro) {
                return { tarefa, cronometro: erro }
            }
        },
        onSuccess: async ({ tarefa, cronometro: resultado }) => {
            await consultas.invalidateQueries({ queryKey: ['tarefas'] })
            if (origem?.tipo === 'solicitacao') await consultas.invalidateQueries({ queryKey: ['solicitacoes'] })
            if (origem?.tipo === 'projeto') await consultas.invalidateQueries({ queryKey: ['projetos'] })
            if (resultado !== 'nao-pedido') await consultas.invalidateQueries({ queryKey: ['cronometro'] })
            if (resultado === 'nao-pedido') avisar(`Tarefa "${tarefa.titulo}" criada.`)
            else if (resultado === 'iniciado') avisar(`Tarefa "${tarefa.titulo}" criada, com o cronômetro correndo.`)
            else avisar(`Tarefa "${tarefa.titulo}" criada, mas o cronômetro não começou: ${mensagemDeErro(resultado)}`)
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
                    solicitacaoId: origem?.tipo === 'solicitacao' ? origem.id : null,
                    projetoId: origem?.tipo === 'projeto' ? origem.id : null,
                    clienteId: origem ? null : clienteId,
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
            {origem && (
                <p className="text-sm text-suave">
                    {origem.tipo === 'solicitacao' ? `Da solicitação #${origem.numero}` : `Do projeto ${origem.nome}`} ·{' '}
                    {origem.clienteNome}
                </p>
            )}
            <CampoTexto name="titulo" rotulo="Título" maxLength={200} isRequired autoFocus />
            <CampoTexto name="descricao" rotulo="Descrição" maxLength={10_000} multilinha />
            <div className="grid gap-4 sm:grid-cols-2">
                {!origem && pode(perfil, 'cliente.ver') && (
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
            {podeCronometrar && (
                <div className="flex flex-col gap-1">
                    <Marcador valor={iniciarJunto} aoMudar={setCronometrar} isDisabled={outroCronometro !== null}>
                        Iniciar o cronômetro
                    </Marcador>
                    {outroCronometro && (
                        <p className="text-xs text-suave">Já há um cronômetro em "{outroCronometro.tarefa.nome}".</p>
                    )}
                </div>
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
