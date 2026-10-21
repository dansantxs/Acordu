import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useState, type ReactNode } from 'react'
import { Button, Tab, TabList, TabPanel, Tabs } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { formatarCep, formatarDocumento, formatarTelefone } from '@/shared/format/mascaras'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Icone } from '@/shared/ui/Icone'
import { Pagina } from '@/shared/ui/Pagina'
import {
    consultaCliente,
    inativarCliente,
    inativarContato,
    reativarCliente,
    reativarContato,
    type Cliente,
    type Contato,
    type Endereco,
} from './consultas'
import { ABAS_CLIENTE, ROTULO_ABA, type AbaCliente } from './abas'
import { ContratosCliente, EmAbertoCliente, ResumoCliente } from './SecoesFicha'
import { Etiquetas } from './Etiquetas'
import { JanelaCliente } from './JanelaCliente'
import { JanelaContato } from './JanelaContato'

function Dado({ rotulo, largo = false, children }: { rotulo: string; largo?: boolean; children: ReactNode }) {
    return (
        <div className={largo ? 'sm:col-span-2' : ''}>
            <p className="text-xs font-semibold text-suave">{rotulo}</p>
            <div>{children || <span className="text-suave">—</span>}</div>
        </div>
    )
}

function EnderecoPostal({ endereco: e }: { endereco: Endereco }) {
    const primeira = [e.logradouro, e.numero, e.complemento].filter(Boolean).join(', ')
    const segunda = [e.bairro, `${e.cidade}/${e.uf}`, formatarCep(e.cep)].filter(Boolean).join(' · ')
    return (
        <address className="not-italic">
            {primeira}
            <br />
            {segunda}
        </address>
    )
}

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
 * Ficha do cliente em abas com endereço (decisão 1 do F4): resumo com dados e a régua dos contratos ativos, o que
 * está em aberto, contratos e contatos (arquivos, movimentos e o portal dos contatos chegam no F4b2).
 */
export function FichaCliente({
    perfil,
    id,
    aba = 'resumo',
    aoMudarAba,
}: {
    perfil: Perfil
    id: string
    aba?: AbaCliente
    aoMudarAba: (aba: AbaCliente) => void
}) {
    const ficha = useQuery(consultaCliente(id))
    const podeGerenciar = pode(perfil, 'cliente.gerenciar')

    if (ficha.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (ficha.isError) throw ficha.error
    return (
        <Ficha cliente={ficha.data} podeGerenciar={podeGerenciar} perfil={perfil} aba={aba} aoMudarAba={aoMudarAba} />
    )
}

function Ficha({
    cliente,
    podeGerenciar,
    perfil,
    aba,
    aoMudarAba,
}: {
    cliente: Cliente
    podeGerenciar: boolean
    perfil: Perfil
    aba: AbaCliente
    aoMudarAba: (aba: AbaCliente) => void
}) {
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

    // Reativar não pede confirmação: dá para inativar de novo (convenções §4).
    const reativacao = useMutation({
        mutationFn: () => reativarCliente(cliente),
        onSuccess: async () => {
            await consultas.invalidateQueries({ queryKey: ['clientes'] })
            avisar(`${cliente.nome} foi reativado. Para os contatos voltarem ao portal, convide-os de novo.`)
        },
        onError: avisarErro,
    })
    const reativacaoContato = useMutation({
        mutationFn: (contato: Contato) => reativarContato(cliente, contato),
        onSuccess: async (_, contato) => {
            await consultas.invalidateQueries({ queryKey: ['clientes'] })
            avisar(`${contato.nome} foi reativado.`)
        },
        onError: avisarErro,
    })
    const [verInativos, setVerInativos] = useState(false)

    const ativos = cliente.contatos.filter((c) => c.ativo)
    const inativos = cliente.contatos.filter((c) => !c.ativo)
    const editavel = podeGerenciar && cliente.ativo
    const abasVisiveis = ABAS_CLIENTE.filter((a) => a !== 'contratos' || pode(perfil, 'contrato.ver'))

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
                    <span className="flex flex-wrap items-center justify-between gap-3">
                        <span>Cliente inativo: não recebe solicitações novas e os contatos não acessam o portal.</span>
                        {podeGerenciar && (
                            <Botao
                                variante="secundario"
                                isPending={reativacao.isPending}
                                onPress={() => reativacao.mutate()}
                            >
                                Reativar cliente
                            </Botao>
                        )}
                    </span>
                </Alerta>
            )}

            <Tabs selectedKey={aba} onSelectionChange={(chave) => aoMudarAba(chave as AbaCliente)}>
                <TabList
                    aria-label="Seções do cliente"
                    className="flex w-fit flex-wrap gap-1 rounded-couro bg-trilho p-1"
                >
                    {abasVisiveis.map((a) => (
                        <Tab
                            key={a}
                            id={a}
                            className="cursor-default rounded-couro px-3 py-1.5 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {ROTULO_ABA[a]}
                        </Tab>
                    ))}
                </TabList>
                <TabPanel id="resumo" className="flex flex-col gap-6 pt-6 outline-none">
                    <section
                        aria-labelledby="dados"
                        className="grid gap-4 rounded-couro border border-linha bg-superficie p-5 sm:grid-cols-2"
                    >
                        <h2 id="dados" className="sr-only">
                            Dados
                        </h2>
                        <Dado rotulo="Razão social">{cliente.razaoSocial}</Dado>
                        <Dado rotulo="CPF ou CNPJ">{cliente.documento && formatarDocumento(cliente.documento)}</Dado>
                        <Dado rotulo="Inscrição estadual">{cliente.inscricaoEstadual}</Dado>
                        <Dado rotulo="Responsável na equipe">
                            {cliente.responsavel?.nome || (cliente.responsavel && 'Pessoa que saiu da equipe')}
                        </Dado>
                        <Dado rotulo="E-mail">
                            {cliente.email && (
                                <a href={`mailto:${cliente.email}`} className="hover:underline">
                                    {cliente.email}
                                </a>
                            )}
                        </Dado>
                        <Dado rotulo="Telefone">{cliente.telefone && formatarTelefone(cliente.telefone)}</Dado>
                        <Dado rotulo="Endereço" largo>
                            {cliente.endereco && <EnderecoPostal endereco={cliente.endereco} />}
                        </Dado>
                        <Dado rotulo="Etiquetas" largo>
                            {cliente.etiquetas.length > 0 && <Etiquetas etiquetas={cliente.etiquetas} />}
                        </Dado>
                        <Dado rotulo="Observações" largo>
                            {cliente.observacoes && <span className="whitespace-pre-line">{cliente.observacoes}</span>}
                        </Dado>
                    </section>
                    <ResumoCliente cliente={cliente} perfil={perfil} />
                </TabPanel>
                <TabPanel id="aberto" className="pt-6 outline-none">
                    <EmAbertoCliente cliente={cliente} perfil={perfil} />
                </TabPanel>
                <TabPanel id="contratos" className="pt-6 outline-none">
                    <ContratosCliente cliente={cliente} perfil={perfil} />
                </TabPanel>
                <TabPanel id="contatos" className="pt-6 outline-none">
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
                                            {contato.cargo && (
                                                <span className="text-xs text-suave">{contato.cargo}</span>
                                            )}
                                        </div>
                                        <span className="flex flex-col text-suave">
                                            <a href={`mailto:${contato.email}`} className="hover:underline">
                                                {contato.email}
                                            </a>
                                            {contato.telefone && <span>{formatarTelefone(contato.telefone)}</span>}
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
                        {inativos.length > 0 && (
                            <div className="flex flex-col gap-2">
                                <Button
                                    onPress={() => setVerInativos((v) => !v)}
                                    aria-expanded={verInativos}
                                    className="w-fit text-sm text-suave hover:underline"
                                >
                                    {verInativos ? 'Esconder' : 'Ver'} contatos inativos ({inativos.length})
                                </Button>
                                {verInativos && (
                                    <ul className="divide-y divide-linha rounded-couro border border-dashed border-linha">
                                        {inativos.map((contato) => (
                                            <li
                                                key={contato.id}
                                                className="flex flex-wrap items-center gap-x-6 gap-y-1 px-4 py-2.5 text-sm text-suave"
                                            >
                                                <span className="mr-auto">
                                                    {contato.nome} · {contato.email}
                                                </span>
                                                {editavel && (
                                                    <Button
                                                        onPress={() => reativacaoContato.mutate(contato)}
                                                        isDisabled={reativacaoContato.isPending}
                                                        aria-label={`Reativar ${contato.nome}`}
                                                        className="text-acento-texto hover:underline"
                                                    >
                                                        Reativar
                                                    </Button>
                                                )}
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </div>
                        )}
                    </section>
                </TabPanel>
            </Tabs>

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
