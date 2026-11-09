import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { Form } from 'react-aria-components'
import { opcional } from '@/features/clientes/consultas'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import { Marcador } from '@/shared/ui/Marcador'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import { editarSolicitacao, type Solicitacao, type Solucao, type TarefasAbertas } from './consultas'

type TarefaAberta = { id: string; titulo: string }

/**
 * Tarefas abertas na hora de resolver ou cancelar (F11-11, F11-14, F11-20): com `tarefa.editar`, a ação conjunta vem
 * marcada; sem, a lista fica só como aviso. Devolve o pedido para a API (as tarefas que a pessoa viu, F11-31).
 */
function useTarefasAbertas(
    tarefas: TarefaAberta[],
    podeMexer: boolean,
    acao: 'CONCLUIR' | 'CANCELAR',
    aberta: boolean,
) {
    const [junto, setJunto] = useState(true)
    // A janela fica montada fechada: cada abertura volta ao padrão (marcado), mesmo que a anterior tenha desmarcado.
    const [abertaAntes, setAbertaAntes] = useState(aberta)
    if (aberta !== abertaAntes) {
        setAbertaAntes(aberta)
        if (aberta) setJunto(true)
    }
    const pedido: TarefasAbertas | undefined =
        tarefas.length > 0 && podeMexer ? { acao: junto ? acao : 'MANTER', ids: tarefas.map((t) => t.id) } : undefined
    const quadro =
        tarefas.length === 0 ? null : (
            <section aria-label="Tarefas abertas" className="flex flex-col gap-2 rounded-couro border border-linha p-3">
                <p className="text-sm">
                    {tarefas.length === 1
                        ? 'Uma tarefa ainda está aberta:'
                        : `${tarefas.length} tarefas ainda estão abertas:`}
                </p>
                <ul className="list-disc pl-5 text-sm text-suave">
                    {tarefas.map((t) => (
                        <li key={t.id}>{t.titulo}</li>
                    ))}
                </ul>
                {podeMexer ? (
                    <Marcador valor={junto} aoMudar={setJunto}>
                        {acao === 'CONCLUIR'
                            ? 'Concluir as tarefas abertas junto'
                            : 'Cancelar as tarefas abertas junto'}
                    </Marcador>
                ) : (
                    <p className="text-xs text-suave">Elas ficam como estão: mudar tarefas pede outra permissão.</p>
                )}
            </section>
        )
    return { pedido, quadro }
}

/** Título e descrição (o resto do formulário muda direto no painel). */
export function JanelaEdicao({
    solicitacao,
    aberta,
    aoMudar,
    aoSalvar,
}: {
    solicitacao: Solicitacao
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    aoSalvar: (nova: Solicitacao) => void
}) {
    const envio = useMutation({
        mutationFn: (dados: { titulo: string; descricao: string | null }) => editarSolicitacao(solicitacao, dados),
        onSuccess: aoSalvar,
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null
    return (
        <Janela
            titulo="Editar solicitação"
            aberta={aberta}
            aoMudar={(a) => {
                if (!a) envio.reset()
                aoMudar(a)
            }}
            ocupada={envio.isPending}
            larga
        >
            <Form
                className="flex flex-col gap-4"
                validationErrors={errosDeCampo(envio.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    const dados = new FormData(evento.currentTarget)
                    envio.mutate({
                        titulo: String(dados.get('titulo')).trim(),
                        descricao: opcional(dados.get('descricao')),
                    })
                }}
            >
                <LegendaObrigatorios />
                {mensagem && <Alerta>{mensagem}</Alerta>}
                <CampoTexto
                    name="titulo"
                    rotulo="Título"
                    defaultValue={solicitacao.titulo}
                    maxLength={200}
                    isRequired
                    autoFocus
                />
                <CampoTexto
                    name="descricao"
                    rotulo="Descrição"
                    defaultValue={solicitacao.descricao ?? ''}
                    maxLength={10_000}
                    multilinha
                />
                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={() => aoMudar(false)} isDisabled={envio.isPending}>
                        Voltar
                    </Botao>
                    <Botao type="submit" isPending={envio.isPending}>
                        Salvar
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}

/** Resolver pede a solução (o texto é obrigatório; problema e causa ajudam a achar semelhantes depois). */
export function JanelaResolucao({
    aberta,
    aoMudar,
    ocupada,
    erro,
    aoResolver,
    tarefasAbertas = [],
    podeMexerTarefas = false,
}: {
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    ocupada: boolean
    erro: unknown
    aoResolver: (solucao: Solucao, tarefas?: TarefasAbertas) => void
    tarefasAbertas?: TarefaAberta[]
    podeMexerTarefas?: boolean
}) {
    const mensagem = erro ? mensagemDeErro(erro) : null
    const tarefas = useTarefasAbertas(tarefasAbertas, podeMexerTarefas, 'CONCLUIR', aberta)
    return (
        <Janela titulo="Resolver solicitação" aberta={aberta} aoMudar={aoMudar} ocupada={ocupada} larga>
            <Form
                className="flex flex-col gap-4"
                validationErrors={prefixar(errosDeCampo(erro))}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    const dados = new FormData(evento.currentTarget)
                    aoResolver(
                        {
                            texto: String(dados.get('texto')).trim(),
                            problema: opcional(dados.get('problema')),
                            causa: opcional(dados.get('causa')),
                        },
                        tarefas.pedido,
                    )
                }}
            >
                <p className="text-sm text-suave">
                    A solução fica só para a equipe e aparece nas solicitações parecidas. O cliente recebe o aviso de
                    que foi resolvida e pode aprovar ou reprovar no portal.
                </p>
                <LegendaObrigatorios />
                {mensagem && <Alerta>{mensagem}</Alerta>}
                <CampoTexto
                    name="texto"
                    rotulo="Solução"
                    descricao="O que foi feito."
                    maxLength={10_000}
                    multilinha
                    isRequired
                    autoFocus
                />
                <div className="grid gap-4 sm:grid-cols-2">
                    <CampoTexto name="problema" rotulo="Problema" maxLength={2_000} />
                    <CampoTexto name="causa" rotulo="Causa" maxLength={2_000} />
                </div>
                {tarefas.quadro}
                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={() => aoMudar(false)} isDisabled={ocupada}>
                        Voltar
                    </Botao>
                    <Botao type="submit" isPending={ocupada}>
                        Resolver
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}

/** Os erros de campo da resolução vêm como `solucao.texto`; no formulário o campo é só `texto`. */
function prefixar(erros: Record<string, string[]>): Record<string, string[]> {
    return Object.fromEntries(Object.entries(erros).map(([campo, msgs]) => [campo.replace(/^solucao\./, ''), msgs]))
}

/** Reabrir e cancelar pedem motivo; cancelar é a confirmação do que não tem volta (convenções §4). */
export function JanelaMotivo({
    titulo,
    acao,
    explicacao,
    perigosa = false,
    aberta,
    aoMudar,
    ocupada,
    erro,
    aoConfirmar,
    tarefasAbertas = [],
    podeMexerTarefas = false,
    acaoTarefas,
}: {
    titulo: string
    acao: string
    explicacao: string
    perigosa?: boolean
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    ocupada: boolean
    erro: unknown
    aoConfirmar: (motivo: string, tarefas?: TarefasAbertas) => void
    tarefasAbertas?: TarefaAberta[]
    podeMexerTarefas?: boolean
    /** Só no cancelamento (F11-20); sem, a janela não fala de tarefas. */
    acaoTarefas?: 'CANCELAR'
}) {
    const mensagem = erro ? mensagemDeErro(erro) : null
    const tarefas = useTarefasAbertas(
        acaoTarefas ? tarefasAbertas : [],
        podeMexerTarefas,
        acaoTarefas ?? 'CANCELAR',
        aberta,
    )
    return (
        <Janela titulo={titulo} aberta={aberta} aoMudar={aoMudar} ocupada={ocupada}>
            <Form
                className="flex flex-col gap-4"
                validationErrors={errosDeCampo(erro)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    aoConfirmar(String(new FormData(evento.currentTarget).get('motivo')).trim(), tarefas.pedido)
                }}
            >
                <p className="text-sm text-suave">{explicacao}</p>
                {mensagem && <Alerta>{mensagem}</Alerta>}
                <CampoTexto name="motivo" rotulo="Motivo" maxLength={1_000} multilinha isRequired autoFocus />
                {tarefas.quadro}
                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={() => aoMudar(false)} isDisabled={ocupada}>
                        Voltar
                    </Botao>
                    <Botao
                        type="submit"
                        isPending={ocupada}
                        className={perigosa ? '!bg-vermelho !text-botao-tinta' : undefined}
                    >
                        {acao}
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}
