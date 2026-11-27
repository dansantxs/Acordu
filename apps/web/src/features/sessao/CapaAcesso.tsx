import type { ReactNode } from 'react'
import { LinksLegais } from '@/features/legal/LinksLegais'
import { FORNECEDOR } from '@/shared/config/fornecedor'
import { SeletorTema } from '@/shared/tema/SeletorTema'

/**
 * Moldura das telas de acesso: uma agenda aberta, com a capa de couro à esquerda e a página à direita. Em tela
 * estreita a capa vira uma faixa no topo. No pé, os links dos textos legais (F14-84); `soPrivacidade` nas telas do
 * contato do portal, em que os termos não cabem.
 */
export function CapaAcesso({
    titulo,
    subtitulo,
    soPrivacidade = false,
    children,
}: {
    titulo: string
    subtitulo?: string
    soPrivacidade?: boolean
    children: ReactNode
}) {
    return (
        <div className="grid min-h-screen place-items-center bg-fundo p-4 sm:p-8">
            <div className="flex w-full max-w-4xl flex-col overflow-hidden rounded-[10px] shadow-xl shadow-black/15 md:min-h-[560px] md:flex-row">
                <aside className="relative flex flex-col justify-between gap-6 bg-lateral p-6 text-lateral-tinta outline-[1.5px] -outline-offset-8 outline-dashed outline-costura md:w-2/5 md:p-10">
                    <div>
                        <span className="font-titulo text-3xl font-semibold italic text-dourado md:text-4xl">
                            acordu
                        </span>
                        <p className="mt-3 max-w-xs text-sm text-lateral-suave">
                            Atendimento, tarefas e contratos num lugar só, com o cliente acompanhando de perto.
                        </p>
                    </div>
                    <p className="hidden text-xs text-lateral-suave md:block">{FORNECEDOR.nome}</p>
                </aside>
                <main className="flex flex-1 flex-col gap-6 bg-superficie p-6 md:p-10">
                    <div className="flex justify-end">
                        <SeletorTema />
                    </div>
                    <header>
                        <h1 className="font-titulo text-3xl font-semibold tracking-tight">{titulo}</h1>
                        {subtitulo && <p className="mt-1 text-sm text-suave">{subtitulo}</p>}
                    </header>
                    {children}
                    <LinksLegais soPrivacidade={soPrivacidade} className="mt-auto" />
                </main>
            </div>
        </div>
    )
}
