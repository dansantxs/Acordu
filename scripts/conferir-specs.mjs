// Confere a estrutura das specs dos marcos (docs/processo.md; spec do P1, P1-06 a P1-08).
// Uso: node scripts/conferir-specs.mjs [pasta das specs]. Sai 1 e lista os problemas se houver algum.
import { readdirSync, readFileSync, existsSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

// Marcos no formato antigo (spec.md com tudo, ADR 0011): sem design.md.
const LEGADOS = new Set(['F8', 'F9', 'F10', 'F11', 'F12', 'F13']);

function celulas(linha) {
    return linha.split('|').slice(1, -1).map((c) => c.trim());
}

export function conferirSpecs(raiz) {
    const problemas = [];
    for (const pasta of readdirSync(raiz).sort()) {
        const caminho = join(raiz, pasta);
        if (pasta.startsWith('_') || !statSync(caminho).isDirectory()) continue;
        const marco = pasta.split('-')[0];
        const spec = join(caminho, 'spec.md');
        if (!existsSync(spec)) {
            problemas.push(`${pasta}: falta spec.md`);
            continue;
        }
        if (!LEGADOS.has(marco) && !existsSync(join(caminho, 'design.md'))) {
            problemas.push(`${pasta}: falta design.md (fora do formato legado F8–F13, spec e design são separados)`);
        }
        const linhas = readFileSync(spec, 'utf8').split(/\r?\n/);
        const status = linhas.map(celulas).find((c) => c[0] === '**Status**')?.[1] ?? '';
        const concluida = status.startsWith('Concluída');
        const vistos = new Set();
        let naTabela = false;
        for (const linha of linhas) {
            if (!linha.startsWith('|')) {
                naTabela = false;
                continue;
            }
            const c = celulas(linha);
            // Cenário é toda linha de uma tabela cujo cabeçalho começa por "Id" (template da spec).
            if (c[0] === 'Id') {
                naTabela = true;
                continue;
            }
            if (!naTabela || /^:?-+:?$/.test(c[0] ?? '')) continue;
            const id = c[0];
            if (!new RegExp(`^${marco}-\\d{2,}$`).test(id)) {
                problemas.push(`${pasta}: cenário ${id} fora do padrão ${marco}-NN`);
            }
            if (vistos.has(id)) problemas.push(`${pasta}: cenário ${id} repetido`);
            vistos.add(id);
            const teste = c[c.length - 1];
            if (concluida && (teste === '' || teste === '—' || teste === '-')) {
                problemas.push(`${pasta}: spec Concluída e o cenário ${id} sem Teste`);
            }
        }
    }
    return problemas;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    const raiz = process.argv[2] ?? join(fileURLToPath(new URL('..', import.meta.url)), 'docs', 'specs');
    const problemas = conferirSpecs(raiz);
    for (const p of problemas) console.log(`::error title=Specs::${p}`);
    if (problemas.length > 0) process.exit(1);
    console.log('Specs ok');
}
