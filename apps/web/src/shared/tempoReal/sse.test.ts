import { describe, expect, it } from 'vitest'
import { LeitorSse } from './sse'

describe('leitor SSE', () => {
    it('junta mensagens partidas entre pedaços e ignora comentários', () => {
        const leitor = new LeitorSse()
        expect(leitor.alimentar('retry: 5000\n\n: vivo\n\nevent: mud')).toEqual([])
        expect(leitor.retry).toBe(5000)
        expect(leitor.alimentar('anca\ndata: {"tipo":"tarefa","id":"t1"}\n')).toEqual([])
        expect(leitor.alimentar('\nevent: mudanca\r\ndata: {"tipo":"cliente","id":"c1"}\r\n\r\n')).toEqual([
            { evento: 'mudanca', dados: '{"tipo":"tarefa","id":"t1"}' },
            { evento: 'mudanca', dados: '{"tipo":"cliente","id":"c1"}' },
        ])
    })
})
