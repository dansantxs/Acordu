import { Outlet } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { Button, Dialog, Modal, ModalOverlay } from 'react-aria-components'
import type { Perfil } from '@/shared/sessao/perfil'
import { aplicarCouro, couroAtual } from '@/shared/tema/tema'
import { useTempoReal } from '@/shared/tempoReal/useTempoReal'
import { FaixaConexao } from '@/shared/ui/FaixaConexao'
import { Icone } from '@/shared/ui/Icone'
import { CapaLateral } from './CapaLateral'
import { PaletaComandos, useAtalhoPaleta } from './PaletaComandos'

/**
 * Moldura do app da equipe: capa lateral fixa a partir de `md`; abaixo disso, faixa de couro no topo com o botão que
 * abre a capa como gaveta. A paleta (Ctrl+K) vale em qualquer tela, e o tempo real (ADR 0009) fica ligado enquanto a
 * casca está montada.
 */
export function Casca({ perfil }: { perfil: Perfil }) {
    const [paletaAberta, setPaletaAberta] = useAtalhoPaleta()
    const [gaveta, setGaveta] = useState(false)
    useTempoReal(perfil.tenant.id)
    // O couro escolhido vale no app da equipe (o portal tira; ver CascaPortal).
    useEffect(() => aplicarCouro(couroAtual()), [])
    const abrirBusca = () => {
        setGaveta(false)
        setPaletaAberta(true)
    }

    return (
        <div className="flex min-h-screen bg-fundo">
            <div className="sticky top-0 hidden h-screen shrink-0 md:block">
                <CapaLateral perfil={perfil} aoBuscar={abrirBusca} />
            </div>

            <div className="flex min-w-0 flex-1 flex-col">
                <FaixaConexao />
                <div className="flex items-center gap-3 bg-lateral px-4 py-2.5 text-lateral-tinta md:hidden">
                    <Button
                        aria-label="Abrir menu"
                        onPress={() => setGaveta(true)}
                        className="grid size-9 place-items-center rounded-couro hover:bg-lateral-ativo"
                    >
                        <Icone nome="menu" className="size-5" />
                    </Button>
                    <span className="font-titulo text-xl font-semibold text-dourado italic">acordu</span>
                    <Button
                        aria-label="Buscar"
                        onPress={abrirBusca}
                        className="ml-auto grid size-9 place-items-center rounded-couro hover:bg-lateral-ativo"
                    >
                        <Icone nome="busca" className="size-5" />
                    </Button>
                </div>
                <main id="conteudo" className="flex-1 px-4 py-6 sm:px-7">
                    <Outlet />
                </main>
            </div>

            <ModalOverlay
                isOpen={gaveta}
                onOpenChange={setGaveta}
                isDismissable
                className="fixed inset-0 z-40 bg-[var(--veu)] md:hidden"
            >
                <Modal className="h-full w-fit">
                    <Dialog aria-label="Menu" className="h-full outline-none">
                        <CapaLateral perfil={perfil} aoBuscar={abrirBusca} aoNavegar={() => setGaveta(false)} />
                    </Dialog>
                </Modal>
            </ModalOverlay>

            <PaletaComandos perfil={perfil} aberta={paletaAberta} aoMudar={setPaletaAberta} />
        </div>
    )
}
