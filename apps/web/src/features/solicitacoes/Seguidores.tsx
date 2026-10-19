import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { avisarErro } from '@/shared/avisos/avisos'
import { Botao } from '@/shared/ui/Botao'
import { consultaSeguidores, seguir } from './consultas'

/**
 * Seguidores (contrato §8.1b): quem segue recebe no sino as mensagens e mudanças de status. Quem abriu, o responsável,
 * quem comenta e quem é mencionado passam a seguir sozinhos; aqui a pessoa entra ou sai por conta própria.
 */
export function Seguidores({ solicitacaoId, usuarioId }: { solicitacaoId: string; usuarioId: string }) {
    const consultas = useQueryClient()
    const chave = consultaSeguidores(solicitacaoId).queryKey
    const seguidores = useQuery(consultaSeguidores(solicitacaoId))
    const sigo = (seguidores.data ?? []).some((p) => p.id === usuarioId)
    const mudanca = useMutation({
        mutationFn: (seguindo: boolean) => seguir(solicitacaoId, seguindo),
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
