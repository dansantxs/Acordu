import { useSyncExternalStore } from 'react'

function assinar(aviso: () => void) {
    window.addEventListener('online', aviso)
    window.addEventListener('offline', aviso)
    return () => {
        window.removeEventListener('online', aviso)
        window.removeEventListener('offline', aviso)
    }
}

/** Faixa no topo quando o navegador perde a rede. Ao voltar, o TanStack Query busca de novo sozinho. */
export function FaixaConexao() {
    const online = useSyncExternalStore(assinar, () => navigator.onLine)
    if (online) return null
    return (
        <div role="status" className="bg-ambar-suave px-4 py-2 text-center text-sm text-ambar-texto">
            Sem conexão. O que estiver na tela pode estar desatualizado; as mudanças voltam quando a internet voltar.
        </div>
    )
}
