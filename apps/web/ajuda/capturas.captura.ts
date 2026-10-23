import { mkdirSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { expect, test, type BrowserContext, type Locator, type Page } from '@playwright/test'
import { CAPTURAS, TAMANHO_CAPTURA, type CapturaAjuda } from '../src/features/ajuda/capturas'
import { criarDemonstracao, type Demonstracao } from './demonstracao'

/**
 * Bot das capturas do manual (M2): cria a empresa de demonstração, entra como a Dona e fotografa cada tela listada em
 * `CAPTURAS`, gravando `public/ajuda/<nome>.webp`. Roda no workflow Ajuda (pedido por `.github/ajuda-request`); fica
 * fora da fumaça (`playwright.ajuda.config.ts`).
 */

interface Receita {
    rota: (d: Demonstracao) => string
    /** O que precisa estar na tela para a foto valer. */
    pronto: (p: Page) => Locator
    /**
     * Tela larga demais para 1440 px (a linha do tempo tem dez dias úteis): fotografa nesta largura, na mesma
     * proporção, e reduz para o tamanho padrão.
     */
    largura?: number
    /** Quem está na tela: a Dona (padrão) ou o contato da padaria no portal. */
    portal?: boolean
}

const texto = (t: string) => (p: Page) => p.getByText(t).first()
const titulo = (t: string | RegExp) => (p: Page) => p.getByRole('heading', { level: 1, name: t })

const RECEITAS: Record<CapturaAjuda, Receita> = {
    hoje: { rota: () => '/', pronto: texto('Criar a conta de e-mail e o acesso') },
    agenda: { rota: () => '/agenda', pronto: texto('Trocar a impressora do caixa 2'), largura: 1920 },
    'agenda-plano': { rota: () => '/agenda?vista=plano', pronto: texto('Trocar a impressora do caixa 2') },
    solicitacoes: { rota: () => '/solicitacoes', pronto: texto('Caixa não imprime o cupom desde ontem') },
    solicitacao: { rota: (d) => `/solicitacoes/${d.solicitacaoId}`, pronto: texto('Parece a cabeça térmica') },
    'tarefas-quadro': { rota: () => '/tarefas?modo=quadro', pronto: texto('Catálogo de produtos') },
    horas: { rota: () => '/horas', pronto: texto('Caixa de e-mail criada') },
    cliente: { rota: (d) => `/clientes/${d.clienteId}`, pronto: titulo('Padaria Grão Dourado') },
    contrato: { rota: (d) => `/contratos/${d.contratoId}`, pronto: texto('Padaria Grão Dourado') },
    projetos: { rota: () => '/projetos', pronto: texto('Novo site da Casa Verde') },
    recorrencias: { rota: () => '/recorrencias', pronto: texto('Conferir o backup dos processos') },
    panorama: { rota: () => '/panorama', pronto: titulo('Panorama') },
    configuracoes: { rota: () => '/configuracoes/empresa', pronto: titulo('Configurações') },
    'portal-lista': { rota: () => '/portal', pronto: texto('Caixa não imprime o cupom desde ontem'), portal: true },
    'portal-detalhe': {
        rota: (d) => `/portal/solicitacoes/${d.entregaId}`,
        pronto: (p) => p.getByRole('button', { name: 'Aprovar entrega' }),
        portal: true,
    },
    'portal-nova': { rota: () => '/portal/nova', pronto: titulo('Nova solicitação'), portal: true },
    'portal-contratos': { rota: () => '/portal/contratos', pronto: texto('Banco de horas'), portal: true },
}

const PASTA = fileURLToPath(new URL('../public/ajuda/', import.meta.url))

test.use({
    viewport: { width: TAMANHO_CAPTURA.largura, height: TAMANHO_CAPTURA.altura },
    deviceScaleFactor: 1,
    colorScheme: 'light',
    contextOptions: { reducedMotion: 'reduce' },
})

/** Espera a tela assentar: nada carregando, fontes prontas e nenhuma chamada em voo (o SSE não conta). */
async function esperarSossego(page: Page, emVoo: Set<string>) {
    await expect(page.getByText('Carregando…')).toHaveCount(0)
    await page.evaluate(() => document.fonts.ready.then(() => undefined))
    await expect.poll(() => emVoo.size, { timeout: 10_000 }).toBe(0)
    await page.waitForTimeout(400)
}

/** Uma aba já logada, no tema claro, que conta as chamadas em voo para `esperarSossego`. */
async function entrar(contexto: BrowserContext, email: string, senha: string, inicio: RegExp | string) {
    // Tema claro fixo, qualquer que seja a escolha guardada.
    await contexto.addInitScript(() => localStorage.setItem('acordu.tema', 'claro'))
    const page = await contexto.newPage()
    const emVoo = new Set<string>()
    page.on('request', (r) => {
        if (r.url().includes('/api/') && !r.url().endsWith('/eventos')) emVoo.add(r.url() + r.method())
    })
    const saiu = (r: { url: () => string; method: () => string }) => emVoo.delete(r.url() + r.method())
    page.on('requestfinished', saiu)
    page.on('requestfailed', saiu)

    await page.goto('/entrar')
    await page.getByLabel('E-mail').fill(email)
    await page.getByLabel('Senha').fill(senha)
    await page.getByRole('button', { name: 'Entrar' }).click()
    await expect(page.getByRole('heading', { level: 1, name: inicio })).toBeVisible()
    return { page, emVoo }
}

test('captura as telas do manual', async ({ browser, context }) => {
    test.setTimeout(300_000)
    mkdirSync(PASTA, { recursive: true })
    const demo = await criarDemonstracao()

    const equipe = await entrar(context, demo.email, demo.senha, /, Marina$/)
    // O portal em outro contexto: cada um com o seu cookie de sessão.
    const portal = await entrar(
        await browser.newContext({
            baseURL: 'http://localhost:5173',
            locale: 'pt-BR',
            timezoneId: 'America/Sao_Paulo',
            colorScheme: 'light',
            reducedMotion: 'reduce',
            deviceScaleFactor: 1,
        }),
        demo.contato.email,
        demo.contato.senha,
        'Solicitações',
    )

    // A conversão para WebP usa o próprio Chromium (canvas), sem dependência de imagem no projeto.
    const conversor = await context.newPage()
    const paraWebp = async (png: Buffer) => {
        const url = await conversor.evaluate(
            async ({ b64, largura, altura }) => {
                const img = new Image()
                img.src = `data:image/png;base64,${b64}`
                await img.decode()
                const canvas = document.createElement('canvas')
                canvas.width = largura
                canvas.height = altura
                const contexto = canvas.getContext('2d')!
                contexto.imageSmoothingQuality = 'high'
                contexto.drawImage(img, 0, 0, largura, altura)
                return canvas.toDataURL('image/webp', 0.85)
            },
            { b64: png.toString('base64'), largura: TAMANHO_CAPTURA.largura, altura: TAMANHO_CAPTURA.altura },
        )
        return Buffer.from(url.slice(url.indexOf(',') + 1), 'base64')
    }

    for (const nome of CAPTURAS) {
        const receita = RECEITAS[nome]
        await test.step(nome, async () => {
            const { page, emVoo } = receita.portal ? portal : equipe
            const largura = receita.largura ?? TAMANHO_CAPTURA.largura
            await page.setViewportSize({
                width: largura,
                height: Math.round((largura * TAMANHO_CAPTURA.altura) / TAMANHO_CAPTURA.largura),
            })
            await page.goto(receita.rota(demo))
            await expect(receita.pronto(page)).toBeVisible({ timeout: 15_000 })
            await esperarSossego(page, emVoo)
            const png = await page.screenshot({ animations: 'disabled', caret: 'hide' })
            writeFileSync(`${PASTA}${nome}.webp`, await paraWebp(png))
        })
    }

    // Captura que saiu da lista sai da pasta também.
    for (const arquivo of readdirSync(PASTA)) {
        if (arquivo.endsWith('.webp') && !(CAPTURAS as readonly string[]).includes(arquivo.slice(0, -5)))
            rmSync(`${PASTA}${arquivo}`)
    }
})
