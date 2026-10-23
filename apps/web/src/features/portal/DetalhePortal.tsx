import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { Form } from 'react-aria-components'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { formatarDuracao } from '@/shared/format/duracao'
import { dataHora } from '@/shared/format/prazo'
import { quando } from '@/shared/format/quando'
import { AnexosParaBaixar, CaixaMensagem } from '@/shared/mensagens/Mensagens'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import {
    aprovarEntrega,
    baixarAnexoPortal,
    consultaInteracoesPortal,
    consultaSolicitacaoPortal,
    ESTILO_STATUS_EXTERNO,
    pedirAjuste,
    responderNoPortal,
    STATUS_EXTERNO,
    type SolicitacaoPortal,
} from './consultas'
import { TituloPortal } from './CascaPortal'

const STATUS_TAREFA = {
    A_FAZER: 'A fazer',
    FAZENDO: 'Fazendo',
    CONCLUIDA: 'Concluída',
    CANCELADA: 'Cancelada',
} as const

/**
 * Detalhe da solicitação no portal (F7b): a entrega para aprovar em destaque, o andamento (prazo, responsável, tempo e
 * tarefas que a equipe deixou visíveis) e a conversa só com as mensagens externas. Responder em "Esperando você"
 * retoma o atendimento (contrato §12).
 */
export function DetalhePortal({ id, fuso, voltar = true }: { id: string; fuso: string; voltar?: boolean }) {
    const consulta = useQuery(consultaSolicitacaoPortal(id))
    if (consulta.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (consulta.isError)
        return (
            <Alerta>
                {consulta.error instanceof ErroApi && consulta.error.status === 404
                    ? 'Não encontramos esta solicitação.'
                    : mensagemDeErro(consulta.error)}
            </Alerta>
        )
    const s = consulta.data
    const encerrada = s.statusExterno === 'CONCLUIDA' || s.statusExterno === 'CANCELADA'
    return (
        <>
            {voltar && (
                <Link to="/portal" className="text-sm text-acento-texto hover:underline">
                    ← Solicitações
                </Link>
            )}
            <TituloPortal
                titulo={s.titulo}
                contexto={`#${s.numero} · aberta ${quando(s.abertaEm, fuso)}`}
                acoes={
                    <span className={`rounded-couro px-2 py-1 text-sm ${ESTILO_STATUS_EXTERNO[s.statusExterno]}`}>
                        {STATUS_EXTERNO[s.statusExterno]}
                    </span>
                }
            />
            {s.podeAprovar && <Aprovacao solicitacao={s} />}
            <Andamento solicitacao={s} fuso={fuso} />
            {s.descricao && (
                <section aria-labelledby="portal-descricao" className="flex flex-col gap-1">
                    <h2 id="portal-descricao" className="text-sm font-semibold">
                        O que foi pedido
                    </h2>
                    <p className="text-sm whitespace-pre-line">{s.descricao}</p>
                </section>
            )}
            <Conversa solicitacao={s} fuso={fuso} encerrada={encerrada} />
        </>
    )
}

function Aprovacao({ solicitacao: s }: { solicitacao: SolicitacaoPortal }) {
    const consultas = useQueryClient()
    const [ajuste, setAjuste] = useState(false)
    const atualizar = async (nova: SolicitacaoPortal) => {
        consultas.setQueryData(consultaSolicitacaoPortal(s.id).queryKey, nova)
        await consultas.invalidateQueries({ queryKey: ['portal'] })
    }
    const aprovacao = useMutation({
        mutationFn: () => aprovarEntrega(s.id),
        onSuccess: async (nova) => {
            await atualizar(nova)
            avisar('Entrega aprovada. Obrigado!')
        },
        onError: avisarErro,
    })
    return (
        <section
            aria-labelledby="portal-aprovacao"
            className="flex flex-col gap-3 rounded-couro border-2 border-ambar bg-ambar-suave p-4"
        >
            <h2 id="portal-aprovacao" className="font-semibold">
                A entrega está pronta. Está tudo certo?
            </h2>
            <p className="text-sm">
                Aprovar encerra a solicitação. Se algo não ficou como combinado, peça um ajuste e conte o que falta.
            </p>
            <div className="flex flex-wrap gap-2">
                <Botao isPending={aprovacao.isPending} onPress={() => aprovacao.mutate()}>
                    Aprovar entrega
                </Botao>
                <Botao variante="secundario" onPress={() => setAjuste(true)}>
                    Pedir ajuste
                </Botao>
            </div>
            {ajuste && <JanelaAjuste solicitacao={s} aoFechar={() => setAjuste(false)} aoSalvar={atualizar} />}
        </section>
    )
}

function JanelaAjuste({
    solicitacao: s,
    aoFechar,
    aoSalvar,
}: {
    solicitacao: SolicitacaoPortal
    aoFechar: () => void
    aoSalvar: (nova: SolicitacaoPortal) => Promise<void>
}) {
    const envio = useMutation({
        mutationFn: (motivo: string) => pedirAjuste(s.id, motivo),
        onSuccess: async (nova) => {
            await aoSalvar(nova)
            avisar('Pedido de ajuste enviado. A equipe retoma o atendimento.')
            aoFechar()
        },
    })
    return (
        <Janela titulo="Pedir ajuste" aberta aoMudar={(a) => !a && aoFechar()} ocupada={envio.isPending}>
            <Form
                className="flex flex-col gap-4"
                onSubmit={(evento) => {
                    evento.preventDefault()
                    const motivo = String(new FormData(evento.currentTarget).get('motivo') ?? '').trim()
                    if (motivo) envio.mutate(motivo)
                }}
            >
                {envio.isError && <Alerta>{mensagemDeErro(envio.error) ?? 'Conte o que falta.'}</Alerta>}
                <CampoTexto
                    name="motivo"
                    rotulo="O que precisa ser ajustado?"
                    multilinha
                    isRequired
                    autoFocus
                    descricao="A equipe recebe isso junto com a solicitação reaberta."
                />
                <div className="flex justify-end gap-2">
                    <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao type="submit" isPending={envio.isPending}>
                        Enviar pedido
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}

function Andamento({ solicitacao: s, fuso }: { solicitacao: SolicitacaoPortal; fuso: string }) {
    const tempo = Number(s.tempoVisivelMinutos)
    return (
        <section
            aria-label="Andamento"
            className="grid gap-3 rounded-couro border border-linha bg-superficie p-4 text-sm sm:grid-cols-3"
        >
            <div>
                <p className="text-xs text-suave">Previsão</p>
                <p>
                    {!s.sla
                        ? 'Sem prazo combinado'
                        : s.sla.pausado
                          ? 'Pausada, esperando você'
                          : `até ${dataHora(s.sla.prazoResolucao, fuso)}`}
                </p>
            </div>
            <div>
                <p className="text-xs text-suave">Com quem está</p>
                <p>{s.responsavel?.nome ?? 'Na fila da equipe'}</p>
            </div>
            <div>
                <p className="text-xs text-suave">Tempo dedicado</p>
                <p>{tempo > 0 ? formatarDuracao(tempo) : '—'}</p>
            </div>
            {s.tarefasVisiveis.length > 0 && (
                <div className="sm:col-span-3">
                    <p className="text-xs text-suave">Etapas</p>
                    <ul className="mt-1 flex flex-col gap-1">
                        {s.tarefasVisiveis.map((t, i) => (
                            <li key={i} className="flex items-center justify-between gap-3">
                                <span className={t.status === 'CONCLUIDA' ? 'text-suave line-through' : ''}>
                                    {t.titulo}
                                </span>
                                <span className="text-xs text-suave">{STATUS_TAREFA[t.status]}</span>
                            </li>
                        ))}
                    </ul>
                </div>
            )}
        </section>
    )
}

function Conversa({
    solicitacao: s,
    fuso,
    encerrada,
}: {
    solicitacao: SolicitacaoPortal
    fuso: string
    encerrada: boolean
}) {
    const consultas = useQueryClient()
    const interacoes = useQuery(consultaInteracoesPortal(s.id))
    return (
        <section aria-labelledby="portal-conversa" className="flex flex-col gap-3">
            <h2 id="portal-conversa" className="font-titulo text-xl font-semibold">
                Conversa
            </h2>
            {interacoes.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : interacoes.isError ? (
                <Alerta>{mensagemDeErro(interacoes.error)}</Alerta>
            ) : interacoes.data.length === 0 ? (
                <p className="text-sm text-suave">Nenhuma mensagem ainda.</p>
            ) : (
                <ol className="flex flex-col gap-3">
                    {interacoes.data.map((i) => (
                        <li
                            key={i.id}
                            className={`flex flex-col gap-2 rounded-couro border p-3 text-sm ${i.daEquipe ? 'border-linha bg-superficie' : 'ml-6 border-acento/30 bg-acento-suave/40'}`}
                        >
                            <p className="flex flex-wrap items-baseline gap-x-2 text-xs text-suave">
                                <span className="text-sm font-semibold text-tinta">{i.autorNome}</span>
                                {i.daEquipe && <span>equipe</span>}
                                <time dateTime={i.registradaEm}>{dataHora(i.registradaEm, fuso)}</time>
                            </p>
                            {i.texto && <p className="break-words whitespace-pre-line">{i.texto}</p>}
                            <AnexosParaBaixar anexos={i.anexos} baixar={baixarAnexoPortal} />
                        </li>
                    ))}
                </ol>
            )}
            {encerrada ? (
                <p className="text-sm text-suave">
                    Esta solicitação foi encerrada. Para outro assunto, abra uma nova solicitação.
                </p>
            ) : (
                <CaixaMensagem
                    rotulo="Mensagem para a equipe"
                    equipe={[]}
                    comMencoes={false}
                    placeholder="Escreva para a equipe…"
                    dica={
                        s.statusExterno === 'AGUARDANDO_VOCE' && !s.podeAprovar
                            ? 'A equipe está esperando sua resposta: responder retoma o atendimento.'
                            : 'A equipe recebe na hora.'
                    }
                    rotuloEnviar="Enviar"
                    enviar={async ({ texto, anexos }) => {
                        await responderNoPortal(s.id, texto, anexos)
                        await consultas.invalidateQueries({ queryKey: ['portal'] })
                    }}
                />
            )}
        </section>
    )
}
