import { useState } from 'react'
import { ToggleButton } from 'react-aria-components'
import { escolherTema, temaAtual, type Tema } from './tema'

/** Alterna claro e escuro; a escolha fica guardada no navegador. */
export function SeletorTema() {
    const [tema, setTema] = useState<Tema>(temaAtual)
    const escuro = tema === 'escuro'
    return (
        <ToggleButton
            isSelected={escuro}
            onChange={(ligado) => setTema(escolherTema(ligado ? 'escuro' : 'claro'))}
            className="rounded-couro border border-linha bg-superficie px-3 py-1.5 text-sm text-tinta hover:bg-trilho"
        >
            {escuro ? 'Tema claro' : 'Tema escuro'}
        </ToggleButton>
    )
}
