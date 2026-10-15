import { Link } from '@tanstack/react-router'
import { Button } from 'react-aria-components'
import type { Perfil } from '@/shared/sessao/perfil'
import { Icone } from '@/shared/ui/Icone'
import { MenuUsuario } from './MenuUsuario'
import { navegacaoDe } from './navegacao'
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

            <ul className="flex flex-col gap-px">
                {navegacaoDe(perfil).map((item) => (
                    <li key={item.para} className="flex flex-col">
                        {item.separar && <span aria-hidden="true" className="mx-2 my-2 h-px bg-lateral-linha" />}
                        <Link
                            to={item.para}
                            activeOptions={{ exact: item.para === '/' }}
                            className={estiloLink}
                            onClick={aoNavegar}
                        >
                            {item.rotulo}
                        </Link>
                    </li>
                ))}
            </ul>

            <div className="mt-auto flex flex-col gap-2">
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
