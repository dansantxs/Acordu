import { useMutation } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { Button, Header, Menu, MenuItem, MenuSection, MenuTrigger, Popover, Separator } from 'react-aria-components'
import { avisarErro } from '@/shared/avisos/avisos'
import { sair, trocarEmpresa } from '@/shared/sessao/acoes'
import type { Perfil } from '@/shared/sessao/perfil'
import { COUROS, couroAtual, escolherCouro, escolherTema, temaAtual, type Couro, type Tema } from '@/shared/tema/tema'

const item =
    'flex cursor-default items-center justify-between gap-3 rounded-couro px-3 py-2 text-sm outline-none data-[focused]:bg-trilho data-[disabled]:text-suave'

/** Pé da capa: quem está usando e em qual empresa; troca de empresa, Minha conta, tema, couro e saída. */
export function MenuUsuario({ perfil }: { perfil: Perfil }) {
    const navegar = useNavigate()
    const [tema, setTema] = useState<Tema>(temaAtual)
    const [couro, setCouro] = useState<Couro>(couroAtual)
    const troca = useMutation({ mutationFn: trocarEmpresa, onError: avisarErro })
    const outras = perfil.tenants.filter((t) => t.id !== perfil.tenant.id)

    return (
        <MenuTrigger>
            <Button className="flex w-full flex-col items-start gap-0.5 rounded-couro px-2 py-2 text-left hover:bg-lateral-ativo">
                <span className="text-sm font-semibold">{perfil.nome}</span>
                <span className="text-xs text-lateral-suave">
                    {perfil.papel} · {perfil.tenant.nome}
                </span>
            </Button>
            <Popover
                placement="top start"
                className="w-64 rounded-couro border border-linha bg-superficie p-1 text-tinta shadow-xl"
            >
                <Menu
                    className="outline-none"
                    onAction={(chave) => {
                        if (chave === 'conta') void navegar({ to: '/conta' })
                        else if (chave === 'tema') setTema(escolherTema(tema === 'escuro' ? 'claro' : 'escuro'))
                        else if (chave === 'sair') void sair()
                        else if (typeof chave === 'string' && chave.startsWith('couro:'))
                            setCouro(escolherCouro(chave.slice(6) as Couro))
                        else if (typeof chave === 'string' && chave.startsWith('empresa:')) troca.mutate(chave.slice(8))
                    }}
                >
                    {outras.length > 0 && (
                        <>
                            <MenuSection>
                                <Header className="px-3 pt-2 pb-1 text-xs font-semibold text-suave">
                                    Trocar de empresa
                                </Header>
                                {outras.map((t) => (
                                    <MenuItem key={t.id} id={`empresa:${t.id}`} textValue={t.nome} className={item}>
                                        <span>{t.nome}</span>
                                        <span className="text-xs text-suave">{t.papel}</span>
                                    </MenuItem>
                                ))}
                            </MenuSection>
                            <Separator className="my-1 border-t border-linha" />
                        </>
                    )}
                    <MenuItem id="conta" className={item}>
                        Minha conta
                    </MenuItem>
                    <MenuItem id="tema" className={item}>
                        {tema === 'escuro' ? 'Tema claro' : 'Tema escuro'}
                    </MenuItem>
                    <Separator className="my-1 border-t border-linha" />
                    <MenuSection>
                        <Header className="px-3 pt-2 pb-1 text-xs font-semibold text-suave">Couro</Header>
                        {COUROS.map((c) => (
                            <MenuItem key={c.id} id={`couro:${c.id}`} textValue={`Couro ${c.nome}`} className={item}>
                                <span>{c.nome}</span>
                                {c.id === couro && (
                                    <span aria-label="escolhido" className="text-xs text-acento-texto">
                                        ✓
                                    </span>
                                )}
                            </MenuItem>
                        ))}
                    </MenuSection>
                    <Separator className="my-1 border-t border-linha" />
                    <MenuItem id="sair" className={item}>
                        Sair
                    </MenuItem>
                </Menu>
            </Popover>
        </MenuTrigger>
    )
}
