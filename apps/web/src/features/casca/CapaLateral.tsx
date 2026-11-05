import { Link } from '@tanstack/react-router'
import { useId } from 'react'
import { Button } from 'react-aria-components'
import type { Perfil } from '@/shared/sessao/perfil'
import { Icone } from '@/shared/ui/Icone'
import { MenuUsuario } from './MenuUsuario'
import { IndicadorCronometro } from '@/features/cronometro/Cronometro'
import { PontoNovidade } from '@/features/novidades/ListaNotas'
import { useHaNovidade } from '@/features/novidades/vistas'
import { gruposDe, pode } from './navegacao'
import { Sino } from './Sino'

const estiloLink =
    'flex items-center justify-between rounded-couro px-2.5 py-1.5 text-lateral-tinta hover:bg-lateral-ativo/70 data-[status=active]:bg-lateral-ativo data-[status=active]:font-semibold'

/**
 * Capa de couro do app (protótipo): marca e empresa, sino, navegação por grupo, busca e quem está usando. Em tela
 * estreita vira gaveta (ver `Casca`).
 */
export function CapaLateral({
    perfil,
    aoBuscar,
    aoNavegar,
}: {
    perfil: Perfil
    aoBuscar: () => void
    aoNavegar?: () => void
}) {
    const haNovidade = useHaNovidade('equipe')
    // A capa aparece duas vezes no DOM (fixa e na gaveta do celular): os ids dos títulos não podem repetir.
    const prefixo = useId()
    return (
        <nav
            aria-label="Principal"
            className="flex h-full w-56 flex-col gap-5 bg-lateral px-3 py-4 text-lateral-tinta shadow-[inset_-3px_0_0_rgb(0_0_0/0.18)] outline-[1.5px] -outline-offset-6 outline-dashed outline-costura"
        >
            <div className="flex items-start justify-between gap-2 px-2">
                <div className="flex min-w-0 flex-col gap-0.5">
                    <span className="font-titulo text-2xl font-semibold tracking-tight text-dourado italic">
                        acordu
                    </span>
                    <span className="truncate text-xs text-lateral-suave">{perfil.tenant.nome}</span>
                </div>
                <Sino fuso={perfil.tenant.fusoHorario} />
            </div>

            {/* Rola sozinha em tela baixa, para o cronômetro, a busca e o menu do usuário continuarem ao alcance. */}
            <div className="-mx-1 flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto px-1">
                {gruposDe(perfil).map(({ grupo, titulo, itens }, i) => (
                    <div key={grupo} className="flex flex-col gap-px">
                        {titulo ? (
                            <span
                                id={`${prefixo}-${grupo}`}
                                className="px-2.5 pb-1 text-[11px] font-semibold tracking-wide text-lateral-suave uppercase"
                            >
                                {titulo}
                            </span>
                        ) : (
                            i > 0 && <span aria-hidden="true" className="mx-2 mb-2 h-px bg-lateral-linha" />
                        )}
                        <ul
                            aria-labelledby={titulo ? `${prefixo}-${grupo}` : undefined}
                            className="flex flex-col gap-px"
                        >
                            {itens.map((item) => (
                                <li key={item.para} className="flex flex-col">
                                    <Link
                                        to={item.para}
                                        activeOptions={{ exact: item.para === '/' }}
                                        className={estiloLink}
                                        onClick={aoNavegar}
                                    >
                                        {item.rotulo}
                                        {item.para === '/novidades' && haNovidade && <PontoNovidade />}
                                    </Link>
                                </li>
                            ))}
                        </ul>
                    </div>
                ))}
            </div>

            <div className="flex shrink-0 flex-col gap-2">
                {pode(perfil, 'apontamento.criar') && <IndicadorCronometro />}
                <Button
                    onPress={aoBuscar}
                    className="flex items-center gap-2 rounded-couro bg-lateral-cartao px-3 py-2 text-left text-sm text-lateral-suave hover:text-lateral-tinta"
                >
                    <Icone nome="busca" />
                    <span className="mr-auto">Buscar</span>
                    <kbd className="rounded border border-lateral-linha px-1.5 font-sans text-[11px]">Ctrl K</kbd>
                </Button>
                <MenuUsuario perfil={perfil} />
            </div>
        </nav>
    )
}
