// Windows e macOS não diferenciam maiúsculas no nome de arquivo: `AjudaPortal.tsx` e `ajudaPortal.ts` viram o mesmo
// módulo para o Vite, e o import de um pega o outro (defeito de 06/11 na Ajuda do portal). Este script falha se dois
// arquivos de `src/` tiverem o mesmo nome sem extensão, ignorando maiúsculas, na mesma pasta.
import { readdirSync } from 'node:fs'
import { join, parse } from 'node:path'

const conflitos = []
function varrer(pasta) {
    const vistos = new Map()
    for (const item of readdirSync(pasta, { withFileTypes: true })) {
        const caminho = join(pasta, item.name)
        if (item.isDirectory()) {
            varrer(caminho)
            continue
        }
        // `.test.tsx` e `.tsx` do mesmo nome são arquivos diferentes de propósito: compara o nome sem a última extensão.
        const chave = parse(item.name).name.toLowerCase()
        const outro = vistos.get(chave)
        if (outro && parse(outro).name !== parse(item.name).name) conflitos.push(`${join(pasta, outro)} × ${caminho}`)
        else vistos.set(chave, item.name)
    }
}
varrer('src')
if (conflitos.length > 0) {
    console.error('Nomes que só diferem em maiúsculas (quebram no Windows e no macOS):')
    for (const c of conflitos) console.error(`  ${c}`)
    process.exit(1)
}
console.log('Nomes ok')
