import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { Button } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Icone } from '@/shared/ui/Icone'
import { Pagina } from '@/shared/ui/Pagina'
import { consultaCliente, inativarCliente, inativarContato, type Cliente, type Contato } from './consultas'
import { JanelaCliente } from './JanelaCliente'
import { JanelaContato } from './JanelaContato'

const ROTULO_ESCOPO = { PROPRIAS: 'só as próprias', TODAS_DO_CLIENTE: 'todas do cliente' } as const

function SituacaoPortal({ contato }: { contato: Contato }) {
    const acesso = contato.acessoPortal
    if (!acesso) return <span className="text-suave">sem acesso</span>
    const escopo = acesso.escopoPortal ? ROTULO_ESCOPO[acesso.escopoPortal] : ''
    return acesso.situacao === 'ATIVO' ? (
        <span className="text-verde">acessa o portal · {escopo}</span>
    ) : (
        <span className="text-ambar-texto">convidado · {escopo}</span>
    )
}

/**
 * Ficha básica (F2a): dados, observações e contatos. A ficha completa do protótipo — régua do banco de horas,
 * contratos, em aberto, acessos ao portal — chega no F4.
 */
export function FichaCliente({ perfil, id }: { perfil: Perfil; id: string }) {
    const ficha = useQuery(consultaCliente(id))
    const podeGerenciar = pode(perfil, 'cliente.gerenciar')

    if (ficha.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (ficha.isError) throw ficha.error
    return <Ficha cliente={ficha.data} podeGerenciar={podeGerenciar} />
}

function Ficha({ cliente, podeGerenciar }: { cliente: Cliente; podeGerenciar: boolean }) {
    const consultas = useQueryClient()
    const navegar = useNavigate()
    const [editando, setEditando] = useState(false)
    const [inativando, setInativando] = useState(false)
    const [contatoEmEdicao, setContatoEmEdicao] = useState<Contato | 'novo' | null>(null)
    const [contatoInativando, setContatoInativando] = useState<Contato | null>(null)

    const inativacao = useMutation({
        mutationFn: () => inativarCliente(cliente),
        onSuccess: async () => {
            await consultas.invalidateQueries({ queryKey: ['clientes'] })
            setInativando(false)
            avisar(`${cliente.nome} foi inativado.`)
            void navegar({ to: '/clientes' })
        },
    })
    const inativacaoContato = useMutation({
        mutationFn: (contato: Contato) => inativarContato(cliente, contato),
        onSuccess: async (_, contato) => {
            await consultas.invalidateQueries({ queryKey: ['clientes'] })
            setContatoInativando(null)
            avisar(`${contato.nome} foi inativado.`)
        },
    })

    const ativos = cliente.contatos.filter((c) => c.ativo)
    const editavel = podeGerenciar && cliente.ativo

    return (
        <Pagina
            titulo={cliente.nome}
            contexto={
                <Link to="/clientes" className="hover:underline">
                    Clientes
                </Link>
            }
            acoes={
                editavel && (
                    <div className="flex gap-2">
                        <Botao variante="secundario" onPress={() => setEditando(true)}>
                            Editar
                        </Botao>
                        <Botao variante="secundario" onPress={() => setInativando(true)}>
                            Inativar
                        </Botao>
                    </div>
                )
            }
        >
            {!cliente.ativo && (
                <Alerta tom="aviso">
                    Cliente inativo: não recebe solicitações novas e os contatos não acessam o portal.
                </Alerta>
            )}

            <section
                aria-labelledby="dados"
                className="grid gap-4 rounded-couro border border-linha bg-superficie p-5 sm:grid-cols-2"
            >
                <h2 id="dados" className="sr-only">
                    Dados
                </h2>
                <div>
                    <p className="text-xs font-semibold text-suave">CPF ou CNPJ</p>
                    <p>{cliente.documento ?? '—'}</p>
                </div>
                <div className="sm:col-span-2">
                    <p className="text-xs font-semibold text-suave">Observações</p>
                    <p className="whitespace-pre-line">{cliente.observacoes ?? '—'}</p>
                </div>
            </section>

            <section aria-labelledby="contatos" className="flex flex-col gap-3">
                <div className="flex items-center justify-between gap-3">
                    <h2 id="contatos" className="font-titulo text-xl font-semibold">
                        Contatos
                    </h2>
                    {editavel && (
                        <Botao variante="secundario" onPress={() => setContatoEmEdicao('novo')}>
                            <Icone nome="mais" />
                            Novo contato
                        </Botao>
                    )}
                </div>
                {ativos.length === 0 ? (
                    <EstadoVazio titulo="Nenhum contato">
                        Os contatos recebem os avisos das solicitações e podem ser convidados para o portal.
                    </EstadoVazio>
                ) : (
                    <ul className="divide-y divide-linha rounded-couro border border-linha bg-superficie">
                        {ativos.map((contato) => (
                            <li
                                key={contato.id}
                                className="flex flex-wrap items-center gap-x-6 gap-y-1 px-4 py-3 text-sm"
                            >
                                <div className="mr-auto flex min-w-48 flex-col">
                                    <span className="font-semibold">{contato.nome}</span>
                                    {contato.cargo && <span className="text-xs text-suave">{contato.cargo}</span>}
                                </div>
                                <span className="flex flex-col text-suave">
                                    <a href={`mailto:${contato.email}`} className="hover:underline">
                                        {contato.email}
                                    </a>
                                    {contato.telefone && <span>{contato.telefone}</span>}
                                </span>
                                <span className="text-xs">
                                    <SituacaoPortal contato={contato} />
                                </span>
                                {editavel && (
                                    <span className="flex gap-3 text-sm">
                                        <Button
                                            onPress={() => setContatoEmEdicao(contato)}
                                            aria-label={`Editar ${contato.nome}`}
                                            className="text-acento-texto hover:underline"
                                        >
                                            Editar
                                        </Button>
                                        <Button
                                            onPress={() => setContatoInativando(contato)}
                                            aria-label={`Inativar ${contato.nome}`}
                                            className="text-suave hover:underline"
                                        >
                                            Inativar
                                        </Button>
                                    </span>
                                )}
                            </li>
                        ))}
                    </ul>
                )}
            </section>

            <JanelaCliente cliente={cliente} aberta={editando} aoMudar={setEditando} />
            {contatoEmEdicao && (
                <JanelaContato
                    cliente={cliente}
                    contato={contatoEmEdicao === 'novo' ? undefined : contatoEmEdicao}
                    aberta
                    aoMudar={(aberta) => !aberta && setContatoEmEdicao(null)}
                />
            )}
            <Confirmacao
                titulo={`Inativar ${cliente.nome}?`}
                acao="Inativar cliente"
                aberta={inativando}
                aoMudar={(aberta) => {
                    if (!aberta) inativacao.reset()
                    setInativando(aberta)
                }}
                aoConfirmar={() => inativacao.mutate()}
                ocupada={inativacao.isPending}
                erro={inativacao.isError ? mensagemDeErro(inativacao.error) : null}
            >
                <p>O histórico continua guardado, mas o cliente deixa de receber solicitações novas.</p>
                <p>Todos os contatos perdem o acesso ao portal, e os links enviados param de funcionar.</p>
            </Confirmacao>
            <Confirmacao
                titulo={`Inativar ${contatoInativando?.nome ?? 'contato'}?`}
                acao="Inativar contato"
                aberta={contatoInativando !== null}
                aoMudar={(aberta) => {
                    if (!aberta) {
                        inativacaoContato.reset()
                        setContatoInativando(null)
                    }
                }}
                aoConfirmar={() => contatoInativando && inativacaoContato.mutate(contatoInativando)}
                ocupada={inativacaoContato.isPending}
                erro={inativacaoContato.isError ? mensagemDeErro(inativacaoContato.error) : null}
            >
                <p>O contato deixa de receber avisos e perde o acesso ao portal, se tiver.</p>
            </Confirmacao>
        </Pagina>
    )
}
