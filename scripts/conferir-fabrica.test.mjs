// Garantias da fábrica pela ferramenta (docs/processo.md §8; spec do P1, P1-20 e P1-22).
// Rodar: node --test scripts/conferir-fabrica.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const raiz = fileURLToPath(new URL('..', import.meta.url));
const ler = (caminho) => readFileSync(raiz + caminho, 'utf8');

function cabecalho(agente) {
    const texto = ler(`.claude/agents/${agente}.md`);
    const bloco = texto.replace(/\r\n/g, '\n').match(/^---\n([\s\S]*?)\n---/)?.[1] ?? '';
    return Object.fromEntries(
        bloco
            .split('\n')
            .filter((l) => l.includes(':'))
            .map((l) => [l.slice(0, l.indexOf(':')).trim(), l.slice(l.indexOf(':') + 1).trim()]),
    );
}

test('P1-20: settings nega merge, force push, push na main, reset --hard, rebase e apagar branch', () => {
    const { deny, ask } = JSON.parse(ler('.claude/settings.json')).permissions;
    for (const regra of [
        'Bash(gh pr merge:*)',
        'Bash(git push --force:*)',
        'Bash(git push -f:*)',
        'Bash(git push --force-with-lease:*)',
        'Bash(git push origin main:*)',
        'Bash(git reset --hard:*)',
        'Bash(git rebase:*)',
        'Bash(git branch -D:*)',
        'Bash(git push origin HEAD:main:*)',
        'Bash(git push --delete:*)',
        'Bash(git push origin --delete:*)',
        'Bash(gh api -X PUT repos/*/pulls/*/merge:*)',
    ]) {
        assert.ok(deny.includes(regra), `falta negar ${regra}`);
    }
    for (const regra of ['Bash(gh workflow run deploy.yml:*)', 'Bash(gh workflow run backup.yml:*)']) {
        assert.ok(ask.includes(regra), `falta pedir confirmação em ${regra}`);
    }
});

test('P1-22: testador e revisor usam outro modelo; os demais herdam o da sessão', () => {
    assert.equal(cabecalho('testador').model, 'sonnet');
    assert.equal(cabecalho('revisor-spec').model, 'sonnet');
    for (const agente of ['analista', 'arquiteto', 'desenvolvedor']) {
        assert.equal(cabecalho(agente).model, 'inherit', agente);
    }
});

test('P1-21: revisor sem ferramenta de edição', () => {
    assert.equal(cabecalho('revisor-spec').tools, 'Read, Grep, Glob, Bash');
});

test('todo subagente termina com o bloco de status do processo §5', () => {
    for (const agente of ['analista', 'arquiteto', 'desenvolvedor', 'testador', 'revisor-spec']) {
        assert.match(ler(`.claude/agents/${agente}.md`), /processo §5/, agente);
    }
});
