import js from '@eslint/js'
import reactHooks from 'eslint-plugin-react-hooks'
import globals from 'globals'
import tseslint from 'typescript-eslint'

const SEM_FEATURES = { group: ['@/features/*', '**/features/*'], message: 'Mova o que é comum para @/shared.' }
const SEM_NEBRYS = {
    group: ['@/nebrys/*', '**/nebrys/*'],
    message: 'A área da Nebrys não entra no app da empresa nem no portal.',
}

export default tseslint.config(
    {
        ignores: [
            'dist',
            'node_modules',
            'playwright-report',
            'test-results',
            'src/routeTree.gen.ts',
            'src/shared/api/*.d.ts',
        ],
    },
    {
        files: ['**/*.{ts,tsx}'],
        extends: [js.configs.recommended, ...tseslint.configs.recommended],
        languageOptions: { ecmaVersion: 2023, globals: globals.browser },
        plugins: { 'react-hooks': reactHooks },
        rules: {
            ...reactHooks.configs.recommended.rules,
            // Tirar um campo de um objeto com o resto (`const { nova, ...filtros } = busca`) não é variável esquecida.
            '@typescript-eslint/no-unused-vars': ['error', { ignoreRestSiblings: true }],
        },
    },
    {
        // O app da equipe e o portal não importam nada da área da Nebrys (ADR 0014; convenções §4): nenhum link, nenhum
        // código dela nas telas da empresa.
        files: ['src/features/**/*.{ts,tsx}', 'src/routes/**/*.{ts,tsx}'],
        ignores: ['src/routes/nebrys/**'],
        rules: {
            'no-restricted-imports': ['error', { patterns: [SEM_NEBRYS] }],
        },
    },
    {
        // O portal e o shared não importam nada do app interno (convenções §4); o que for comum vai para shared.
        files: ['src/portal/**/*.{ts,tsx}', 'src/shared/**/*.{ts,tsx}'],
        rules: {
            'no-restricted-imports': ['error', { patterns: [SEM_FEATURES, SEM_NEBRYS] }],
        },
    },
    {
        // A área da Nebrys é separada do app da empresa e do portal: só usa o shared (ADR 0014; convenções §4).
        files: ['src/nebrys/**/*.{ts,tsx}', 'src/routes/nebrys/**/*.{ts,tsx}'],
        rules: {
            'no-restricted-imports': [
                'error',
                {
                    patterns: [
                        {
                            group: ['@/features/*', '**/features/*', '@/portal/*', '**/portal/*'],
                            message: 'A área da Nebrys só usa o @/shared.',
                        },
                    ],
                },
            ],
        },
    },
)
