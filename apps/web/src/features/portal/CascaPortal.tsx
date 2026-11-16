import { useQueryClient } from '@tanstack/react-query'
import { Link, useRouter } from '@tanstack/react-router'
import { useEffect, useMemo, type ReactNode } from 'react'
import { Button, Menu, MenuItem, MenuTrigger, Popover } from 'react-aria-components'
import { sair } from '@/shared/sessao/acoes'
import { encerrarSessaoLink } from '@/shared/sessao/sessaoLink'
import { SeletorTema } from '@/shared/tema/SeletorTema'
import { aplicarCouro } from '@/shared/tema/tema'
import { PontoNovidade } from '@/features/novidades/ListaNotas'
import { useHaNovidade } from '@/features/novidades/vistas'
import { ContextoGravacao, ID_MOTIVO_SEM_GRAVACAO, useAoRecusarGravacao } from '@/shared/sessao/gravacao'
import { consultaEu, type PortalEu } from './consultas'

/**
 * A mensagem neutra da recusa no portal (decisão 10 do G2; a mesma do `422 portal-indisponivel`, F14-21): não diz que a
 * empresa venceu nem foi cancelada.
 */
export const envioIndisponivel = (prestador: string) =>
    `No momento não é possível enviar por aqui; fale com ${prestador}.`

const estiloLink =
    'rounded-couro px-3 py-1.5 text-sm text-suave hover:bg-trilho data-[status=active]:bg-trilho data-[status=active]:font-semibold data-[status=active]:text-tinta'

/**
 * Moldura do portal (decisão 1 do F7): pensada primeiro para o celular. Barra no topo com o logotipo e o nome do
 * prestador, a navegação (Solicitações, Contratos se o contato vê, e Ajuda) e o menu do contato. Na sessão por link
 * (`link`), sem navegação: ela só alcança a solicitação do link.
 */
export function CascaPortal({ eu, link = false, children }: { eu: PortalEu; link?: boolean; children: ReactNode }) {
    // O portal fica sempre no Café, qualquer que seja o couro escolhido por alguém da equipe neste navegador.
    useEffect(() => aplicarCouro(null), [])
    const haNovidade = useHaNovidade('cliente')
    // Envio indisponível (empresa só leitura; F14-28): as ações de enviar ficam indisponíveis, com a mensagem neutra
    // da API (F14-21), que não diz por quê. Uma recusa com a tela aberta relê o `/portal/eu` (F14-27).
    const consultas = useQueryClient()
    const roteador = useRouter()
    useAoRecusarGravacao(() => {
        void consultas.refetchQueries({ queryKey: consultaEu.queryKey }).then(() => roteador.invalidate())
    })
    const envio = eu.envioDisponivel
    const gravacao = useMemo(
        () => ({ podeGravar: envio, idMotivo: envio ? undefined : ID_MOTIVO_SEM_GRAVACAO }),
        [envio],
    )
    return (
        <ContextoGravacao value={gravacao}>
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
                                <Link to="/portal/ajuda" className={`${estiloLink} ml-auto flex items-center gap-1.5`}>
                                    Ajuda
                                    {haNovidade && <PontoNovidade />}
                                </Link>
                            </nav>
                        )}
                    </div>
                </header>
                <main className="mx-auto flex max-w-3xl flex-col gap-6 px-4 py-6">
                    {!envio && (
                        <p
                            id={ID_MOTIVO_SEM_GRAVACAO}
                            role="status"
                            className="rounded-couro border border-ambar/40 bg-ambar-suave px-4 py-3 text-sm text-ambar-texto"
                        >
                            {envioIndisponivel(eu.prestador.nome)}
                        </p>
                    )}
                    {children}
                </main>
                <footer className="mx-auto max-w-3xl px-4 pb-8 text-xs text-suave">
                    {eu.cliente.nome} · atendimento de {eu.prestador.nome}, pelo Acordu
                </footer>
            </div>
        </ContextoGravacao>
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
