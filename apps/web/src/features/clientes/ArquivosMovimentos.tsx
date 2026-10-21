import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Button } from 'react-aria-components'
import { STATUS, STATUS_TAREFA, type Status } from '@/features/solicitacoes/rotulos'
import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { baixarAnexo, tamanhoLegivel } from '@/shared/anexos/anexos'
import { avisarErro } from '@/shared/avisos/avisos'
import { dataHora } from '@/shared/format/prazo'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Paginacao } from '@/shared/ui/Paginacao'
import type { Cliente } from './consultas'

type Atividade = components['schemas']['AtividadeDto']
type Alvo = components['schemas']['AlvoAtividadeDto']
type StatusTarefa = components['schemas']['StatusTarefa']

const TAMANHO_ARQUIVOS = 50

/** Link para a solicitação (`#12 Título`) ou a tarefa de um arquivo ou movimento. */
function LinkAlvo({ alvo }: { alvo: Alvo }) {
    return alvo.tipo === 'SOLICITACAO' ? (
        <Link to="/solicitacoes/$id" params={{ id: alvo.id }} className="hover:underline">
            <span className="text-suave tabular-nums">#{alvo.numero}</span> {alvo.titulo}
        </Link>
    ) : (
        <Link to="/tarefas/$id" params={{ id: alvo.id }} className="hover:underline">
            {alvo.titulo}
        </Link>
    )
}

/** Arquivos: todos os anexos das solicitações e tarefas do cliente (contrato §4.3), só leitura, com a página no endereço. */
export function ArquivosCliente({ cliente, fuso, pagina }: { cliente: Cliente; fuso: string; pagina: number }) {
    const arquivos = useQuery({
        queryKey: ['clientes', cliente.id, 'arquivos', pagina],
        queryFn: async () =>
            (
                await api.GET('/api/v1/clientes/{id}/arquivos', {
                    params: { path: { id: cliente.id }, query: { pagina, tamanhoPagina: TAMANHO_ARQUIVOS } },
                })
            ).data!,
    })
    if (arquivos.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (arquivos.isError) return <Alerta>{mensagemDeErro(arquivos.error)}</Alerta>
    if (arquivos.data.itens.length === 0)
        return (
            <EstadoVazio titulo="Nenhum arquivo">
                Os anexos das conversas e dos comentários de tarefas deste cliente aparecem aqui.
            </EstadoVazio>
        )
    return (
        <section aria-label="Arquivos" className="flex flex-col gap-3">
            <ul className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                {arquivos.data.itens.map((a) => (
                    <li key={a.id} className="flex flex-wrap items-baseline gap-x-4 gap-y-1 px-4 py-3">
                        <span className="mr-auto flex min-w-48 flex-col gap-0.5">
                            <Button
                                onPress={() => void baixarAnexo(a.id).catch(avisarErro)}
                                aria-label={`Baixar ${a.nomeArquivo}`}
                                className="w-fit text-left font-semibold text-acento-texto hover:underline"
                            >
                                {a.nomeArquivo}
                            </Button>
                            <span className="text-xs text-suave">
                                <LinkAlvo alvo={a.origem} />
                            </span>
                        </span>
                        {a.interno && (
                            <span className="rounded-couro bg-ambar-suave px-2 py-0.5 text-xs text-ambar-texto">
                                Interno
                            </span>
                        )}
                        <span className="text-xs text-suave">
                            {a.autor ? a.autor.nome || 'Pessoa removida' : 'Sistema'}
                            {a.autorEhCliente && ' (cliente)'} ·{' '}
                            <time dateTime={a.enviadoEm}>{dataHora(a.enviadoEm, fuso)}</time> ·{' '}
                            {tamanhoLegivel(Number(a.tamanhoBytes))}
                        </span>
                    </li>
                ))}
            </ul>
            <Paginacao pagina={pagina} tamanhoPagina={TAMANHO_ARQUIVOS} total={Number(arquivos.data.total)} />
        </section>
    )
}

function responsavel(a: Atividade, artigo: 'a' | 'o'): string {
    const r = a.detalhe?.responsavel
    if (!r) return 'tirou o responsável'
    if (a.autor && r.id === a.autor.id) return `assumiu ${artigo === 'a' ? 'a solicitação' : 'a tarefa'}`
    return `passou para ${r.nome || 'pessoa removida'}`
}

/** O que aconteceu, sem o autor (que vai na frente) nem o alvo (que vai no link). O front monta a frase (§11f). */
function acao(a: Atividade): string {
    const para = a.detalhe?.statusPara
    switch (a.tipo) {
        case 'SOLICITACAO_ABERTA':
            return 'abriu a solicitação'
        case 'SOLICITACAO_ATRIBUIDA':
            return responsavel(a, 'a')
        case 'SOLICITACAO_STATUS':
            return para && para in STATUS ? `mudou para ${STATUS[para as Status].toLowerCase()}` : 'mudou o status'
        case 'RESPOSTA_DO_CLIENTE':
            return 'respondeu pelo portal'
        case 'RESPOSTA_AO_CLIENTE':
            return 'respondeu ao cliente'
        case 'NOTA_INTERNA':
            return 'deixou uma nota interna'
        case 'TAREFA_CRIADA':
            return 'criou a tarefa'
        case 'TAREFA_ATRIBUIDA':
            return responsavel(a, 'o')
        case 'TAREFA_STATUS':
            return para && para in STATUS_TAREFA
                ? `moveu para ${STATUS_TAREFA[para as StatusTarefa].toLowerCase()}`
                : 'mudou o status'
        case 'COMENTARIO_TAREFA':
            return 'comentou'
    }
}

/** Movimentos: o feed do cliente (`GET /atividades?clienteId`), 50 por vez, com "Ver mais" pelo cursor `proximo`. */
export function MovimentosCliente({ cliente, fuso }: { cliente: Cliente; fuso: string }) {
    const feed = useInfiniteQuery({
        queryKey: ['atividades', 'cliente', cliente.id],
        initialPageParam: undefined as number | undefined,
        queryFn: async ({ pageParam }) =>
            (
                await api.GET('/api/v1/atividades', {
                    params: { query: { clienteId: cliente.id, antes: pageParam } },
                })
            ).data!,
        getNextPageParam: (ultima) => (ultima.proximo == null ? undefined : Number(ultima.proximo)),
    })
    if (feed.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (feed.isError) return <Alerta>{mensagemDeErro(feed.error)}</Alerta>
    const itens = feed.data.pages.flatMap((p) => p.itens)
    if (itens.length === 0)
        return (
            <EstadoVazio titulo="Nada nos últimos 90 dias">
                Aberturas, respostas, mudanças de status e comentários deste cliente aparecem aqui.
            </EstadoVazio>
        )
    return (
        <section aria-label="Movimentos" className="flex flex-col gap-3">
            <ol className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                {itens.map((a) => (
                    <li key={a.id} className="flex flex-col gap-0.5 px-4 py-2.5">
                        <LinkAlvo alvo={a.alvo} />
                        <span className="text-xs text-suave">
                            <span className="font-semibold text-tinta">
                                {a.autor ? a.autor.nome || 'Pessoa removida' : 'O sistema'}
                            </span>
                            {a.autorEhCliente && ' (cliente)'} {acao(a)} ·{' '}
                            <time dateTime={a.ocorridaEm}>{dataHora(a.ocorridaEm, fuso)}</time>
                        </span>
                    </li>
                ))}
            </ol>
            {feed.hasNextPage && (
                <Botao
                    variante="secundario"
                    className="self-center"
                    isPending={feed.isFetchingNextPage}
                    onPress={() => void feed.fetchNextPage()}
                >
                    Ver mais
                </Botao>
            )}
            <p className="text-xs text-suave">O histórico de movimentos guarda os últimos 90 dias.</p>
        </section>
    )
}
