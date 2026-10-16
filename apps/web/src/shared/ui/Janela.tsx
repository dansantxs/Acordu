import type { ReactNode } from 'react'
import { Dialog, Heading, Modal, ModalOverlay } from 'react-aria-components'

/**
 * Janela sobre a tela (formulário curto, confirmação). Esc e clique fora fecham, a não ser durante um envio; o foco
 * fica preso dentro e volta para quem abriu.
 */
export function Janela({
    titulo,
    aberta,
    aoMudar,
    ocupada = false,
    children,
}: {
    titulo: string
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    /** Durante o envio não fecha por Esc nem clique fora. */
    ocupada?: boolean
    children: ReactNode
}) {
    return (
        <ModalOverlay
            isOpen={aberta}
            onOpenChange={aoMudar}
            isDismissable={!ocupada}
            isKeyboardDismissDisabled={ocupada}
            className="fixed inset-0 z-50 grid place-items-center overflow-y-auto bg-[var(--veu)] p-4"
        >
            <Modal className="w-full max-w-lg rounded-couro border border-linha bg-superficie text-tinta shadow-2xl">
                <Dialog className="flex flex-col gap-5 p-6 outline-none">
                    <Heading slot="title" className="font-titulo text-2xl font-semibold tracking-tight">
                        {titulo}
                    </Heading>
                    {children}
                </Dialog>
            </Modal>
        </ModalOverlay>
    )
}
