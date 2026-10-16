import { Label } from 'react-aria-components'

/**
 * Rótulo de campo com o `*` dos obrigatórios (convenções §4, decidido em 16/10). O asterisco é só visual: o campo já
 * leva `aria-required`, e o leitor de tela diz "obrigatório".
 */
export function Rotulo({
    texto,
    obrigatorio = false,
    oculto = false,
}: {
    texto: string
    obrigatorio?: boolean
    oculto?: boolean
}) {
    return (
        // O asterisco fica fora do <label>: não entra no nome do campo, que continua sendo só o texto.
        <span className={oculto ? 'sr-only' : 'flex items-baseline gap-0.5'}>
            <Label className="text-sm font-semibold text-tinta">{texto}</Label>
            {obrigatorio && !oculto && (
                <span aria-hidden="true" className="text-sm font-semibold text-acento-texto">
                    *
                </span>
            )}
        </span>
    )
}

/** Legenda no topo dos formulários que têm campos opcionais. */
export function LegendaObrigatorios() {
    return (
        <p className="text-xs text-suave">
            <span aria-hidden="true" className="text-acento-texto">
                *
            </span>{' '}
            obrigatório
        </p>
    )
}
