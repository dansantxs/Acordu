import { Link } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import type { Perfil } from '@/shared/sessao/perfil'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Pagina } from '@/shared/ui/Pagina'
import { podeVer, secoesDe, type CaminhoSecao } from './secoes'

const estiloLink =
    'block rounded-couro px-3 py-1.5 text-sm whitespace-nowrap text-suave hover:bg-trilho data-[status=active]:bg-trilho data-[status=active]:font-semibold data-[status=active]:text-tinta'

/** Configurações (F5): menu de seções à esquerda (no celular, em cima, rolando de lado) e a seção aberta ao lado. */
export function TelaConfiguracoes({ perfil, children }: { perfil: Perfil; children: ReactNode }) {
    const secoes = secoesDe(perfil)
    return (
        <Pagina titulo="Configurações">
            <div className="flex flex-col gap-6 md:flex-row md:items-start">
                <nav aria-label="Seções" className="-mx-4 overflow-x-auto px-4 md:mx-0 md:w-48 md:shrink-0 md:px-0">
                    <ul className="flex gap-1 md:flex-col">
                        {secoes.map((s) => (
                            <li key={s.para}>
                                <Link to={s.para} className={estiloLink}>
                                    {s.rotulo}
                                </Link>
                            </li>
                        ))}
                    </ul>
                </nav>
                <div className="min-w-0 flex-1">{children}</div>
            </div>
        </Pagina>
    )
}

/** Cabeçalho de seção (h2) com a explicação curta embaixo. */
export function CabecalhoSecao({ titulo, children }: { titulo: string; children?: ReactNode }) {
    return (
        <header className="mb-5 flex flex-col gap-1">
            <h2 className="font-titulo text-2xl font-semibold tracking-tight">{titulo}</h2>
            {children && <p className="max-w-prose text-sm text-suave">{children}</p>}
        </header>
    )
}

/** A seção só abre com a permissão dela; pelo endereço direto, sem ela, explica em vez de mostrar erro da API. */
export function SecaoProtegida({
    perfil,
    para,
    children,
}: {
    perfil: Perfil
    para: CaminhoSecao
    children: ReactNode
}) {
    if (!podeVer(perfil, para))
        return (
            <EstadoVazio titulo="Sem acesso a esta seção">
                Seu papel não inclui esta parte das configurações.
            </EstadoVazio>
        )
    return children
}
