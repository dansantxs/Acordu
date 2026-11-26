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

test('F14-121: o deploy normaliza TEXTOS_LEGAIS_PUBLICADOS uma vez e leva o mesmo valor à API e ao build do SPA', () => {
    const deploy = ler('.github/workflows/deploy.yml').replace(/\r\n/g, '\n');
    // O passo que normaliza tem id, e o valor sai dele para a API (arquivo de variáveis) e para o passo do SPA.
    assert.match(deploy, /- name: Variáveis da API\n\s+id: variaveis\n/);
    assert.match(deploy, /textos = os\.environ\.get\("TEXTOS_LEGAIS_PUBLICADOS", ""\)\.strip\(\)\.lower\(\) or "false"/);
    assert.match(deploy, /if textos not in \("true", "false"\):\n\s+faltam\.append\("TEXTOS_LEGAIS_PUBLICADOS/);
    assert.match(deploy, /variaveis\["TextosLegais__Publicados"\] = textos\n/);
    assert.match(deploy, /saida\.write\("textos_legais_publicados=" \+ textos \+ "[\\]n"\)/);
    // A normalização e a conferência vêm antes de qualquer publicação (o passo que para o deploy é o das variáveis).
    assert.ok(deploy.indexOf('TEXTOS_LEGAIS_PUBLICADOS (variável inválida') < deploy.indexOf('- name: SPA (Pages)'));
    // O SPA lê a saída do passo, nunca a variável do repositório direto (não pode divergir da API).
    const linhas = deploy.split('\n').filter((l) => l.includes('VITE_TEXTOS_LEGAIS_PUBLICADOS:'));
    assert.equal(linhas.length, 1);
    assert.match(linhas[0], /\$\{\{ steps\.variaveis\.outputs\.textos_legais_publicados \}\}/);
    assert.doesNotMatch(linhas[0], /vars\./);
    // O passo do SPA vem depois do passo que escreve a saída.
    assert.ok(deploy.indexOf('- name: Variáveis da API') < deploy.indexOf('- name: SPA (build)'));
});

test('F14n (design do F14, D11): o deploy exige CADASTRO_MODO, convite ou aberto, antes de publicar, e o leva à API', () => {
    const deploy = ler('.github/workflows/deploy.yml').replace(/\r\n/g, '\n');
    assert.match(deploy, /CADASTRO_MODO: \$\{\{ vars\.CADASTRO_MODO \}\}/);
    assert.match(deploy, /modo_cadastro = os\.environ\.get\("CADASTRO_MODO", ""\)\.strip\(\)\.lower\(\)\n/);
    // Sem a variável o modo fica vazio e cai na recusa: nenhum padrão abre ou fecha o cadastro em silêncio.
    assert.match(deploy, /if modo_cadastro not in \("convite", "aberto"\):\n\s+faltam\.append\("CADASTRO_MODO/);
    assert.match(deploy, /variaveis\["Cadastro__Modo"\] = modo_cadastro\n/);
    assert.ok(deploy.indexOf('CADASTRO_MODO (variável obrigatória') < deploy.indexOf('- name: Nova revisão'));
});

test('F15b (design do F15, D2): o deploy exige EMAIL_ENTRADA_DOMINIO, um nome de domínio, antes de publicar, e o leva à API', () => {
    const deploy = ler('.github/workflows/deploy.yml').replace(/\r\n/g, '\n');
    assert.match(deploy, /EMAIL_ENTRADA_DOMINIO: \$\{\{ vars\.EMAIL_ENTRADA_DOMINIO \}\}/);
    assert.match(deploy, /dominio_entrada = os\.environ\.get\("EMAIL_ENTRADA_DOMINIO", ""\)\.strip\(\)\.lower\(\)\n/);
    // Sem a variável o domínio fica vazio, não casa o formato e cai na recusa.
    assert.match(deploy, /if not re\.fullmatch\(.*dominio_entrada\).*:\n\s+faltam\.append\("EMAIL_ENTRADA_DOMINIO/);
    assert.match(deploy, /variaveis\["EmailEntrada__Dominio"\] = dominio_entrada\n/);
    assert.ok(deploy.indexOf('EMAIL_ENTRADA_DOMINIO (variável obrigatória') < deploy.indexOf('- name: Nova revisão'));
});
