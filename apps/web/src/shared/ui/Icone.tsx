/** Ícones de traço, desenhados aqui (sem biblioteca). Decorativos: o texto ao lado ou o `aria-label` do botão dá o nome. */
const caminhos = {
    sino: 'M6 8a6 6 0 1 1 12 0c0 7 3 9 3 9H3s3-2 3-9M10.3 21a1.94 1.94 0 0 0 3.4 0',
    busca: 'M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16zm10 2-4.35-4.35',
    menu: 'M4 6h16M4 12h16M4 18h16',
    fechar: 'M18 6 6 18M6 6l12 12',
    mais: 'M12 5v14M5 12h14',
    seta: 'M5 12h14m-6-6 6 6-6 6',
    clipe: 'm21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48',
} as const

export type NomeIcone = keyof typeof caminhos

export function Icone({ nome, className = 'size-4' }: { nome: NomeIcone; className?: string }) {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth={1.8}
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
            className={className}
        >
            <path d={caminhos[nome]} />
        </svg>
    )
}
