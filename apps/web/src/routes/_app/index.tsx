import { createFileRoute } from '@tanstack/react-router'
import { formatarDuracao } from '@/shared/format/duracao'
import { SeletorTema } from '@/shared/tema/SeletorTema'

// Página provisória da fundação (F1a): mostra a capa, o papel e os tokens. A casca do app entra no F1c.
export const Route = createFileRoute('/')({ component: Fundacao })

function Fundacao() {
    return (
        <div className="flex min-h-screen">
            <nav
                aria-label="Principal"
                className="flex w-56 flex-col gap-4 bg-lateral p-5 text-lateral-tinta outline-[1.5px] -outline-offset-6 outline-dashed outline-costura"
            >
                <span className="font-titulo text-2xl font-semibold italic text-dourado">acordu</span>
                <span className="text-xs text-lateral-suave">fundação do frontend</span>
            </nav>
            <main className="flex flex-1 flex-col gap-6 p-8">
                <header className="flex items-end justify-between gap-4">
                    <div>
                        <p className="text-sm text-suave">Agenda de couro, tema claro e escuro</p>
                        <h1 className="font-titulo text-3xl font-semibold tracking-tight">Acordu</h1>
                    </div>
                    <SeletorTema />
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
