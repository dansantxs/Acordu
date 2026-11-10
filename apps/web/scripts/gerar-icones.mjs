// Gera os ícones do app instalável (spec do F12, D8) a partir do desenho do favicon: couro, costura tracejada e o "a"
// em itálico dourado, com a fonte do próprio app (Fraunces) embutida, para não depender das fontes da máquina.
// Uso: node scripts/gerar-icones.mjs (precisa do Chromium do Playwright). Os PNG vão para public/icones/.
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from '@playwright/test'

const raiz = join(dirname(fileURLToPath(import.meta.url)), '..')
const fonte = readFileSync(
    join(raiz, 'node_modules/@fontsource-variable/fraunces/files/fraunces-latin-standard-italic.woff2'),
).toString('base64')

/** `margem`: fração de cada lado sem desenho (ícone "maskable" precisa de 10% de zona segura). */
function svg({ cantos, margem }) {
    const m = 32 * margem
    const lado = 32 - 2 * m
    const costura = 3.5 * (lado / 32)
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="100%" height="100%">
  <rect width="32" height="32" rx="${cantos}" fill="#3B2A20"/>
  <rect x="${m + costura}" y="${m + costura}" width="${lado - 2 * costura}" height="${lado - 2 * costura}"
        rx="${5 * (lado / 32)}" fill="none" stroke="#C9A877" stroke-width="${lado / 32}" stroke-dasharray="${2 * (lado / 32)} ${2 * (lado / 32)}"/>
  <text x="16" y="${16 + 6 * (lado / 32)}" text-anchor="middle" font-family="Fraunces" font-style="italic"
        font-size="${17 * (lado / 32)}" fill="#E2C48F">a</text>
</svg>`
}

const ICONES = [
    { arquivo: 'icone-192.png', tamanho: 192, cantos: 7, margem: 0 },
    { arquivo: 'icone-512.png', tamanho: 512, cantos: 7, margem: 0 },
    // Maskable: o sistema recorta (círculo, gota); fundo cheio até a borda e o desenho dentro dos 80% do meio.
    { arquivo: 'icone-maskable-512.png', tamanho: 512, cantos: 0, margem: 0.1 },
    // Apple: o iOS arredonda sozinho; fundo cheio.
    { arquivo: 'apple-touch-icon.png', tamanho: 180, cantos: 0, margem: 0.04 },
]

// ACORDU_CHROMIUM: caminho de outro Chromium, se o do Playwright do projeto não estiver instalado.
const navegador = await chromium.launch(
    process.env.ACORDU_CHROMIUM ? { executablePath: process.env.ACORDU_CHROMIUM } : {},
)
try {
    for (const icone of ICONES) {
        const pagina = await navegador.newPage({ viewport: { width: icone.tamanho, height: icone.tamanho } })
        await pagina.setContent(`<!doctype html><html><head><style>
            @font-face { font-family: Fraunces; font-style: italic; src: url(data:font/woff2;base64,${fonte}) format('woff2'); }
            html, body { margin: 0; background: transparent; }
            </style></head><body>${svg(icone)}</body></html>`)
        await pagina.evaluate(() => document.fonts.ready)
        await pagina.screenshot({ path: join(raiz, 'public/icones', icone.arquivo), omitBackground: true })
        await pagina.close()
    }
} finally {
    await navegador.close()
}
console.log('Ícones gerados em public/icones/.')
