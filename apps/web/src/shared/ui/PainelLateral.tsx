import { useEffect, type ReactNode } from 'react'
import { Button } from 'react-aria-components'
import { Icone } from './Icone'

/**
 * Painel de detalhe sobre a lista (convenções §4): não modal, a lista segue clicável; Esc fecha, a não ser dentro de
 * lista aberta, campo de escolha, menu ou janela, que têm o Esc deles.
 */
export function PainelLateral({
    idTitulo,
    cabecalho,
    fechar,
    children,
}: {
    /** Id do título (h2) do conteúdo, que nomeia o painel. */
    idTitulo: string
    cabecalho: ReactNode
    fechar: () => void
    children: ReactNode
}) {
    useEffect(() => {
        function aoTeclar(evento: KeyboardEvent) {
            if (evento.key !== 'Escape' || evento.defaultPrevented) return
            const alvo = evento.target instanceof Element ? evento.target : null
            if (alvo?.closest('[role="combobox"], [role="listbox"], [role="dialog"], [role="menu"]')) return
            fechar()
        }
        document.addEventListener('keydown', aoTeclar)
        return () => document.removeEventListener('keydown', aoTeclar)
    }, [fechar])

    return (
        <aside
            aria-labelledby={idTitulo}
            className="fixed inset-y-0 right-0 z-40 flex w-full max-w-2xl flex-col border-l border-linha bg-fundo text-tinta shadow-2xl"
        >
            <div className="flex items-center gap-3 border-b border-linha bg-superficie px-5 py-3">
                {cabecalho}
                <Button
                    onPress={fechar}
                    aria-label="Fechar"
                    className="ml-auto grid size-8 place-items-center rounded-couro text-suave hover:bg-trilho hover:text-tinta"
                >
                    <Icone nome="fechar" />
                </Button>
            </div>
            <div className="flex-1 overflow-y-auto px-5 py-5">{children}</div>
        </aside>
    )
}

/** Campo só de leitura no painel, alinhado com os campos editáveis (mesmo rótulo e altura). */
export function Dado({ rotulo, children }: { rotulo: string; children: ReactNode }) {
    return (
        <div className="flex flex-col gap-1">
            <p className="text-sm font-semibold">{rotulo}</p>
            <div className="py-2 text-sm">{children}</div>
        </div>
    )
}
