// Confere a CSP do Pages (dist/_headers) contra o build (spec do F8, F8-22). Roda depois do build na CI: um script ou
// estilo inline que muda sem o hash mudar junto quebraria o SPA só em produção.
// - script-src: cada <script> inline do dist/index.html, pelo hash;
// - style-src: os <style> que o React Aria cria em tempo de execução. O texto deles está no bundle; aqui ficam os textos
//   esperados e conferimos que continuam iguais no bundle (upgrade do React Aria que mude o texto falha aqui);
// - connect-src: a lista exata de origens, sem curinga (spec do F10, F10-21).
import { createHash } from 'node:crypto'
import { readdirSync, readFileSync } from 'node:fs'

const dist = new URL('../dist/', import.meta.url)
const html = readFileSync(new URL('index.html', dist), 'utf8')
const cabecalhos = readFileSync(new URL('_headers', dist), 'utf8')
const csp = cabecalhos.split('\n').find((l) => l.trim().startsWith('Content-Security-Policy:')) ?? ''
const diretiva = (nome) => csp.split(';').find((d) => d.trim().startsWith(`${nome} `)) ?? ''
const hash = (texto) => `'sha256-${createHash('sha256').update(texto, 'utf8').digest('base64')}'`

const erros = []

const scripts = [...html.matchAll(/<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)<\/script>/g)].map((m) => m[1])
for (const codigo of scripts)
    if (!diretiva('script-src').includes(hash(codigo)))
        erros.push(`script-src sem ${hash(codigo)} (script do index.html)`)

const bundle = readdirSync(new URL('assets/', dist))
    .filter((f) => f.endsWith('.js'))
    .map((f) => readFileSync(new URL(`assets/${f}`, dist), 'utf8'))
    .join('\n')

// Texto aplicado (o que o navegador hasheia) e trecho que precisa existir igual no bundle minificado.
const estilos = [
    {
        nome: 'React Aria usePress',
        aplicado: '@layer {\n  [data-react-aria-pressable] {\n    touch-action: pan-x pan-y pinch-zoom;\n  }\n}',
        noBundle: '\n@layer {\n  [${',
        resto: '] {\n    touch-action: pan-x pan-y pinch-zoom;\n  }\n}\n',
    },
    {
        nome: 'React Aria usePreventScroll',
        aplicado: '@layer {\n  * {\n    overscroll-behavior: contain;\n  }\n}',
        noBundle: '`@layer {\n  * {\n    overscroll-behavior: contain;\n  }\n}`',
    },
]
for (const e of estilos) {
    if (!bundle.includes(e.noBundle) || (e.resto && !bundle.includes(e.resto)))
        erros.push(
            `${e.nome}: o texto do <style> mudou no bundle; recalcule o hash e atualize este script e o _headers`,
        )
    if (!diretiva('style-src').includes(hash(e.aplicado))) erros.push(`style-src sem ${hash(e.aplicado)} (${e.nome})`)
}
// O Turnstile do cadastro (RNF-SEG-10, F9-45): script e iframe vêm do Cloudflare; o resto da política segue fechado.
const TURNSTILE = 'https://challenges.cloudflare.com'
for (const nome of ['script-src', 'frame-src'])
    if (!diretiva(nome).split(/\s+/).includes(TURNSTILE)) erros.push(`${nome} sem ${TURNSTILE} (Turnstile do cadastro)`)
// connect-src (spec do F10, F10-21): a API e os serviços públicos que o navegador consulta direto (CEP e CNPJ), nada
// além disso e sem curinga. Serviço novo entra aqui e na segurança §9 no mesmo PR.
const CONEXOES = ["'self'", 'https://api.acordu.app.br', 'https://viacep.com.br', 'https://brasilapi.com.br']
const conexoes = diretiva('connect-src').trim().split(/\s+/).slice(1)
for (const origem of CONEXOES) if (!conexoes.includes(origem)) erros.push(`connect-src sem ${origem}`)
for (const origem of conexoes)
    if (origem.includes('*')) erros.push(`connect-src não pode ter curinga (${origem})`)
    else if (!CONEXOES.includes(origem)) erros.push(`connect-src com ${origem}, que não está na lista conferida`)
for (const proibido of ["'unsafe-inline'", "'unsafe-eval'"])
    if (csp.includes(proibido)) erros.push(`a política não pode ter ${proibido}`)
if (diretiva('script-src').split(/\s+/).includes('*')) erros.push('script-src não pode ter o curinga *')

// O react-dom também cria <style>, mas só para `<style precedence>`, que o app não usa: fica fora da conta.
const criados = [...bundle.matchAll(/createElement\(`style`\)/g)].filter(
    (m) => !bundle.slice(Math.max(0, m.index - 300), m.index).includes('precedence'),
).length
if (criados !== estilos.length)
    erros.push(`o bundle cria ${criados} <style>, e a CSP conhece ${estilos.length}: confira o que mudou`)

if (erros.length > 0) {
    for (const erro of erros) console.error(`::error::CSP do Pages: ${erro}`)
    process.exit(1)
}
console.log(`CSP ok: ${scripts.length} script(s) e ${estilos.length} estilo(s) inline com hash na política.`)
