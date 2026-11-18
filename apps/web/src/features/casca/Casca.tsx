import { useQueryClient } from '@tanstack/react-query'
import { Outlet, useRouter } from '@tanstack/react-router'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button, Dialog, Modal, ModalOverlay } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { AvisoTextosPendentes } from '@/features/legal/AvisoTextosPendentes'
import { AvisoEnvioBloqueado } from '@/features/sessao/AvisoConfirmacao'
import { anunciarManifesto } from '@/shared/app/instalacao'
import { useLembreteCronometro } from '@/shared/cronometro/useLembreteCronometro'
import { escopoNotificacoes } from '@/shared/notificacoes/navegador'
import { ContextoGravacao, ID_MOTIVO_SEM_GRAVACAO, podeGravar, useAoRecusarGravacao } from '@/shared/sessao/gravacao'
import { consultaPerfil, type Perfil } from '@/shared/sessao/perfil'
import { aplicarCouro, couroAtual } from '@/shared/tema/tema'
import { useTempoReal } from '@/shared/tempoReal/useTempoReal'
import { FaixaConexao } from '@/shared/ui/FaixaConexao'
import { Icone } from '@/shared/ui/Icone'
import { CapaLateral } from './CapaLateral'
import { FaixaSituacao } from './FaixaSituacao'
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
    // Lembrete no expediente (F12d): montado aqui, uma vez, e não na capa (que aparece duas vezes no DOM).
    const roteador = useRouter()
    const abrirCaminho = useCallback((caminho: string) => roteador.history.push(caminho), [roteador])
    useLembreteCronometro(escopoNotificacoes(perfil), pode(perfil, 'apontamento.criar'), abrirCaminho)
    // App instalável só para a equipe (F12-26): o manifesto entra com a casca.
    useEffect(() => anunciarManifesto(), [])
    // O couro escolhido vale no app da equipe (o portal tira; ver CascaPortal).
    useEffect(() => aplicarCouro(couroAtual()), [])
    const abrirBusca = () => {
        setGaveta(false)
        setPaletaAberta(true)
    }
    // Só leitura (ADR 0015): um `podeGravar` só para todas as ações da equipe (convenções §4). A recusa que chega com
    // a tela aberta (a situação mudou) relê o perfil, e a faixa e as ações desabilitadas aparecem (F14-27).
    const consultas = useQueryClient()
    useAoRecusarGravacao(() => {
        void consultas.refetchQueries({ queryKey: consultaPerfil.queryKey }).then(() => roteador.invalidate())
    })
    const grava = podeGravar(perfil.tenant)
    const gravacao = useMemo(
        () => ({ podeGravar: grava, idMotivo: grava ? undefined : ID_MOTIVO_SEM_GRAVACAO }),
        [grava],
    )

    return (
        <ContextoGravacao value={gravacao}>
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
                    <main id="conteudo" className="flex flex-1 flex-col gap-4 px-4 py-6 sm:px-7">
                        <FaixaSituacao tenant={perfil.tenant} />
                        <AvisoEnvioBloqueado perfil={perfil} />
                        <AvisoTextosPendentes perfil={perfil} />
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
        </ContextoGravacao>
    )
}
