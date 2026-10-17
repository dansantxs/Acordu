import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { Input, SearchField, Switch } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { formatarDocumento } from '@/shared/format/mascaras'
import { useAtrasado } from '@/shared/hooks/useAtrasado'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Icone } from '@/shared/ui/Icone'
import { Pagina } from '@/shared/ui/Pagina'
import { Paginacao } from '@/shared/ui/Paginacao'
import { consultaClientes, consultaEtiquetas, TAMANHO_PAGINA, type FiltrosClientes } from './consultas'
import { Etiquetas } from './Etiquetas'
import { JanelaCliente } from './JanelaCliente'

/** Lista de clientes com busca e "mostrar inativos" no endereço (`?busca=&inativos=&pagina=`). */
export function ListaClientes({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosClientes }) {
    const navegar = useNavigate()
    const [texto, setTexto] = useState(filtros.busca ?? '')
    const busca = useAtrasado(texto.trim())
    const lista = useQuery(consultaClientes(filtros))
    const etiquetas = useQuery(consultaEtiquetas)
    const equipe = useQuery(consultaEquipe)
    const filtrar = (mudanca: Partial<FiltrosClientes>) =>
        void navegar({ to: '/clientes', search: (atual) => ({ ...atual, ...mudanca, pagina: undefined }) })
    const podeGerenciar = pode(perfil, 'cliente.gerenciar')
    // A janela de novo cliente abre pelo endereço (`?novo=1`), para o Ctrl+K chegar com ela aberta.
    const novo = Boolean(filtros.novo) && podeGerenciar
    const setNovo = (aberta: boolean) =>
        void navegar({
            to: '/clientes',
            search: (atual) => ({ ...atual, novo: aberta || undefined }),
            replace: !aberta,
        })

    // A busca digitada vai para o endereço (sem empilhar no histórico) e volta para a primeira página.
    useEffect(() => {
        if (busca === (filtros.busca ?? '')) return
        void navegar({
            to: '/clientes',
            search: { ...filtros, busca: busca || undefined, pagina: undefined },
            replace: true,
        })
    }, [busca, filtros, navegar])

    const filtrando = Boolean(filtros.busca || filtros.etiqueta || filtros.responsavel)
    return (
        <Pagina
            titulo="Clientes"
            acoes={
                podeGerenciar && (
                    <Botao onPress={() => setNovo(true)}>
                        <Icone nome="mais" />
                        Novo cliente
                    </Botao>
                )
            }
        >
            <div className="flex flex-wrap items-center gap-4">
                <SearchField
                    aria-label="Buscar clientes"
                    value={texto}
                    onChange={setTexto}
                    className="flex min-w-64 flex-1 items-center gap-2 rounded-couro border border-linha bg-superficie px-3"
                >
                    <Icone nome="busca" className="size-4 text-suave" />
                    <Input
                        placeholder="Buscar por nome, razão social ou documento"
                        className="w-full bg-transparent py-2 outline-none placeholder:text-suave [&::-webkit-search-cancel-button]:hidden"
                    />
                </SearchField>
                <Switch
                    isSelected={Boolean(filtros.inativos)}
                    onChange={(ligado) =>
                        void navegar({
                            to: '/clientes',
                            search: { ...filtros, inativos: ligado || undefined, pagina: undefined },
                        })
                    }
                    className="group flex items-center gap-2 text-sm"
                >
                    <span className="h-5 w-9 rounded-full bg-trilho p-0.5 transition group-data-[selected]:bg-acento">
                        <span className="block size-4 rounded-full bg-superficie shadow transition group-data-[selected]:translate-x-4" />
                    </span>
                    Mostrar inativos
                </Switch>
                {(etiquetas.data?.length ?? 0) > 0 && (
                    <CampoSelecao
                        rotulo="Etiqueta"
                        rotuloOculto
                        opcoes={(etiquetas.data ?? []).map((e) => ({ id: e, rotulo: e }))}
                        valor={filtros.etiqueta ?? null}
                        aoMudar={(e) => filtrar({ etiqueta: e ?? undefined })}
                        vazio="Todas as etiquetas"
                        className="min-w-44"
                    />
                )}
                <CampoSelecao
                    rotulo="Responsável"
                    rotuloOculto
                    opcoes={(equipe.data ?? []).map((p) => ({ id: p.id, rotulo: p.nome }))}
                    valor={filtros.responsavel ?? null}
                    aoMudar={(r) => filtrar({ responsavel: r ?? undefined })}
                    vazio="Qualquer responsável"
                    className="min-w-48"
                />
            </div>

            {lista.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : lista.isError ? (
                <Alerta>{mensagemDeErro(lista.error)}</Alerta>
            ) : lista.data.itens.length === 0 ? (
                filtrando ? (
                    <EstadoVazio titulo="Nenhum cliente encontrado">
                        Confira a busca ou tente outra parte do nome.
                    </EstadoVazio>
                ) : (
                    <EstadoVazio
                        titulo="Nenhum cliente ainda"
                        acao={
                            podeGerenciar && <Botao onPress={() => setNovo(true)}>Cadastrar o primeiro cliente</Botao>
                        }
                    >
                        Cada solicitação, contrato e projeto pertence a um cliente. Comece por aqui.
                    </EstadoVazio>
                )
            ) : (
                <div className="overflow-hidden rounded-couro border border-linha bg-superficie">
                    <table className="w-full text-sm">
                        <thead className="border-b border-linha text-left text-xs text-suave">
                            <tr>
                                <th className="px-4 py-2.5 font-semibold">Nome</th>
                                <th className="hidden px-4 py-2.5 font-semibold sm:table-cell">Documento</th>
                                <th className="hidden px-4 py-2.5 font-semibold md:table-cell">Responsável</th>
                                <th className="hidden px-4 py-2.5 font-semibold lg:table-cell">Etiquetas</th>
                            </tr>
                        </thead>
                        <tbody>
                            {lista.data.itens.map((cliente) => (
                                <tr key={cliente.id} className="border-b border-linha last:border-0 hover:bg-trilho/50">
                                    <td className="px-4 py-2.5">
                                        <Link
                                            to="/clientes/$id"
                                            params={{ id: cliente.id }}
                                            className="font-semibold hover:underline"
                                        >
                                            {cliente.nome}
                                        </Link>
                                        {!cliente.ativo && (
                                            <span className="ml-2 rounded-couro bg-trilho px-1.5 py-0.5 text-xs text-suave">
                                                inativo
                                            </span>
                                        )}
                                        {cliente.razaoSocial && cliente.razaoSocial !== cliente.nome && (
                                            <span className="block text-xs text-suave">{cliente.razaoSocial}</span>
                                        )}
                                    </td>
                                    <td className="hidden px-4 py-2.5 text-suave tabular-nums sm:table-cell">
                                        {cliente.documento ? formatarDocumento(cliente.documento) : '—'}
                                    </td>
                                    <td className="hidden px-4 py-2.5 text-suave md:table-cell">
                                        {cliente.responsavel?.nome ?? '—'}
                                    </td>
                                    <td className="hidden px-4 py-2.5 lg:table-cell">
                                        <Etiquetas etiquetas={cliente.etiquetas} />
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {lista.data && (
                <Paginacao
                    pagina={filtros.pagina ?? 1}
                    tamanhoPagina={TAMANHO_PAGINA}
                    total={Number(lista.data.total)}
                />
            )}

            <JanelaCliente
                aberta={novo}
                aoMudar={setNovo}
                aoSalvar={(cliente) => void navegar({ to: '/clientes/$id', params: { id: cliente.id } })}
            />
        </Pagina>
    )
}
