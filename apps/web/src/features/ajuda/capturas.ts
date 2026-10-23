/**
 * Capturas de tela do manual (marco do manual, M2). As imagens não são tiradas à mão: o workflow Ajuda sobe a API com
 * uma empresa de demonstração fictícia, abre cada tela com o Playwright e grava `public/ajuda/<nome>.webp`
 * (`apps/web/ajuda/capturas.captura.ts`). Nome novo aqui exige a receita lá (o TypeScript cobra) e um pedido ao bot
 * (`.github/ajuda-request`) no mesmo PR; um teste confere que todo nome tem o arquivo.
 */
export const CAPTURAS = [
    'hoje',
    'agenda',
    'agenda-plano',
    'solicitacoes',
    'solicitacao',
    'tarefas-quadro',
    'horas',
    'cliente',
    'contrato',
    'projetos',
    'recorrencias',
    'panorama',
    'configuracoes',
    // Portal do cliente (M3), com o contato da padaria.
    'portal-lista',
    'portal-detalhe',
    'portal-nova',
    'portal-contratos',
] as const

export type CapturaAjuda = (typeof CAPTURAS)[number]

/** Tamanho em que as capturas são tiradas (CSS px); a tela reserva a proporção antes de a imagem chegar. */
export const TAMANHO_CAPTURA = { largura: 1440, altura: 900 } as const

export const caminhoDaCaptura = (nome: CapturaAjuda) => `/ajuda/${nome}.webp`
