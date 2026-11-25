import type { QueryClient } from '@tanstack/react-query'
import { assinar, estadoSessao } from './sessao'

/**
 * Outra empresa, outro usuário ou fim de uma sessão que existia (nesta aba ou em outra): nada do cache vale mais, e as
 * guardas das rotas decidem de novo para onde ir. Renovação comum e a primeira carga (com ou sem sessão) não mexem em
 * nada: limpar ali derrubaria as consultas que as telas públicas já fizeram (#222).
 */
export function limparCacheAoTrocarSessao(consultas: QueryClient, invalidarRotas: () => void): () => void {
    return assinar(() => {
        const { motivo } = estadoSessao()
        if (motivo !== 'troca' && motivo !== 'encerramento') return
        consultas.clear()
        invalidarRotas()
    })
}
