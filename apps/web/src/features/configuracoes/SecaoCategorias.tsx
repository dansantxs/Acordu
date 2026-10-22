import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button, Form } from 'react-aria-components'
import { consultaCategorias } from '@/features/solicitacoes/consultas'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Janela } from '@/shared/ui/Janela'
import { criarCategoria, editarCategoria, inativarCategoria, type Categoria } from './consultas'
import { CabecalhoSecao } from './TelaConfiguracoes'

/**
 * Categorias de solicitação (F5b): criar, renomear, inativar e reativar. Inativar não pede confirmação: a categoria
 * some só das solicitações novas, e reativar desfaz.
 */
export function SecaoCategorias() {
    const consultas = useQueryClient()
    const lista = useQuery(consultaCategorias)
    const [renomeando, setRenomeando] = useState<Categoria | null>(null)
    const [verInativas, setVerInativas] = useState(false)
    const atualizar = () => consultas.invalidateQueries({ queryKey: consultaCategorias.queryKey })
    const criacao = useMutation({
        mutationFn: criarCategoria,
        onSuccess: async (c) => {
            await atualizar()
            avisar(`Categoria "${c.nome}" criada.`)
        },
    })
    const situacao = useMutation({
        mutationFn: async ({ c, ativa }: { c: Categoria; ativa: boolean }) => {
            if (ativa) await editarCategoria(c, { ativa: true })
            else await inativarCategoria(c)
        },
        onSuccess: async (_, { c, ativa }) => {
            await atualizar()
            avisar(ativa ? `"${c.nome}" voltou para as solicitações novas.` : `"${c.nome}" inativada.`, {
                desfazer: () => situacao.mutate({ c, ativa: !ativa }),
            })
        },
        onError: avisarErro,
    })
    const ativas = (lista.data ?? []).filter((c) => c.ativa)
    const inativas = (lista.data ?? []).filter((c) => !c.ativa)

    return (
        <>
            <CabecalhoSecao titulo="Categorias">
                Organizam as solicitações (suporte, ajuste, novo projeto…) e filtram a fila. Inativar tira a categoria
                só das solicitações novas.
            </CabecalhoSecao>
            <div className="flex max-w-2xl flex-col gap-4">
                <Form
                    key={criacao.data?.id ?? 'nova'}
                    className="flex flex-wrap items-start gap-2"
                    validationErrors={errosDeCampo(criacao.error)}
                    onSubmit={(evento) => {
                        evento.preventDefault()
                        const nome = String(new FormData(evento.currentTarget).get('nome') ?? '').trim()
                        if (nome) criacao.mutate(nome)
                    }}
                >
                    <CampoTexto name="nome" rotulo="Nova categoria" maxLength={100} className="min-w-56 flex-1" />
                    <Botao type="submit" variante="secundario" isPending={criacao.isPending} className="mt-6">
                        Adicionar
                    </Botao>
                </Form>
                {criacao.isError && mensagemDeErro(criacao.error) && <Alerta>{mensagemDeErro(criacao.error)}</Alerta>}

                {lista.isPending ? (
                    <p className="text-sm text-suave">Carregando…</p>
                ) : lista.isError ? (
                    <Alerta>{mensagemDeErro(lista.error)}</Alerta>
                ) : ativas.length === 0 ? (
                    <EstadoVazio titulo="Nenhuma categoria ativa">
                        Sem categoria, as solicitações entram sem classificação.
                    </EstadoVazio>
                ) : (
                    <ul className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                        {ativas.map((c) => (
                            <li key={c.id} className="flex items-center justify-between gap-3 px-4 py-2.5">
                                <span>{c.nome}</span>
                                <span className="flex gap-3">
                                    <Button
                                        onPress={() => setRenomeando(c)}
                                        aria-label={`Renomear ${c.nome}`}
                                        className="text-acento-texto hover:underline"
                                    >
                                        Renomear
                                    </Button>
                                    <Button
                                        onPress={() => situacao.mutate({ c, ativa: false })}
                                        aria-label={`Inativar ${c.nome}`}
                                        className="text-suave hover:underline"
                                    >
                                        Inativar
                                    </Button>
                                </span>
                            </li>
                        ))}
                    </ul>
                )}

                {inativas.length > 0 && (
                    <div className="flex flex-col gap-2">
                        <Button
                            onPress={() => setVerInativas((v) => !v)}
                            aria-expanded={verInativas}
                            className="w-fit text-sm text-suave hover:underline"
                        >
                            {verInativas ? 'Esconder' : 'Ver'} inativas ({inativas.length})
                        </Button>
                        {verInativas && (
                            <ul className="divide-y divide-linha rounded-couro border border-dashed border-linha text-sm text-suave">
                                {inativas.map((c) => (
                                    <li key={c.id} className="flex items-center justify-between gap-3 px-4 py-2">
                                        <span>{c.nome}</span>
                                        <Button
                                            onPress={() => situacao.mutate({ c, ativa: true })}
                                            aria-label={`Reativar ${c.nome}`}
                                            className="text-acento-texto hover:underline"
                                        >
                                            Reativar
                                        </Button>
                                    </li>
                                ))}
                            </ul>
                        )}
                    </div>
                )}
            </div>
            {renomeando && <JanelaRenomear categoria={renomeando} aoFechar={() => setRenomeando(null)} />}
        </>
    )
}

function JanelaRenomear({ categoria, aoFechar }: { categoria: Categoria; aoFechar: () => void }) {
    const consultas = useQueryClient()
    const envio = useMutation({
        mutationFn: (nome: string) => editarCategoria(categoria, { nome }),
        onSuccess: async (c) => {
            await consultas.invalidateQueries({ queryKey: consultaCategorias.queryKey })
            avisar(`Agora é "${c.nome}".`)
            aoFechar()
        },
    })
    return (
        <Janela
            titulo={`Renomear "${categoria.nome}"`}
            aberta
            aoMudar={(a) => !a && aoFechar()}
            ocupada={envio.isPending}
        >
            <Form
                className="flex flex-col gap-4"
                validationErrors={errosDeCampo(envio.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    envio.mutate(String(new FormData(evento.currentTarget).get('nome') ?? '').trim())
                }}
            >
                {envio.isError && mensagemDeErro(envio.error) && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
                <CampoTexto
                    name="nome"
                    rotulo="Nome"
                    defaultValue={categoria.nome}
                    maxLength={100}
                    isRequired
                    autoFocus
                />
                <p className="text-xs text-suave">
                    As solicitações que já usam a categoria passam a mostrar o nome novo.
                </p>
                <div className="flex justify-end gap-2">
                    <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao type="submit" isPending={envio.isPending}>
                        Salvar
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}
