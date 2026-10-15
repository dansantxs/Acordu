import js from '@eslint/js'
import reactHooks from 'eslint-plugin-react-hooks'
import globals from 'globals'
import tseslint from 'typescript-eslint'

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
        rules: { ...reactHooks.configs.recommended.rules },
    },
    {
        // O portal e o shared não importam nada do app interno (convenções §4); o que for comum vai para shared.
        files: ['src/portal/**/*.{ts,tsx}', 'src/shared/**/*.{ts,tsx}'],
        rules: {
            'no-restricted-imports': [
                'error',
                {
                    patterns: [
                        { group: ['@/features/*', '**/features/*'], message: 'Mova o que é comum para @/shared.' },
                    ],
                },
            ],
        },
    },
)
