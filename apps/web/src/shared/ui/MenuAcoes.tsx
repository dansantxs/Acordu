import type { ReactNode } from 'react'
import { Menu, MenuTrigger, Popover, type Key } from 'react-aria-components'
import { BotaoTexto } from './Botao'

/**
 * Menu "⋯" de ações de um item (quadro, plano da semana). `grava`: as ações gravam dado da empresa; com a empresa só
 * leitura o botão que abre o menu fica indisponível, ligado à explicação da casca, como o `<Botao grava>` (#191).
 */
export function MenuAcoes({
    rotulo,
    rotuloMenu,
    grava = false,
    aoEscolher,
    classeBotao = 'rounded-couro px-1 text-suave hover:bg-trilho hover:text-tinta',
    classeMenu = 'min-w-44 rounded-couro border border-linha bg-superficie py-1 shadow-lg',
    children,
}: {
    /** Nome acessível do botão "⋯" (ex.: "Mover Arte"). */
    rotulo: string
    /** Nome acessível do menu aberto (ex.: "Mover para"). */
    rotuloMenu: string
    grava?: boolean
    aoEscolher: (chave: Key) => void
    classeBotao?: string
    classeMenu?: string
    /** Os `MenuItem` (e seções) do menu. */
    children: ReactNode
}) {
    return (
        <MenuTrigger>
            <BotaoTexto grava={grava} aria-label={rotulo} className={classeBotao}>
                ⋯
            </BotaoTexto>
            <Popover className={classeMenu}>
                <Menu aria-label={rotuloMenu} onAction={aoEscolher} className="outline-none">
                    {children}
                </Menu>
            </Popover>
        </MenuTrigger>
    )
}
