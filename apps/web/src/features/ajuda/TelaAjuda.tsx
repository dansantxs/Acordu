import { Link } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { pode } from '@/features/casca/navegacao'
import { contem } from '@/shared/format/texto'
import type { Perfil } from '@/shared/sessao/perfil'
import { Pagina } from '@/shared/ui/Pagina'
import { PRIMEIROS_PASSOS, SECOES_AJUDA, type SecaoAjuda } from './conteudo'

const GRUPOS: { id: SecaoAjuda['grupo']; titulo: string }[] = [
    { id: 'comecar', titulo: 'Para começar' },
    { id: 'telas', titulo: 'As telas' },
    { id: 'conceitos', titulo: 'Conceitos' },
]

const textoDa = (s: SecaoAjuda) => [s.titulo, s.resumo, ...(s.passos ?? []), ...(s.dicas ?? [])].join(' ')

/**
 * Ajuda (marco do manual): o manual dentro do sistema, para todos da equipe. Mostra só as seções das telas que o papel
 * alcança (decisão 3), com os primeiros passos na ordem de uso, busca e o destaque da seção vinda do "?" de cada tela.
 */
export function TelaAjuda({ perfil, secao }: { perfil: Perfil; secao?: string }) {
    const [busca, setBusca] = useState('')
    const visiveis = SECOES_AJUDA.filter((s) => pode(perfil, ...s.permissoes))
    const achadas = busca.trim() ? visiveis.filter((s) => contem(textoDa(s), busca.trim())) : visiveis
    const passos = PRIMEIROS_PASSOS.filter((p) => pode(perfil, ...p.permissoes))
    const escondidas = SECOES_AJUDA.length - visiveis.length

    useEffect(() => {
        if (!secao) return
        const alvo = document.getElementById(`ajuda-${secao}`)
        alvo?.scrollIntoView?.({ block: 'start' })
        alvo?.focus({ preventScroll: true })
    }, [secao])

    return (
        <Pagina titulo="Ajuda" contexto="Manual do Acordu">
            <div className="flex max-w-3xl flex-col gap-8">
                <div className="flex flex-col gap-1">
                    <label htmlFor="ajuda-busca" className="text-sm font-semibold">
                        Buscar no manual
                    </label>
                    <input
                        id="ajuda-busca"
                        type="search"
                        value={busca}
                        onChange={(e) => setBusca(e.target.value)}
                        placeholder="Ex.: fechar o mês, SLA, portal"
                        className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta"
                    />
                </div>

                {!busca.trim() && passos.length > 0 && (
                    <section aria-labelledby="ajuda-passos" className="flex flex-col gap-3">
                        <h2 id="ajuda-passos" className="font-titulo text-2xl font-semibold tracking-tight">
                            Primeiros passos
                        </h2>
                        <p className="text-sm text-suave">A ordem que funciona melhor para começar a usar o Acordu.</p>
                        <ol className="flex flex-col gap-2">
                            {passos.map((p, i) => (
                                <li
                                    key={p.titulo}
                                    className="flex gap-3 rounded-couro border border-linha bg-superficie p-3 text-sm"
                                >
                                    <span className="grid size-6 shrink-0 place-items-center rounded-full bg-acento-suave text-xs font-semibold text-acento-texto">
                                        {i + 1}
                                    </span>
                                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                                        <span className="font-semibold">{p.titulo}</span>
                                        <span className="text-suave">{p.texto}</span>
                                    </span>
                                    <Link
                                        to={p.para}
                                        className="self-center text-xs whitespace-nowrap text-acento-texto hover:underline"
                                    >
                                        Abrir
                                    </Link>
                                </li>
                            ))}
                        </ol>
                    </section>
                )}

                {achadas.length === 0 ? (
                    <p className="text-sm text-suave">Nada no manual com "{busca}".</p>
                ) : (
                    GRUPOS.map((g) => {
                        const doGrupo = achadas.filter((s) => s.grupo === g.id)
                        if (doGrupo.length === 0) return null
                        return (
                            <div key={g.id} className="flex flex-col gap-4">
                                <h2 className="font-titulo text-2xl font-semibold tracking-tight">{g.titulo}</h2>
                                {doGrupo.map((s) => (
                                    <Secao key={s.id} secao={s} destacada={s.id === secao} />
                                ))}
                            </div>
                        )
                    })
                )}
                {escondidas > 0 && (
                    <p className="text-xs text-suave">
                        Algumas partes do manual não aparecem porque dependem de permissões que o seu papel não tem.
                    </p>
                )}
            </div>
        </Pagina>
    )
}

function Secao({ secao: s, destacada }: { secao: SecaoAjuda; destacada: boolean }) {
    return (
        <section
            id={`ajuda-${s.id}`}
            tabIndex={-1}
            aria-labelledby={`ajuda-titulo-${s.id}`}
            className={`flex scroll-mt-4 flex-col gap-3 rounded-couro border bg-superficie p-4 outline-none ${destacada ? 'border-acento ring-2 ring-acento/30' : 'border-linha'}`}
        >
            <div className="flex items-baseline justify-between gap-3">
                <h3 id={`ajuda-titulo-${s.id}`} className="text-lg font-semibold">
                    {s.titulo}
                </h3>
                {s.tela && (
                    <Link to={s.tela} className="text-sm whitespace-nowrap text-acento-texto hover:underline">
                        Abrir a tela
                    </Link>
                )}
            </div>
            <p className="text-sm">{s.resumo}</p>
            {s.passos && (
                <ol className="flex list-decimal flex-col gap-1 pl-5 text-sm">
                    {s.passos.map((p) => (
                        <li key={p}>{p}</li>
                    ))}
                </ol>
            )}
            {s.dicas && (
                <ul className="flex list-disc flex-col gap-1 pl-5 text-sm text-suave">
                    {s.dicas.map((d) => (
                        <li key={d}>{d}</li>
                    ))}
                </ul>
            )}
        </section>
    )
}
