import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'

export type SugestaoCriada = components['schemas']['SugestaoCriadaDto']

export const TEXTO_MINIMO = 10
export const TEXTO_MAXIMO = 2000

/** "Sugerir melhoria" (contrato §16, F13e): vai para a equipe do Acordu, fora dos dados da empresa. */
export async function enviarSugestao(texto: string, tela: string): Promise<SugestaoCriada> {
    return (await api.POST('/api/v1/sugestoes', { body: { texto, tela } })).data!
}
