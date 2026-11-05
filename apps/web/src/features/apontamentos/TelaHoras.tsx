import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { pode } from '@/features/casca/navegacao'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { dataCurta, diaPorExtenso, hojeNoFuso, segundaDaSemana, somarDias } from '@/shared/format/datas'
import { formatarDuracao } from '@/shared/format/duracao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Icone } from '@/shared/ui/Icone'
import { Pagina } from '@/shared/ui/Pagina'
import { JanelaApontamento, LinhasApontamento } from './Apontamentos'
import { consultaApontamentos, type Apontamento } from './consultas'

/** Filtros no endereço (`/horas?semana=2026-10-19&pessoa=equipe&cliente=`); padrão: a semana atual e eu. */
export interface FiltrosHoras {
    /** Segunda-feira da semana. */
    semana?: string
    /** Id da pessoa ou `equipe` (todas); vazio = eu. */
    pessoa?: string
    cliente?: string
}

const estiloSemana =
    'grid size-9 place-items-center rounded-couro border border-linha bg-superficie text-suave hover:bg-trilho hover:text-tinta'

/**
 * Horas (plano do F3, decisão 7): a semana agrupada por dia, com o total do dia e da semana; editar e excluir pelo
 * autor (ou `apontamento.editar_todos`); filtros de pessoa e cliente para ver a equipe. Lançar pede a tarefa.
 */
export function TelaHoras({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosHoras }) {
    const navegar = useNavigate()
    const fuso = perfil.tenant.fusoHorario
    const hoje = hojeNoFuso(fuso)
    const semanaAtual = segundaDaSemana(hoje)
    const semana = filtros.semana ?? semanaAtual
    const ate = somarDias(semana, 6)
    const usuarioId =
        filtros.pessoa === 'equipe'
            ? undefined
            : filtros.pessoa && filtros.pessoa !== 'eu'
              ? filtros.pessoa
              : perfil.usuarioId
    const podeVer = pode(perfil, 'apontamento.ver')
    const podeCriar = pode(perfil, 'apontamento.criar')
    const apontamentos = useQuery({
        ...consultaApontamentos({ usuarioId, clienteId: filtros.cliente, de: semana, ate }),
        enabled: podeVer,
    })
    const equipe = useQuery(consultaEquipe)
    const [lancando, setLancando] = useState(false)

    const filtrar = (mudanca: Partial<FiltrosHoras>) =>
        void navegar({ to: '/horas', search: (atual: FiltrosHoras) => ({ ...atual, ...mudanca }) })
    const irPara = (segunda: string) => filtrar({ semana: segunda === semanaAtual ? undefined : segunda })

    const itens = apontamentos.data?.itens ?? []
    const porDia = new Map<string, Apontamento[]>()
    for (const a of itens) porDia.set(a.data, [...(porDia.get(a.data) ?? []), a])
    const total = itens.reduce((soma, a) => soma + Number(a.duracaoMinutos), 0)
    const faturavel = itens.filter((a) => a.faturavel).reduce((soma, a) => soma + Number(a.duracaoMinutos), 0)
    const vendoOutros = usuarioId !== perfil.usuarioId

    return (
        <Pagina
            ajuda="horas"
            titulo="Horas trabalhadas na semana"
            acoes={
                podeCriar && (
                    <Botao onPress={() => setLancando(true)}>
                        <Icone nome="mais" />
                        Lançar horas
                    </Botao>
                )
            }
        >
            <div className="flex flex-wrap items-end gap-3">
                <div className="flex items-center gap-2">
                    <Link
                        to="/horas"
                        search={(atual: FiltrosHoras) => ({ ...atual, semana: somarDias(semana, -7) })}
                        aria-label="Semana anterior"
                        className={estiloSemana}
                    >
                        <Icone nome="seta" className="size-4 rotate-180" />
                    </Link>
                    <h2 className="min-w-44 text-center text-sm font-semibold" aria-live="polite">
                        {dataCurta(semana, hoje)} a {dataCurta(ate, hoje)}
                    </h2>
                    <Link
                        to="/horas"
                        search={(atual: FiltrosHoras) => {
                            const proxima = somarDias(semana, 7)
                            return { ...atual, semana: proxima === semanaAtual ? undefined : proxima }
                        }}
                        aria-label="Próxima semana"
                        className={estiloSemana}
                    >
                        <Icone nome="seta" className="size-4" />
                    </Link>
                    {semana !== semanaAtual && (
                        <Botao variante="discreto" onPress={() => irPara(semanaAtual)}>
                            Esta semana
                        </Botao>
                    )}
                </div>
                <CampoSelecao
                    rotulo="Pessoa"
                    rotuloOculto
                    opcoes={[
                        { id: 'equipe', rotulo: 'Toda a equipe' },
                        ...(equipe.data ?? [])
                            .filter((p) => p.id !== perfil.usuarioId)
                            .map((p) => ({ id: p.id, rotulo: p.nome })),
                    ]}
                    valor={filtros.pessoa ?? null}
                    aoMudar={(v) => filtrar({ pessoa: v ?? undefined })}
                    vazio="As minhas"
                    dica="Pessoa"
                    className="sm:w-48"
                />
                {pode(perfil, 'cliente.ver') && (
                    <CampoCliente
                        rotulo="Cliente"
                        rotuloOculto
                        valor={filtros.cliente ?? null}
                        aoMudar={(v) => filtrar({ cliente: v ?? undefined })}
                        vazio="Todos os clientes"
                        dica="Cliente"
                        className="sm:w-52"
                    />
                )}
            </div>

            {!podeVer ? (
                <EstadoVazio titulo="Sem acesso às horas">Seu papel não permite ver apontamentos.</EstadoVazio>
            ) : apontamentos.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : apontamentos.isError ? (
                <Alerta>{mensagemDeErro(apontamentos.error)}</Alerta>
            ) : itens.length === 0 ? (
                <EstadoVazio titulo="Nenhuma hora nesta semana">
                    As horas do cronômetro e as lançadas à mão aparecem aqui, por dia.
                </EstadoVazio>
            ) : (
                <>
                    <p className="text-sm">
                        <span className="font-semibold">{formatarDuracao(total)}</span> na semana
                        <span className="text-suave"> · {formatarDuracao(faturavel)} faturáveis</span>
                    </p>
                    {[...porDia.entries()].map(([dia, doDia]) => (
                        <section key={dia} aria-labelledby={`dia-${dia}`} className="flex flex-col gap-2">
                            <h3 id={`dia-${dia}`} className="flex items-baseline justify-between text-sm font-semibold">
                                <span className="first-letter:uppercase">
                                    {diaPorExtenso(dia)}
                                    {dia === hoje && <span className="font-normal text-suave"> · hoje</span>}
                                </span>
                                <span className="tabular-nums">
                                    {formatarDuracao(doDia.reduce((s, a) => s + Number(a.duracaoMinutos), 0))}
                                </span>
                            </h3>
                            <LinhasApontamento
                                itens={doDia}
                                fuso={fuso}
                                usuarioId={perfil.usuarioId}
                                podeEditarTodos={pode(perfil, 'apontamento.editar_todos')}
                                podeCriar={podeCriar}
                                mostrarTarefa
                                mostrarPessoa={vendoOutros}
                            />
                        </section>
                    ))}
                </>
            )}

            <JanelaApontamento fuso={fuso} aberta={lancando} aoMudar={setLancando} />
        </Pagina>
    )
}
