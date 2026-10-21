import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/cliente'
import { avisarErro } from '../avisos/avisos'
import { Botao } from '../ui/Botao'

/** Solicitação ou tarefa: as rotas e a chave começam pelo recurso, para o sinal de tempo real invalidar. */
export type AlvoSeguido = 'solicitacoes' | 'tarefas'

export function consultaSeguidores(alvo: AlvoSeguido, id: string) {
    return queryOptions({
        queryKey: [alvo, 'seguidores', id],
        queryFn: async () =>
            alvo === 'solicitacoes'
                ? (await api.GET('/api/v1/solicitacoes/{id}/seguidores', { params: { path: { id } } })).data!
                : (await api.GET('/api/v1/tarefas/{id}/seguidores', { params: { path: { id } } })).data!,
    })
}

async function seguir(alvo: AlvoSeguido, id: string, seguindo: boolean): Promise<void> {
    const opcoes = { params: { path: { id } } }
    if (alvo === 'solicitacoes') {
        if (seguindo) await api.PUT('/api/v1/solicitacoes/{id}/seguidores/eu', opcoes)
        else await api.DELETE('/api/v1/solicitacoes/{id}/seguidores/eu', opcoes)
    } else if (seguindo) await api.PUT('/api/v1/tarefas/{id}/seguidores/eu', opcoes)
    else await api.DELETE('/api/v1/tarefas/{id}/seguidores/eu', opcoes)
}

/**
 * Seguidores (contrato §8.1b): quem segue recebe no sino as mensagens, os comentários e as mudanças de status. Quem abriu, o responsável,
 * quem comenta e quem é mencionado passam a seguir sozinhos; aqui a pessoa entra ou sai por conta própria.
 */
export function Seguidores({ alvo, id, usuarioId }: { alvo: AlvoSeguido; id: string; usuarioId: string }) {
    const consultas = useQueryClient()
    const chave = consultaSeguidores(alvo, id).queryKey
    const seguidores = useQuery(consultaSeguidores(alvo, id))
    const sigo = (seguidores.data ?? []).some((p) => p.id === usuarioId)
    const mudanca = useMutation({
        mutationFn: (seguindo: boolean) => seguir(alvo, id, seguindo),
        onSettled: () => consultas.invalidateQueries({ queryKey: chave }),
        onError: avisarErro,
    })

    const nomes = (seguidores.data ?? []).map((p) => (p.id === usuarioId ? 'você' : p.nome))
    return (
        <div className="flex flex-col gap-1">
            <p className="text-sm font-semibold">Seguidores</p>
            <div className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2 text-sm">
                <span className={nomes.length ? '' : 'text-suave'}>
                    {seguidores.isPending ? '…' : nomes.length ? nomes.join(', ') : 'Ninguém segue'}
                </span>
                {seguidores.isSuccess && (
                    <Botao
                        variante="discreto"
                        className="!px-0"
                        isPending={mudanca.isPending}
                        onPress={() => mudanca.mutate(!sigo)}
                    >
                        {sigo ? 'Deixar de seguir' : 'Seguir'}
                    </Botao>
                )}
            </div>
        </div>
    )
}
