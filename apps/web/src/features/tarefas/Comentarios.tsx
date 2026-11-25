import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { dataHora } from '@/shared/format/prazo'
import { AnexosParaBaixar, CaixaMensagem, TextoComMencoes } from '@/shared/mensagens/Mensagens'
import { Alerta } from '@/shared/ui/Alerta'
import { BotaoTexto } from '@/shared/ui/Botao'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { comentar, consultaComentarios, excluirComentario, type Comentario } from './consultas'

/**
 * Comentários da tarefa (RF-COL-01, contrato §8.1a): só da equipe, nunca no portal; texto simples com @menções e
 * anexos, como as notas internas da solicitação. Imutáveis; só o autor exclui (com confirmação, não tem volta).
 */
export function Comentarios({
    tarefaId,
    fuso,
    usuarioId,
    podeComentar,
}: {
    tarefaId: string
    fuso: string
    usuarioId: string
    podeComentar: boolean
}) {
    const consultas = useQueryClient()
    const comentarios = useQuery(consultaComentarios(tarefaId))
    const equipe = useQuery(consultaEquipe)
    const nomes = useMemo(() => new Map((equipe.data ?? []).map((p) => [p.id.toLowerCase(), p.nome])), [equipe.data])
    const [excluindo, setExcluindo] = useState<Comentario | null>(null)
    const exclusao = useMutation({
        mutationFn: (c: Comentario) => excluirComentario(tarefaId, c.id),
        onSuccess: async () => {
            setExcluindo(null)
            avisar('Comentário excluído.')
            await consultas.invalidateQueries({ queryKey: consultaComentarios(tarefaId).queryKey })
        },
    })

    return (
        <section aria-labelledby="comentarios" className="flex flex-col gap-3">
            <h3 id="comentarios" className="text-sm font-semibold">
                Comentários
            </h3>
            {comentarios.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : comentarios.isError ? (
                <Alerta>{mensagemDeErro(comentarios.error)}</Alerta>
            ) : comentarios.data.length === 0 ? (
                <p className="text-sm text-suave">Nenhum comentário ainda.</p>
            ) : (
                <ol className="flex flex-col gap-3">
                    {comentarios.data.map((c) => (
                        <li
                            key={c.id}
                            className="flex flex-col gap-2 rounded-couro border border-linha bg-superficie p-3 text-sm"
                        >
                            <p className="flex flex-wrap items-baseline gap-x-2 text-xs text-suave">
                                <span className="text-sm font-semibold text-tinta">
                                    {c.autor.nome || 'Pessoa removida'}
                                </span>
                                <time dateTime={c.registradoEm}>{dataHora(c.registradoEm, fuso)}</time>
                                {c.autor.id === usuarioId && podeComentar && (
                                    <BotaoTexto
                                        grava
                                        onPress={() => {
                                            exclusao.reset()
                                            setExcluindo(c)
                                        }}
                                        className="ml-auto text-xs text-suave hover:text-vermelho hover:underline"
                                    >
                                        Excluir
                                    </BotaoTexto>
                                )}
                            </p>
                            {c.texto && <TextoComMencoes texto={c.texto} nomes={nomes} />}
                            <AnexosParaBaixar anexos={c.anexos} />
                        </li>
                    ))}
                </ol>
            )}
            {podeComentar && (
                <CaixaMensagem
                    rotulo="Comentário"
                    equipe={equipe.data ?? []}
                    comMencoes
                    placeholder="Só a equipe vê. Use @ para chamar alguém."
                    dica="Comentário interno: o cliente não vê."
                    rotuloEnviar="Comentar"
                    enviar={async ({ texto, anexos }) => {
                        await comentar(tarefaId, texto, anexos)
                        // Comentar faz seguir a tarefa (contrato §8.1b).
                        await consultas.invalidateQueries({ queryKey: ['tarefas'] })
                    }}
                />
            )}
            <Confirmacao
                titulo="Excluir o comentário?"
                acao="Excluir comentário"
                aberta={excluindo !== null}
                aoMudar={(a) => !a && setExcluindo(null)}
                aoConfirmar={() => excluindo && exclusao.mutate(excluindo)}
                ocupada={exclusao.isPending}
                erro={exclusao.isError ? mensagemDeErro(exclusao.error) : null}
            >
                <p>O comentário e os anexos dele saem da tarefa e não dá para recuperar.</p>
            </Confirmacao>
        </section>
    )
}
