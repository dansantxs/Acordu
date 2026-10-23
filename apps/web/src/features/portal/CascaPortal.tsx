import { Link } from '@tanstack/react-router'
import { useEffect, type ReactNode } from 'react'
import { Button, Menu, MenuItem, MenuTrigger, Popover } from 'react-aria-components'
import { sair } from '@/shared/sessao/acoes'
import { encerrarSessaoLink } from '@/shared/sessao/sessaoLink'
import { SeletorTema } from '@/shared/tema/SeletorTema'
import type { PortalEu } from './consultas'

const estiloLink =
    'rounded-couro px-3 py-1.5 text-sm text-suave hover:bg-trilho data-[status=active]:bg-trilho data-[status=active]:font-semibold data-[status=active]:text-tinta'

/**
 * Moldura do portal (decisão 1 do F7): pensada primeiro para o celular. Barra no topo com o logotipo e o nome do
 * prestador, a navegação (Solicitações, Contratos se o contato vê, e Ajuda) e o menu do contato. Na sessão por link
 * (`link`), sem navegação: ela só alcança a solicitação do link.
 */
export function CascaPortal({ eu, link = false, children }: { eu: PortalEu; link?: boolean; children: ReactNode }) {
    return (
        <div className="min-h-screen bg-fundo text-tinta">
            <header className="border-b border-linha bg-superficie">
                <div className="mx-auto flex max-w-3xl flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3">
                    <span className="mr-auto flex min-w-0 items-center gap-3">
                        {eu.prestador.logoUrl ? (
                            <img
                                src={eu.prestador.logoUrl}
                                alt={eu.prestador.nome}
                                className="h-8 max-w-32 object-contain"
                            />
                        ) : (
                            <span className="truncate font-titulo text-xl font-semibold">{eu.prestador.nome}</span>
                        )}
                    </span>
                    <MenuTrigger>
                        <Button className="rounded-couro px-2 py-1 text-sm hover:bg-trilho">
                            {eu.contato.nome}
                            <span className="sr-only">, menu</span>
                        </Button>
                        <Popover className="min-w-48 rounded-couro border border-linha bg-superficie p-1 shadow-xl">
                            <Menu
                                className="outline-none"
                                // Pelo link não há conta: sair só esquece a sessão desta aba.
                                onAction={(c) => c === 'sair' && (link ? encerrarSessaoLink('saiu') : void sair())}
                            >
                                <MenuItem
                                    id="sair"
                                    className="cursor-default rounded-couro px-3 py-2 text-sm outline-none data-[focused]:bg-trilho"
                                >
                                    Sair
                                </MenuItem>
                            </Menu>
                        </Popover>
                    </MenuTrigger>
                    <SeletorTema />
                    {!link && (
                        <nav aria-label="Portal" className="flex w-full gap-1">
                            <Link to="/portal" activeOptions={{ exact: true }} className={estiloLink}>
                                Solicitações
                            </Link>
                            {eu.veContratos && (
                                <Link to="/portal/contratos" className={estiloLink}>
                                    Contratos
                                </Link>
                            )}
                            <Link to="/portal/ajuda" className={`${estiloLink} ml-auto`}>
                                Ajuda
                            </Link>
                        </nav>
                    )}
                </div>
            </header>
            <main className="mx-auto flex max-w-3xl flex-col gap-6 px-4 py-6">{children}</main>
            <footer className="mx-auto max-w-3xl px-4 pb-8 text-xs text-suave">
                {eu.cliente.nome} · atendimento de {eu.prestador.nome}, pelo Acordu
            </footer>
        </div>
    )
}

/** Título de tela do portal, que também nomeia a aba do navegador. */
export function TituloPortal({ titulo, contexto, acoes }: { titulo: string; contexto?: ReactNode; acoes?: ReactNode }) {
    useEffect(() => {
        document.title = `${titulo} · Portal`
    }, [titulo])
    return (
        <header className="flex flex-wrap items-end gap-3">
            <div className="mr-auto flex flex-col gap-0.5">
                {contexto && <span className="text-sm text-suave">{contexto}</span>}
                <h1 className="font-titulo text-3xl font-semibold tracking-tight">{titulo}</h1>
            </div>
            {acoes}
        </header>
    )
}
