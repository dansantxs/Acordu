/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// A API roda em http://localhost:5080 no desenvolvimento; em produção o Caddy serve /api no mesmo host (deploy §2.1).
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
    },
})
