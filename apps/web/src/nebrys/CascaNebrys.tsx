import { useQueryClient } from '@tanstack/react-query'
import { Link, Outlet, useRouter } from '@tanstack/react-router'
import { useEffect, useSyncExternalStore } from 'react'
import {
    assinarSessaoOperacao,
    encerrarSessaoOperacao,
    lerSessaoOperacao,
    sessaoOperacao,
    versaoSessaoOperacao,
} from '@/shared/sessao/sessaoOperacao'
import { SeletorTema } from '@/shared/tema/SeletorTema'
import { Botao } from '@/shared/ui/Botao'

const estiloLink =
    'rounded-couro px-3 py-1.5 text-sm text-lateral-suave hover:text-lateral-tinta aria-[current=page]:bg-black/20 aria-[current=page]:font-semibold aria-[current=page]:text-lateral-tinta'

/** Hora local do fim da sessão ("até 14:30"). */
function horaDoFim(expiraEm: string): string {
    return new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' }).format(new Date(expiraEm))
}

/**
 * Moldura da área da Nebrys: faixa de couro no topo com Empresas, Convites, Lista de espera, Liberar documento e Sair. Não tem nada do app da
 * equipe (capa, sino, paleta, ajuda): a área fica fora do manual. Quando a sessão de operação acaba (prazo, recusa da
 * API ou "Sair"), o cache da área some e as rotas voltam para a entrada.
 */
export function CascaNebrys() {
    const roteador = useRouter()
    const consultas = useQueryClient()
    useSyncExternalStore(assinarSessaoOperacao, versaoSessaoOperacao)
    // Só lê: encerrar a sessão vencida durante a renderização avisaria os ouvintes no meio dela (ressalva do F14m). O
    // temporizador e o cliente da área encerram no prazo.
    const sessao = lerSessaoOperacao()

    useEffect(
        () =>
            assinarSessaoOperacao(() => {
                if (sessaoOperacao()) return
                consultas.removeQueries({ queryKey: ['nebrys'] })
                void roteador.invalidate()
            }),
        [consultas, roteador],
    )

    return (
        <div className="min-h-screen bg-fundo text-tinta">
            <header className="bg-lateral text-lateral-tinta">
                <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-6 gap-y-2 px-4 py-3 sm:px-8">
                    <span className="font-titulo text-2xl font-semibold italic text-dourado">acordu</span>
                    <span className="text-sm text-lateral-suave">Nebrys</span>
                    <nav aria-label="Área da Nebrys" className="flex gap-1">
                        <Link to="/nebrys/empresas" className={estiloLink}>
                            Empresas
                        </Link>
                        <Link to="/nebrys/convites" className={estiloLink}>
                            Convites
                        </Link>
                        <Link to="/nebrys/lista-espera" className={estiloLink}>
                            Lista de espera
                        </Link>
                        <Link to="/nebrys/documentos" className={estiloLink}>
                            Liberar documento
                        </Link>
                    </nav>
                    <div className="ml-auto flex items-center gap-3">
                        {sessao && (
                            <span className="text-xs text-lateral-suave">Sessão até {horaDoFim(sessao.expiraEm)}</span>
                        )}
                        <SeletorTema />
                        <Botao variante="secundario" onPress={() => encerrarSessaoOperacao('saiu')}>
                            Sair
                        </Botao>
                    </div>
                </div>
            </header>
            <main className="mx-auto max-w-6xl px-4 py-8 sm:px-8">
                <Outlet />
            </main>
        </div>
    )
}

/** Endereço da área que não existe (com sessão de operação). */
export function NaoEncontradaNebrys() {
    return (
        <div className="flex flex-col items-start gap-3 rounded-couro border border-dashed border-linha px-6 py-10">
            <p className="font-titulo text-xl font-semibold">Página não encontrada</p>
            <Link
                to="/nebrys/empresas"
                className="text-sm font-semibold text-acento-texto underline-offset-4 hover:underline"
            >
                Ir para as empresas
            </Link>
        </div>
    )
}
