import { createFileRoute } from '@tanstack/react-router'
import { SeletorEmpresa } from '@/features/sessao/SeletorEmpresa'
import { formatarDuracao } from '@/shared/format/duracao'
import { sair } from '@/shared/sessao/acoes'
import { SeletorTema } from '@/shared/tema/SeletorTema'
import { Botao } from '@/shared/ui/Botao'

// Página provisória: mostra a capa, a sessão e os tokens. A casca do app (capa lateral, cabeçalho, sino) entra no F1c.
export const Route = createFileRoute('/_app/')({ component: Inicio })

function Inicio() {
    const { perfil } = Route.useRouteContext()
    return (
        <div className="flex min-h-screen">
            <nav
                aria-label="Principal"
                className="flex w-56 flex-col gap-4 bg-lateral p-5 text-lateral-tinta outline-[1.5px] -outline-offset-6 outline-dashed outline-costura"
            >
                <span className="font-titulo text-2xl font-semibold italic text-dourado">acordu</span>
                <span className="text-xs text-lateral-suave">{perfil.papel}</span>
            </nav>
            <main className="flex flex-1 flex-col gap-6 p-8">
                <header className="flex flex-wrap items-end justify-between gap-4">
                    <div>
                        <p className="text-sm text-suave">Olá, {perfil.nome}</p>
                        <h1 className="font-titulo text-3xl font-semibold tracking-tight">Acordu</h1>
                    </div>
                    <div className="flex items-center gap-3">
                        <SeletorEmpresa perfil={perfil} />
                        <SeletorTema />
                        <Botao variante="secundario" onPress={() => void sair()}>
                            Sair
                        </Botao>
                    </div>
                </header>
                <section className="flex flex-wrap gap-3" aria-label="Situações">
                    <span className="rounded-couro bg-verde-suave px-3 py-1 text-verde">No prazo</span>
                    <span className="rounded-couro bg-ambar-suave px-3 py-1 text-ambar-texto">Em risco</span>
                    <span className="rounded-couro bg-vermelho-suave px-3 py-1 text-vermelho">Violado</span>
                    <span className="rounded-couro bg-acento-suave px-3 py-1 text-acento-texto">
                        {formatarDuracao(90)} apontadas
                    </span>
                </section>
            </main>
        </div>
    )
}
