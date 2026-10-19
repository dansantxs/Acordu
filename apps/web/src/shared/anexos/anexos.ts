import { api } from '../api/cliente'

/** Limites da API (contrato §7): até 5 arquivos de 20 MB por envio; o tipo é conferido pelo conteúdo no servidor. */
export const MAXIMO_ANEXOS = 5
export const TAMANHO_MAXIMO = 20 * 1024 * 1024

export function tamanhoLegivel(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1).replace('.', ',')} MB`
}

/**
 * Baixa um anexo da equipe: a API devolve a URL assinada (5 min, `attachment`) e o navegador a abre sem sair da página.
 * O token não vai para o bucket.
 */
export async function baixarAnexo(id: string): Promise<void> {
    const { url } = (await api.GET('/api/v1/anexos/{id}/download', { params: { path: { id } } })).data!
    globalThis.location.assign(url)
}
