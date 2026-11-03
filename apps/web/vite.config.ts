/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// A API roda em http://localhost:5080 no desenvolvimento (proxy de /api); em produção fica em api.acordu.app.br, pela
// variável VITE_URL_API do build (ADR 0010, deploy-ci §4).
export default defineConfig({
    plugins: [tanstackRouter({ target: 'react', autoCodeSplitting: true }), react(), tailwindcss()],
    resolve: {
        alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
    },
    server: {
        port: 5173,
        proxy: { '/api': { target: 'http://localhost:5080', changeOrigin: false } },
    },
    test: {
        environment: 'jsdom',
        globals: true,
        setupFiles: ['./src/testes/preparar.ts'],
        css: false,
        // Acima do tempo de espera do Testing Library (5 s, testes/preparar.ts), para a falha mostrar o que faltou.
        testTimeout: 15_000,
    },
})
