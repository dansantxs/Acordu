import { useEffect, useState } from 'react'

/** O instante atual, renovado a cada `intervaloMs` (relógio na tela, como o do cronômetro). */
export function useAgora(intervaloMs = 15_000): number {
    const [agora, setAgora] = useState(() => Date.now())
    useEffect(() => {
        const id = setInterval(() => setAgora(Date.now()), intervaloMs)
        return () => clearInterval(id)
    }, [intervaloMs])
    return agora
}
