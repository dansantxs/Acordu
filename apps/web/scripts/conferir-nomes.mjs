// Windows e macOS não diferenciam maiúsculas no nome de arquivo nem de pasta: `AjudaPortal.tsx` e `ajudaPortal.ts`
// viram o mesmo módulo para o Vite, e o import de um pega o outro (defeito de 06/11 na Ajuda do portal). Este script
// falha quando, na mesma pasta, dois itens têm o mesmo nome ignorando maiúsculas: arquivos (sem a última extensão),
// pastas, ou uma pasta e um arquivo (`ajuda/index.ts` × `Ajuda.tsx`, porque `import './ajuda'` fica ambíguo).
import { existsSync, readdirSync } from 'node:fs'
import { join, parse } from 'node:path'

const PASTAS = ['src', 'ajuda', 'e2e', 'scripts']
const conflitos = []

function varrer(pasta) {
    const vistos = new Map()
    for (const item of readdirSync(pasta, { withFileTypes: true })) {
        const caminho = join(pasta, item.name)
        // `.test.tsx` e `.tsx` do mesmo nome são arquivos diferentes de propósito: compara o nome sem a última extensão.
        const nome = item.isDirectory() ? item.name : parse(item.name).name
        const outro = vistos.get(nome.toLowerCase())
        if (outro !== undefined && outro.nome !== nome) conflitos.push(`${outro.caminho} × ${caminho}`)
        else vistos.set(nome.toLowerCase(), { nome, caminho })
        if (item.isDirectory()) varrer(caminho)
    }
}

for (const pasta of PASTAS) if (existsSync(pasta)) varrer(pasta)
if (conflitos.length > 0) {
    console.error('Nomes que só diferem em maiúsculas (quebram no Windows e no macOS):')
    for (const c of conflitos) console.error(`  ${c}`)
    process.exit(1)
}
console.log('Nomes ok')
