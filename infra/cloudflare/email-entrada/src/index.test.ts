import { afterEach, describe, expect, it, vi } from 'vitest'
import {
    DESTINO_PADRAO,
    RECUSA_TAMANHO,
    TAMANHO_PADRAO,
    autenticidade,
    carimbo,
    dominioDoFrom,
    receber,
    type Env,
    type Mensagem,
} from './index'

const MIME = 'From: Ana <ana@cliente.com.br>\r\nTo: k7m2qd9w@entrada.acordu.app.br\r\nSubject: Forno\r\n\r\nParou.\r\n'
const ENV: Env = { TOKEN_ENTRADA: 'segredo-da-entrada' }

/** Mensagem do Email Routing de mentira: guarda a recusa e o corpo em stream. */
function mensagem(cabecalhos: Record<string, string> = {}, rawSize = MIME.length) {
    const recusas: string[] = []
    const m: Mensagem = {
        from: 'ana@cliente.com.br',
        to: 'k7m2qd9w@entrada.acordu.app.br',
        headers: new Headers({ from: 'Ana <ana@cliente.com.br>', ...cabecalhos }),
        raw: new Blob([MIME]).stream(),
        rawSize,
        setReject: (motivo: string) => {
            recusas.push(motivo)
        },
    }
    return { m, recusas }
}

/** `fetch` falso da rota de entrada: responde `status` (ou falha a rede) e guarda o pedido e o corpo lido. */
function rota(status: number | 'rede') {
    const pedidos: { url: string; init: RequestInit; corpo: string }[] = []
    const buscar = vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => {
        pedidos.push({ url: String(url), init: init!, corpo: await new Response(init?.body).text() })
        if (status === 'rede') throw new TypeError('fetch failed')
        return new Response(null, { status })
    })
    return { buscar: buscar as unknown as typeof fetch, pedidos, chamadas: buscar }
}

const cabecalho = (init: RequestInit, nome: string) => new Headers(init.headers).get(nome)

afterEach(() => vi.restoreAllMocks())

describe('entrada por e-mail (F15d)', () => {
    it('repassa o MIME bruto à rota pela borda, com o token e o envelope', async () => {
        const { m, recusas } = mensagem()
        const api = rota(202)

        const resultado = await receber(m, ENV, api.buscar)

        expect(resultado).toBe('repassada')
        expect(recusas).toEqual([])
        const [pedido] = api.pedidos
        expect(pedido!.url).toBe(DESTINO_PADRAO)
        expect(pedido!.init.method).toBe('POST')
        expect(pedido!.corpo).toBe(MIME)
        expect(cabecalho(pedido!.init, 'Content-Type')).toBe('message/rfc822')
        expect(cabecalho(pedido!.init, 'X-Email-Token')).toBe('segredo-da-entrada')
        expect(cabecalho(pedido!.init, 'X-Email-Remetente')).toBe('ana@cliente.com.br')
        expect(cabecalho(pedido!.init, 'X-Email-Destinatario')).toBe('k7m2qd9w@entrada.acordu.app.br')
        expect(cabecalho(pedido!.init, 'X-Email-Filtro')).toBe('ok')
        // O token de origem é da borda: o Worker nunca o manda.
        expect(cabecalho(pedido!.init, 'X-Origem-Token')).toBeNull()
    })

    it('F15-89: acima do tamanho máximo, recusa a entrega sem chamar a API', async () => {
        const { m, recusas } = mensagem({}, TAMANHO_PADRAO + 1)
        const api = rota(202)

        const resultado = await receber(m, ENV, api.buscar)

        expect(resultado).toBe('recusada-tamanho')
        expect(recusas).toEqual([RECUSA_TAMANHO])
        expect(api.chamadas).not.toHaveBeenCalled()
    })

    it('F15-89: o tamanho máximo vem de TAMANHO_MAXIMO, e o limite é inclusivo', async () => {
        const noLimite = mensagem({}, 1000)
        const acima = mensagem({}, 1001)
        const api = rota(202)

        expect(await receber(noLimite.m, { ...ENV, TAMANHO_MAXIMO: '1000' }, api.buscar)).toBe('repassada')
        expect(await receber(acima.m, { ...ENV, TAMANHO_MAXIMO: '1000' }, api.buscar)).toBe('recusada-tamanho')
        expect(api.chamadas).toHaveBeenCalledTimes(1)
    })

    it('F15-89: TAMANHO_MAXIMO inválido vale o padrão de 25 MiB', async () => {
        const { m, recusas } = mensagem({}, TAMANHO_PADRAO)

        expect(await receber(m, { ...ENV, TAMANHO_MAXIMO: 'muito' }, rota(202).buscar)).toBe('repassada')
        expect(recusas).toEqual([])
    })

    it('F15-89: o 413 da API também recusa a entrega', async () => {
        const { m, recusas } = mensagem()

        expect(await receber(m, ENV, rota(413).buscar)).toBe('recusada-tamanho')
        expect(recusas).toEqual([RECUSA_TAMANHO])
    })

    it('5xx, 404 ou rede fora: lança erro (falha temporária, o servidor de origem repete)', async () => {
        for (const status of [500, 503, 404, 400] as const) {
            await expect(receber(mensagem().m, ENV, rota(status).buscar)).rejects.toThrow(String(status))
        }
        await expect(receber(mensagem().m, ENV, rota('rede').buscar)).rejects.toThrow('fetch failed')
    })

    it('sem TOKEN_ENTRADA: falha temporária, sem chamar a API', async () => {
        const api = rota(202)

        await expect(receber(mensagem().m, {}, api.buscar)).rejects.toThrow('TOKEN_ENTRADA')
        expect(api.chamadas).not.toHaveBeenCalled()
    })

    it('o log não leva endereço, assunto nem conteúdo', async () => {
        const log = vi.spyOn(console, 'log').mockImplementation(() => undefined)

        await receber(mensagem().m, ENV, rota(202).buscar)
        await receber(mensagem({}, TAMANHO_PADRAO + 1).m, ENV, rota(202).buscar)
        await receber(mensagem().m, ENV, rota(413).buscar)

        const linhas = log.mock.calls.map((c) => c.join(' '))
        expect(linhas).toHaveLength(3)
        for (const linha of linhas) {
            expect(linha).not.toMatch(/@|cliente|k7m2qd9w|Forno|Parou/)
        }
    })

    it('endereço fora do ASCII não quebra o cabeçalho do pedido', async () => {
        const { m } = mensagem()
        const api = rota(202)

        await receber({ ...m, from: 'joão@cliente.com.br' }, ENV, api.buscar)

        expect(cabecalho(api.pedidos[0]!.init, 'X-Email-Remetente')).toBe('jo?o@cliente.com.br')
    })

    it('envelope vazio (devolução) vai como <>, para o cabeçalho chegar à API (F15-85)', async () => {
        const { m } = mensagem()
        const api = rota(202)

        await receber({ ...m, from: '' }, ENV, api.buscar)

        expect(cabecalho(api.pedidos[0]!.init, 'X-Email-Remetente')).toBe('<>')
    })
})

describe('veredito de autenticidade (design D11)', () => {
    const cf = (resultados: string) => ({ 'authentication-results': `mx.cloudflare.net; ${resultados}` })

    it('DMARC pass do carimbo do provedor: pass', () => {
        const { m } = mensagem(cf('dkim=pass header.d=cliente.com.br header.s=s1; spf=pass smtp.mailfrom=cliente.com.br; dmarc=pass header.from=cliente.com.br'))

        expect(autenticidade(m.headers)).toBe('pass')
    })

    it('DMARC fail: fail, mesmo com DKIM pass', () => {
        const { m } = mensagem(cf('dkim=pass header.d=outro.com; dmarc=fail (p=reject) header.from=cliente.com.br'))

        expect(autenticidade(m.headers)).toBe('fail')
    })

    it('sem DMARC publicado: DKIM pass alinhado ao From vale', () => {
        expect(autenticidade(mensagem(cf('dkim=pass header.d=cliente.com.br; dmarc=none header.from=cliente.com.br')).m.headers)).toBe('pass')
        expect(autenticidade(mensagem(cf('dkim=pass header.d=mail.cliente.com.br; spf=pass')).m.headers)).toBe('pass')
    })

    it('sem DMARC publicado: DKIM de outro domínio ou só SPF não valem', () => {
        expect(autenticidade(mensagem(cf('dkim=pass header.d=mailer.exemplo.net; dmarc=none')).m.headers)).toBe('fail')
        expect(autenticidade(mensagem(cf('spf=pass smtp.mailfrom=cliente.com.br; dmarc=none')).m.headers)).toBe('fail')
        expect(autenticidade(mensagem(cf('dkim=fail header.d=cliente.com.br')).m.headers)).toBe('fail')
    })

    it('sem carimbo: none; carimbo de outro servidor (que o remetente pode escrever) não conta', () => {
        expect(autenticidade(mensagem().m.headers)).toBe('none')
        expect(autenticidade(mensagem({ 'authentication-results': 'mx.forjado.com; dmarc=pass' }).m.headers)).toBe('none')
    })

    it('ARC-Authentication-Results do provedor serve quando não há Authentication-Results', () => {
        const { m } = mensagem({ 'arc-authentication-results': 'i=1; mx.cloudflare.net; dkim=pass header.d=cliente.com.br; dmarc=pass header.from=cliente.com.br' })

        expect(autenticidade(m.headers)).toBe('pass')
    })

    it('repetições juntadas pelo Headers: vale a primeira instância do provedor', () => {
        const headers = new Headers()
        headers.append('authentication-results', 'mx.cloudflare.net; dkim=fail header.d=cliente.com.br; dmarc=fail header.from=cliente.com.br')
        headers.append('authentication-results', 'mx.cloudflare.net; dmarc=pass header.from=cliente.com.br')
        headers.append('from', 'ana@cliente.com.br')

        expect(carimbo(headers)).toContain('dmarc=fail')
        expect(autenticidade(headers)).toBe('fail')
    })

    it('o veredito vai no pedido, e o log diz se veio o carimbo', async () => {
        const log = vi.spyOn(console, 'log').mockImplementation(() => undefined)
        const api = rota(202)

        await receber(mensagem(cf('dmarc=pass header.from=cliente.com.br')).m, ENV, api.buscar)
        await receber(mensagem().m, ENV, api.buscar)

        expect(cabecalho(api.pedidos[0]!.init, 'X-Email-Autenticidade')).toBe('pass')
        expect(cabecalho(api.pedidos[1]!.init, 'X-Email-Autenticidade')).toBe('none')
        expect(log.mock.calls[0]!.join(' ')).toContain('carimbo presente')
        expect(log.mock.calls[1]!.join(' ')).toContain('carimbo ausente')
    })

    it('SERVIDOR_AUTENTICACAO troca o servidor esperado', async () => {
        const api = rota(202)
        const { m } = mensagem({ 'authentication-results': 'mx.outro-provedor.net; dmarc=pass' })

        await receber(m, { ...ENV, SERVIDOR_AUTENTICACAO: 'mx.outro-provedor.net' }, api.buscar)

        expect(cabecalho(api.pedidos[0]!.init, 'X-Email-Autenticidade')).toBe('pass')
    })

    it('o domínio do From: um só; vários ou nenhum, nulo', () => {
        expect(dominioDoFrom('Ana <Ana@Cliente.com.br>')).toBe('cliente.com.br')
        expect(dominioDoFrom('ana@a.com, bruno@b.com')).toBeNull()
        expect(dominioDoFrom(null)).toBeNull()
    })
})

describe('handler do Email Routing', () => {
    it('o email() repassa com o fetch global', async () => {
        const buscar = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 202 }))
        const worker = (await import('./index')).default

        await worker.email(mensagem().m as ForwardableEmailMessage, ENV)

        expect(buscar).toHaveBeenCalledWith(DESTINO_PADRAO, expect.objectContaining({ method: 'POST' }))
    })

    it('fetch() sem rota pública: 404', async () => {
        const worker = (await import('./index')).default

        const resposta = await worker.fetch()

        expect(resposta.status).toBe(404)
    })
})
