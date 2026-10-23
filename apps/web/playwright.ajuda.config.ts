import { defineConfig, devices } from '@playwright/test'

/**
 * Bot das capturas do manual (M2): mesmo SPA compilado e mesma API da fumaça, mas só `ajuda/*.captura.ts`, sem
 * repetição (uma falha precisa aparecer) e com as fotos gravadas em `public/ajuda/`. Roda no workflow Ajuda; localmente,
 * suba a API, o Postgres e o Mailpit como no README e rode `pnpm ajuda:capturas`.
 */
export default defineConfig({
    testDir: './ajuda',
    testMatch: '**/*.captura.ts',
    workers: 1,
    retries: 0,
    reporter: process.env.CI ? [['github'], ['list']] : 'list',
    use: {
        baseURL: 'http://localhost:5173',
        locale: 'pt-BR',
        timezoneId: 'America/Sao_Paulo',
        trace: 'retain-on-failure',
    },
    projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
    webServer: {
        command: 'pnpm preview --port 5173 --strictPort',
        url: 'http://localhost:5173',
        reuseExistingServer: !process.env.CI,
        timeout: 60_000,
    },
})
