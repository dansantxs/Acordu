import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useRouter } from '@tanstack/react-router'
import { Button, Dialog, DialogTrigger, Heading, Popover } from 'react-aria-components'
import { api } from '@/shared/api/cliente'
import { avisarErro } from '@/shared/avisos/avisos'
import type { components } from '@/shared/api/interno'
import { quando } from '@/shared/format/quando'
import { Icone } from '@/shared/ui/Icone'

type Notificacao = components['schemas']['NotificacaoDto']

export const consultaSino = {
    queryKey: ['notificacoes', 'sino'] as const,
    queryFn: async () => (await api.GET('/api/v1/notificacoes', { params: { query: { tamanhoPagina: 10 } } })).data!,
}

/** Caminho interno do link de um aviso (a API manda a URL pública completa). */
export function caminhoDoLink(link: string | null): string | null {
    if (!link) return null
    try {
        const url = new URL(link, window.location.origin)
        return url.pathname + url.search
    } catch {
        return null
    }
}

/** Sino da capa: os 10 avisos mais recentes. Abrir um marca como lido e vai para o item. */
export function Sino({ fuso }: { fuso: string }) {
    const consultas = useQueryClient()
    const roteador = useRouter()
    // Sem sinal de tempo real para avisos (ADR 0009 só cobre itens): consulta a cada minuto e ao focar a janela.
    const sino = useQuery({ ...consultaSino, refetchInterval: 60_000 })
    const atualizar = () => consultas.invalidateQueries({ queryKey: ['notificacoes'] })
    const lerUm = useMutation({
        mutationFn: (id: string) => api.POST('/api/v1/notificacoes/{id}/leitura', { params: { path: { id } } }),
        onSettled: atualizar,
    })
    const lerTodos = useMutation({
        mutationFn: () => api.POST('/api/v1/notificacoes/leitura'),
        onError: avisarErro,
        onSettled: atualizar,
    })

    const naoLidas = Number(sino.data?.naoLidas ?? 0)
    const rotulo = naoLidas > 0 ? `Avisos, ${naoLidas} não lidos` : 'Avisos'

    function abrir(aviso: Notificacao, fechar: () => void) {
        if (!aviso.lidaEm) lerUm.mutate(aviso.id)
        const caminho = caminhoDoLink(aviso.link)
        fechar()
        if (caminho) roteador.history.push(caminho)
    }

    return (
        <DialogTrigger>
            <Button
                aria-label={rotulo}
                className="relative grid size-9 place-items-center rounded-couro text-lateral-tinta hover:bg-lateral-ativo"
            >
                <Icone nome="sino" className="size-5" />
                {naoLidas > 0 && (
                    <span
                        aria-hidden="true"
                        className="absolute -top-0.5 -right-0.5 min-w-4 rounded-full bg-dourado px-1 text-center text-[11px] leading-4 font-semibold text-lateral"
                    >
                        {naoLidas > 99 ? '99+' : naoLidas}
                    </span>
                )}
            </Button>
            <Popover
                placement="bottom start"
                className="w-[22rem] max-w-[calc(100vw-2rem)] rounded-couro border border-linha bg-superficie text-tinta shadow-xl"
            >
                <Dialog className="outline-none">
                    {({ close }) => (
                        <div className="flex flex-col">
                            <div className="flex items-center justify-between border-b border-linha px-4 py-3">
                                <Heading slot="title" className="font-titulo text-lg font-semibold">
                                    Avisos
                                </Heading>
                                {naoLidas > 0 && (
                                    <Button
                                        onPress={() => lerTodos.mutate()}
                                        className="text-sm text-acento-texto underline-offset-4 hover:underline"
                                    >
                                        Marcar todos como lidos
                                    </Button>
                                )}
                            </div>
                            {sino.isPending ? (
                                <p className="px-4 py-6 text-sm text-suave">Carregando…</p>
                            ) : sino.isError ? (
                                <p className="px-4 py-6 text-sm text-vermelho">Não deu para carregar os avisos.</p>
                            ) : sino.data.itens.length === 0 ? (
                                <p className="px-4 py-6 text-sm text-suave">Nenhum aviso por aqui.</p>
                            ) : (
                                <ul className="max-h-96 overflow-y-auto py-1">
                                    {sino.data.itens.map((aviso) => (
                                        <li key={aviso.id}>
                                            <Button
                                                onPress={() => abrir(aviso, close)}
                                                className="flex w-full gap-3 px-4 py-2.5 text-left hover:bg-trilho"
                                            >
                                                <span
                                                    aria-hidden="true"
                                                    className={`mt-1.5 size-2 shrink-0 rounded-full ${aviso.lidaEm ? 'bg-transparent' : 'bg-acento'}`}
                                                />
                                                <span className="flex flex-col gap-0.5">
                                                    <span
                                                        className={`text-sm ${aviso.lidaEm ? 'text-suave' : 'font-semibold'}`}
                                                    >
                                                        {aviso.titulo}
                                                        {!aviso.lidaEm && <span className="sr-only"> (não lido)</span>}
                                                    </span>
                                                    <span className="text-xs text-suave">
                                                        {quando(aviso.criadaEm, fuso)}
                                                    </span>
                                                </span>
                                            </Button>
                                        </li>
                                    ))}
                                </ul>
                            )}
                        </div>
                    )}
                </Dialog>
            </Popover>
        </DialogTrigger>
    )
}
