// Testes de scripts/conferir-specs.mjs (spec do P1, P1-06 a P1-08). Rodar: node --test scripts/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { conferirSpecs } from './conferir-specs.mjs';

function spec(status, ...linhas) {
    return [
        '| | |',
        '|---|---|',
        `| **Status** | ${status} |`,
        '',
        '| Id | Dado | Quando | Então | Teste |',
        '|---|---|---|---|---|',
        ...linhas,
    ].join('\n');
}

function raizCom(pastas) {
    const raiz = mkdtempSync(join(tmpdir(), 'specs-'));
    for (const [pasta, arquivos] of Object.entries(pastas)) {
        mkdirSync(join(raiz, pasta));
        for (const [nome, conteudo] of Object.entries(arquivos)) writeFileSync(join(raiz, pasta, nome), conteudo);
    }
    return raiz;
}

test('marco novo completo passa', () => {
    const raiz = raizCom({
        'F14-algo': { 'spec.md': spec('Concluída', '| F14-01 | a | b | c | X › Y |'), 'design.md': '' },
        _template: {},
    });
    assert.deepEqual(conferirSpecs(raiz), []);
});

test('P1-06: marco novo sem design.md falha com a pasta', () => {
    const raiz = raizCom({ 'P2-coisa': { 'spec.md': spec('Rascunho') } });
    assert.deepEqual(conferirSpecs(raiz), ['P2-coisa: falta design.md (fora do formato legado F8–F13, spec e design são separados)']);
});

test('P1-06: marco legado sem design.md passa', () => {
    const raiz = raizCom({ 'F12-x': { 'spec.md': spec('Concluída', '| F12-01 | a | b | c | T |') } });
    assert.deepEqual(conferirSpecs(raiz), []);
});

test('P1-07: id repetido falha', () => {
    const raiz = raizCom({
        'F14-a': { 'spec.md': spec('Rascunho', '| F14-01 | a | b | c | — |', '| F14-01 | d | e | f | — |'), 'design.md': '' },
    });
    assert.deepEqual(conferirSpecs(raiz), ['F14-a: cenário F14-01 repetido']);
});

test('P1-07: id de outro marco ou sem dois dígitos falha', () => {
    const raiz = raizCom({
        'F14-a': { 'spec.md': spec('Rascunho', '| F13-01 | a | b | c | — |', '| F14-1 | a | b | c | — |'), 'design.md': '' },
    });
    assert.deepEqual(conferirSpecs(raiz), [
        'F14-a: cenário F13-01 fora do padrão F14-NN',
        'F14-a: cenário F14-1 fora do padrão F14-NN',
    ]);
});

test('P1-07: id com letra, minúscula, travessão ou sublinhado falha', () => {
    const raiz = raizCom({
        'F14-a': {
            'spec.md': spec('Rascunho', '| F14-01a | a | b | c | — |', '| f14-02 | a | b | c | — |', '| F14–03 | a | b | c | — |', '| F14_04 | a | b | c | — |'),
            'design.md': '',
        },
    });
    assert.deepEqual(conferirSpecs(raiz), [
        'F14-a: cenário F14-01a fora do padrão F14-NN',
        'F14-a: cenário f14-02 fora do padrão F14-NN',
        'F14-a: cenário F14–03 fora do padrão F14-NN',
        'F14-a: cenário F14_04 fora do padrão F14-NN',
    ]);
});

test('tabela sem coluna Id não conta como cenário', () => {
    const raiz = raizCom({
        'F14-a': { 'spec.md': spec('Concluída', '| F14-01 | a | b | c | T |') + '\n\n| PR | Entrega |\n|---|---|\n| F14a | x |', 'design.md': '' },
    });
    assert.deepEqual(conferirSpecs(raiz), []);
});

test('P1-08: spec Concluída com cenário sem Teste falha', () => {
    const raiz = raizCom({
        'F14-a': { 'spec.md': spec('Concluída', '| F14-01 | a | b | c | T |', '| F14-02 | a | b | c | — |'), 'design.md': '' },
    });
    assert.deepEqual(conferirSpecs(raiz), ['F14-a: spec Concluída e o cenário F14-02 sem Teste']);
});

test('P1-08: spec em implementação com cenário sem Teste passa', () => {
    const raiz = raizCom({
        'F14-a': { 'spec.md': spec('Em implementação', '| F14-01 | a | b | c | — |'), 'design.md': '' },
    });
    assert.deepEqual(conferirSpecs(raiz), []);
});
