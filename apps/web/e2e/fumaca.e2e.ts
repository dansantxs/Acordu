import { expect, test, type Page } from '@playwright/test'

// Cada execução cria a própria conta: o banco da CI é novo, mas localmente o teste pode rodar várias vezes.
const sufixo = `${Date.now()}`
const conta = {
    nome: 'Ana Fumaça',
    email: `ana.${sufixo}@exemplo.com`,
    senha: 'senha-de-teste-1',
    empresa: `Estúdio ${sufixo}`,
}

const saudacao = (pagina: Page) => pagina.getByRole('heading', { level: 1, name: /, Ana$/ })

test.describe.configure({ mode: 'serial' })

test('cria a conta, mantém a sessão, navega e sai', async ({ page }) => {
    await page.goto('/cadastro')
    await page.getByLabel('Seu nome').fill(conta.nome)
    await page.getByLabel('E-mail').fill(conta.email)
    await page.getByLabel('Senha', { exact: true }).fill(conta.senha)
    await page.getByLabel('Nome da empresa').fill(conta.empresa)
    // O tempo real abre assim que a casca aparece: confere que o SSE passa pelo proxy com o Bearer.
    const eventos = page.waitForResponse((r) => r.url().endsWith('/api/v1/eventos') && r.status() === 200)
    await page.getByRole('button', { name: 'Criar conta' }).click()

    await expect(saudacao(page)).toBeVisible()
    // Conta nova: a empresa ainda não envia a terceiros, e a casca explica (RF-AUT-06, F9-42).
    await expect(
        page.getByText(
            `Confirme seu e-mail para liberar convites, links do portal e avisos por e-mail aos clientes. Mandamos o link para ${conta.email}.`,
        ),
    ).toBeVisible()
    await eventos

    // Recarregar recupera a sessão pelo cookie HttpOnly (ADR 0007).
    await page.reload()
    await expect(saudacao(page)).toBeVisible()

    // Ctrl+K leva a uma tela pelo nome.
    await page.keyboard.press('Control+k')
    await page.getByRole('searchbox', { name: 'Buscar' }).fill('clien')
    await page.getByRole('menuitem', { name: 'Clientes' }).click()
    await expect(page).toHaveURL(/\/clientes$/)
    await expect(page.getByRole('heading', { level: 1, name: 'Clientes' })).toBeVisible()

    await expect(page.getByRole('button', { name: /^Avisos/ })).toBeVisible()

    await page.getByRole('button', { name: new RegExp(conta.nome) }).click()
    await page.getByRole('menuitem', { name: 'Sair' }).click()
    await expect(page.getByRole('heading', { name: 'Entrar' })).toBeVisible()

    // Sem sessão, o app manda para o login e volta para onde se queria ir.
    await page.goto('/clientes')
    await expect(page).toHaveURL(/\/entrar\?voltar=%2Fclientes/)
})

test('recusa a senha errada e entra com a certa', async ({ page }) => {
    await page.goto('/entrar?voltar=%2Fclientes')
    await page.getByLabel('E-mail').fill(conta.email)
    await page.getByLabel('Senha', { exact: true }).fill('senha-errada-123')
    await page.getByRole('button', { name: 'Entrar' }).click()
    await expect(page.getByRole('alert')).toContainText('E-mail ou senha inválidos')

    await page.getByLabel('Senha', { exact: true }).fill(conta.senha)
    await page.getByRole('button', { name: 'Entrar' }).click()
    await expect(page).toHaveURL(/\/clientes$/)
})

test('duas abas renovando ao mesmo tempo não derrubam a sessão', async ({ page, context }) => {
    await page.goto('/entrar')
    await page.getByLabel('E-mail').fill(conta.email)
    await page.getByLabel('Senha', { exact: true }).fill(conta.senha)
    await page.getByRole('button', { name: 'Entrar' }).click()
    await expect(saudacao(page)).toBeVisible()

    const outra = await context.newPage()
    await outra.goto('/')
    await expect(saudacao(outra)).toBeVisible()

    // As duas recarregam juntas: sem a serialização entre abas, a segunda apresentaria o refresh já consumido e a
    // API revogaria todas as sessões (reuso).
    await Promise.all([page.reload(), outra.reload()])
    await expect(saudacao(page)).toBeVisible()
    await expect(saudacao(outra)).toBeVisible()

    await page.reload()
    await expect(saudacao(page)).toBeVisible()
})
