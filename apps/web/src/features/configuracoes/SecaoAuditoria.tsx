import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { Button } from 'react-aria-components'
import { AREAS_ACAO, diferencas, fraseAuditoria, valor, type Registro } from '@/features/auditoria/acoes'
import { consultaAuditoria, TAMANHO_PAGINA, type FiltrosAuditoria } from '@/features/auditoria/consultas'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { dataHora } from '@/shared/format/prazo'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Paginacao } from '@/shared/ui/Paginacao'
import { CabecalhoSecao } from './TelaConfiguracoes'

/**
 * Auditoria (F5d, contrato §11a): quem fez o quê, do mais recente, com filtros de período, ação e pessoa no endereço.
 * Cada linha vira frase (decisão 6 do F5); "Detalhes" abre o que mudou. A trilha guarda 12 meses.
 */
export function SecaoAuditoria({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosAuditoria }) {
    const navegar = useNavigate()
    const trilha = useQuery(consultaAuditoria(filtros))
    const equipe = useQuery(consultaEquipe)
    const nomes = new Map((equipe.data ?? []).map((p) => [p.id, p.nome]))
    const fuso = perfil.tenant.fusoHorario
    const filtrar = (mudanca: Partial<FiltrosAuditoria>) =>
        void navegar({ to: '.', search: (atual: FiltrosAuditoria) => ({ ...atual, ...mudanca, pagina: undefined }) })
    const comFiltro = Boolean(filtros.de || filtros.ate || filtros.acao || filtros.quem || filtros.alvo)

    return (
        <>
            <CabecalhoSecao titulo="Auditoria">
                Quem fez o quê: entradas, equipe, portal, contratos, horas e solicitações. Os registros ficam 12 meses.
            </CabecalhoSecao>
            <div className="mb-4 flex flex-wrap items-end gap-3">
                <CampoData id="aud-de" rotulo="De" valor={filtros.de} aoMudar={(de) => filtrar({ de })} />
                <CampoData id="aud-ate" rotulo="Até" valor={filtros.ate} aoMudar={(ate) => filtrar({ ate })} />
                <CampoSelecao
                    rotulo="Ação"
                    opcoes={AREAS_ACAO.flatMap((a) => a.acoes.map(([id, rotulo]) => ({ id, rotulo, detalhe: a.area })))}
                    valor={filtros.acao ?? null}
                    aoMudar={(v) => filtrar({ acao: v ?? undefined })}
                    vazio="Todas as ações"
                    className="sm:w-64"
                />
                <CampoSelecao
                    rotulo="Pessoa"
                    opcoes={(equipe.data ?? []).map((p) => ({ id: p.id, rotulo: p.nome }))}
                    valor={filtros.quem ?? null}
                    aoMudar={(v) => filtrar({ quem: v ?? undefined })}
                    vazio="Todas as pessoas"
                    className="sm:w-56"
                />
                {comFiltro && (
                    <Botao
                        variante="discreto"
                        onPress={() =>
                            filtrar({
                                de: undefined,
                                ate: undefined,
                                acao: undefined,
                                quem: undefined,
                                alvo: undefined,
                            })
                        }
                    >
                        Limpar filtros
                    </Botao>
                )}
            </div>
            {filtros.alvo && (
                <p className="mb-3 text-sm text-suave">
                    Só o histórico de um item.{' '}
                    <Button onPress={() => filtrar({ alvo: undefined })} className="text-acento-texto hover:underline">
                        Ver tudo
                    </Button>
                </p>
            )}

            {trilha.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : trilha.isError ? (
                <Alerta>{mensagemDeErro(trilha.error)}</Alerta>
            ) : trilha.data.itens.length === 0 ? (
                <EstadoVazio titulo={comFiltro ? 'Nada com esses filtros' : 'Nenhum registro ainda'} />
            ) : (
                <ol
                    aria-label="Registros"
                    className={`divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm ${trilha.isPlaceholderData ? 'opacity-60' : ''}`}
                >
                    {trilha.data.itens.map((r) => (
                        <Linha
                            key={r.id}
                            registro={r}
                            nomes={nomes}
                            fuso={fuso}
                            aoFiltrarAlvo={(alvo) => filtrar({ alvo })}
                        />
                    ))}
                </ol>
            )}
            {trilha.data && (
                <div className="mt-4">
                    <Paginacao
                        pagina={filtros.pagina ?? 1}
                        tamanhoPagina={TAMANHO_PAGINA}
                        total={Number(trilha.data.total)}
                    />
                </div>
            )}
        </>
    )
}

function LinkAlvo({ r }: { r: Registro }) {
    const d = (r.dados ?? {}) as Record<string, unknown>
    if (!r.alvoId) return null
    if (r.alvoTipo === 'solicitacao')
        return (
            <Link to="/solicitacoes/$id" params={{ id: r.alvoId }} className="text-acento-texto hover:underline">
                Abrir a solicitação
            </Link>
        )
    if (r.alvoTipo === 'contrato')
        return (
            <Link to="/contratos/$id" params={{ id: r.alvoId }} className="text-acento-texto hover:underline">
                Abrir o contrato
            </Link>
        )
    if (r.alvoTipo === 'contato' && typeof d.clienteId === 'string')
        return (
            <Link
                to="/clientes/$id"
                params={{ id: d.clienteId }}
                search={{ secao: 'contatos' }}
                className="text-acento-texto hover:underline"
            >
                Abrir o cliente
            </Link>
        )
    return null
}

function Linha({
    registro: r,
    nomes,
    fuso,
    aoFiltrarAlvo,
}: {
    registro: Registro
    nomes: ReadonlyMap<string, string>
    fuso: string
    aoFiltrarAlvo: (alvo: string) => void
}) {
    const [aberta, setAberta] = useState(false)
    const mudancas = diferencas(r.dados)
    const outros =
        r.dados && typeof r.dados === 'object' && mudancas.length === 0
            ? Object.entries(r.dados as Record<string, unknown>)
            : []
    const temDetalhes = mudancas.length > 0 || outros.length > 0 || Boolean(r.ip)
    return (
        <li className="flex flex-col gap-1 px-4 py-2.5">
            <div className="flex flex-wrap items-baseline gap-x-2">
                <span className="font-semibold">{r.usuario ? r.usuario.nome || 'Pessoa removida' : 'Sistema'}</span>
                <span>{fraseAuditoria(r, nomes)}</span>
            </div>
            <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-suave">
                <time dateTime={r.ocorridoEm}>{dataHora(r.ocorridoEm, fuso)}</time>
                <LinkAlvo r={r} />
                {r.alvoId && (
                    <Button onPress={() => aoFiltrarAlvo(r.alvoId!)} className="hover:underline">
                        Histórico deste item
                    </Button>
                )}
                {temDetalhes && (
                    <Button onPress={() => setAberta((v) => !v)} aria-expanded={aberta} className="hover:underline">
                        {aberta ? 'Esconder detalhes' : 'Detalhes'}
                    </Button>
                )}
            </div>
            {aberta && (
                <dl className="mt-1 grid grid-cols-[auto_1fr] gap-x-4 gap-y-0.5 rounded-couro bg-trilho/50 px-3 py-2 text-xs">
                    {mudancas.map((m) => (
                        <div key={m.campo} className="contents">
                            <dt className="text-suave">{m.campo}</dt>
                            <dd className="break-all">
                                {m.antes} <span className="text-suave">→</span> {m.depois}
                            </dd>
                        </div>
                    ))}
                    {outros.map(([chave, v]) => (
                        <div key={chave} className="contents">
                            <dt className="text-suave">{chave}</dt>
                            <dd className="break-all">{valor(v)}</dd>
                        </div>
                    ))}
                    {r.ip && (
                        <div className="contents">
                            <dt className="text-suave">IP</dt>
                            <dd>{r.ip}</dd>
                        </div>
                    )}
                </dl>
            )}
        </li>
    )
}

function CampoData({
    id,
    rotulo,
    valor: v,
    aoMudar,
}: {
    id: string
    rotulo: string
    valor?: string
    aoMudar: (v: string | undefined) => void
}) {
    return (
        <div className="flex flex-col gap-1">
            <label htmlFor={id} className="text-sm font-semibold">
                {rotulo}
            </label>
            <input
                id={id}
                type="date"
                value={v ?? ''}
                onChange={(e) => aoMudar(e.target.value || undefined)}
                className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta"
            />
        </div>
    )
}
