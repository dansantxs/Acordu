import { digitos } from '../format/mascaras'

/**
 * Endereço pelo CEP no ViaCEP, direto do navegador (decidido em 16/10, opção a): sem rota na nossa API; a CSP libera
 * `https://viacep.com.br` em `connect-src` (segurança §9). Fora do ar, lento ou CEP inexistente → `null`, e a pessoa
 * digita o endereço.
 */
export interface EnderecoDoCep {
    logradouro: string
    bairro: string
    cidade: string
    uf: string
}

const ESPERA_MAXIMA_MS = 4_000

export async function buscarCep(cep: string, sinal?: AbortSignal): Promise<EnderecoDoCep | null> {
    const numeros = digitos(cep)
    if (numeros.length !== 8) return null
    const tempo = AbortSignal.timeout(ESPERA_MAXIMA_MS)
    try {
        const resposta = await globalThis.fetch(`https://viacep.com.br/ws/${numeros}/json/`, {
            signal: sinal ? AbortSignal.any([sinal, tempo]) : tempo,
            // Sem cookie nem Referer: o ViaCEP só fica sabendo do CEP.
            credentials: 'omit',
            referrerPolicy: 'no-referrer',
        })
        if (!resposta.ok) return null
        const dados = (await resposta.json()) as {
            erro?: boolean | string
            logradouro?: string
            bairro?: string
            localidade?: string
            uf?: string
        }
        if (dados.erro || !dados.localidade || !dados.uf) return null
        return {
            logradouro: dados.logradouro ?? '',
            bairro: dados.bairro ?? '',
            cidade: dados.localidade,
            uf: dados.uf,
        }
    } catch {
        return null
    }
}
