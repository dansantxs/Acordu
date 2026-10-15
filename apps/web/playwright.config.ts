import { defineConfig, devices } from '@playwright/test'

/**
 * Fumaça de ponta a ponta (F1c3): o SPA compilado (`vite preview`, com `/api` encaminhado para a API em
 * http://localhost:5080) contra a API real com Postgres. Na CI, o job `E2E` sobe o banco e a API antes; localmente,
 * suba os dois como no README e rode `pnpm e2e`.
 */
export default defineConfig({
    testDir: './e2e',
    testMatch: '**/*.e2e.ts',
    fullyParallel: false,
    workers: 1,
    retries: process.env.CI ? 1 : 0,
    reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
    use: {
        baseURL: 'http://localhost:5173',
        locale: 'pt-BR',
        timezoneId: 'America/Sao_Paulo',
        trace: 'retain-on-failure',
        screenshot: 'only-on-failure',
    },
    projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
    webServer: {
        command: 'pnpm preview --port 5173 --strictPort',
        url: 'http://localhost:5173',
        reuseExistingServer: !process.env.CI,
        timeout: 60_000,
    },
})
