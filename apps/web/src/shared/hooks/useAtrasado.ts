import { useEffect, useState } from 'react'

/** O valor só muda depois de `ms` sem mudanças: para não consultar a API a cada tecla. */
export function useAtrasado<T>(valor: T, ms = 250): T {
    const [atrasado, setAtrasado] = useState(valor)
    useEffect(() => {
        const temporizador = setTimeout(() => setAtrasado(valor), ms)
        return () => clearTimeout(temporizador)
    }, [valor, ms])
    return atrasado
}
