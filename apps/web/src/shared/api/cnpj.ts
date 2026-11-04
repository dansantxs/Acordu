import { formatarCep, formatarTelefone } from '../format/mascaras'

/**
 * Dados abertos da Receita pelo CNPJ na BrasilAPI, direto do navegador (RF-CLI-08; spec do F10, decisão 1a): sem rota
 * na nossa API; a CSP libera `https://brasilapi.com.br` em `connect-src` (segurança §9). Como no ViaCEP, vai sem cookie
 * e sem Referer, espera no máximo 4 s, e qualquer falha vira "preencha à mão" (convenções §4).
 */
export interface EnderecoDoCnpj {
    cep: string
    logradouro: string
    numero: string
    complemento: string
    bairro: string
    cidade: string
    uf: string
}

export interface EmpresaDoCnpj {
    /** Como a Receita manda: é o nome oficial. */
    razaoSocial: string
    /** Nome fantasia em forma de título; vazio quando a empresa não tem. */
    nomeFantasia: string
    /** `ATIVA`, `BAIXADA`, `INAPTA`, `SUSPENSA` ou `NULA`, como a Receita escreve. */
    situacao: string
    /** `yyyy-MM-dd`, quando vier. */
    situacaoDesde: string | null
    /** Nulo quando a Receita não traz o mínimo (CEP, logradouro, cidade e UF). */
    endereco: EnderecoDoCnpj | null
    telefone: string
    email: string
}

export type ResultadoCnpj = { tipo: 'achou'; empresa: EmpresaDoCnpj } | { tipo: 'nao-encontrado' } | { tipo: 'falhou' }

const ESPERA_MAXIMA_MS = 4_000

const MINUSCULAS = new Set(['de', 'da', 'do', 'das', 'dos', 'e'])
/** Siglas de tipo de empresa que ficam em maiúsculas ("Padaria Bom Grao ME"). */
const SIGLAS = new Set(['ME', 'EPP', 'EIRELI', 'MEI', 'SA', 'S/A', 'SS', 'S/S'])
const ROMANO = /^M{0,3}(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$/

/**
 * A Receita escreve em maiúsculas e sem acento ("AVENIDA PAULISTA", "SAO PAULO"). Forma de título, com as preposições
 * em minúsculas no meio ("Rua das Flores"); os acentos não voltam (spec do F10, registro de 04/11).
 */
export function formaDeTitulo(texto: string): string {
    const palavras = texto.trim().toUpperCase().split(/\s+/).filter(Boolean)
    return palavras
        .map((palavra, i) => {
            // Siglas e números romanos ("XV de Novembro") ficam como estão.
            if (SIGLAS.has(palavra) || (palavra.length > 1 && ROMANO.test(palavra))) return palavra
            const minuscula = palavra.toLowerCase()
            // "e" só é conjunção no meio: "Bloco E" no fim fica maiúsculo.
            if (i > 0 && i < palavras.length - 1 && MINUSCULAS.has(minuscula)) return minuscula
            // Hífen e apóstrofo começam outra palavra: "Embu-Guacu", "D'Oeste".
            return minuscula.replace(
                /(^|[-'’])(\p{L})/gu,
                (_, antes: string, letra: string) => antes + letra.toUpperCase(),
            )
        })
        .join(' ')
}

const texto = (valor: unknown): string => (typeof valor === 'string' ? valor.trim() : '')

/** `AbortSignal.any` falta em Safari < 17.4 e Firefox < 124; sem ele, a consulta falharia sempre, em silêncio. */
function juntar(sinais: AbortSignal[]): AbortSignal {
    if (typeof AbortSignal.any === 'function') return AbortSignal.any(sinais)
    const controle = new AbortController()
    for (const sinal of sinais) {
        if (sinal.aborted) {
            controle.abort(sinal.reason)
            break
        }
        sinal.addEventListener('abort', () => controle.abort(sinal.reason), { once: true })
    }
    return controle.signal
}

const UFS = new Set('AC AL AP AM BA CE DF ES GO MA MT MS MG PA PB PR PE PI RJ RN RS RO RR SC SP SE TO'.split(' '))

function endereco(dados: Record<string, unknown>): EnderecoDoCnpj | null {
    const cep = texto(dados.cep).replace(/\D/g, '')
    const tipo = formaDeTitulo(texto(dados.descricao_tipo_de_logradouro))
    const nome = formaDeTitulo(texto(dados.logradouro))
    // A Receita guarda o tipo à parte ("AVENIDA" + "PAULISTA 37"); às vezes o nome já começa com ele.
    const logradouro = tipo && !nome.toLowerCase().startsWith(`${tipo.toLowerCase()} `) ? `${tipo} ${nome}` : nome
    const cidade = formaDeTitulo(texto(dados.municipio))
    const uf = texto(dados.uf).toUpperCase()
    if (cep.length !== 8 || !nome || !cidade || !UFS.has(uf)) return null
    const numero = texto(dados.numero)
    return {
        cep: formatarCep(cep),
        logradouro,
        numero: /^S\/?N$/i.test(numero) ? '' : numero,
        complemento: formaDeTitulo(texto(dados.complemento)),
        bairro: formaDeTitulo(texto(dados.bairro)),
        cidade,
        uf,
    }
}

function empresa(dados: unknown): EmpresaDoCnpj | null {
    if (typeof dados !== 'object' || dados === null) return null
    const d = dados as Record<string, unknown>
    const razaoSocial = texto(d.razao_social)
    const situacao = texto(d.descricao_situacao_cadastral).toUpperCase()
    if (!razaoSocial || !situacao) return null
    const desde = texto(d.data_situacao_cadastral)
    return {
        razaoSocial,
        nomeFantasia: formaDeTitulo(texto(d.nome_fantasia)),
        situacao,
        situacaoDesde: /^\d{4}-\d{2}-\d{2}$/.test(desde) ? desde : null,
        endereco: endereco(d),
        telefone: formatarTelefone(texto(d.ddd_telefone_1).replace(/\D/g, '')),
        // E-mail fora do formato mínimo travaria o salvar (campo `type="email"`): melhor vazio.
        email: /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(texto(d.email)) ? texto(d.email).toLowerCase() : '',
    }
}

/** `cnpj` já validado (14 caracteres, sem pontuação, em maiúsculas: `cnpjCompleto`). CPF nunca chega aqui. */
export async function buscarCnpj(cnpj: string, sinal?: AbortSignal): Promise<ResultadoCnpj> {
    const tempo = AbortSignal.timeout(ESPERA_MAXIMA_MS)
    try {
        const resposta = await globalThis.fetch(`https://brasilapi.com.br/api/cnpj/v1/${encodeURIComponent(cnpj)}`, {
            signal: sinal ? juntar([sinal, tempo]) : tempo,
            // Sem cookie nem Referer: a BrasilAPI só fica sabendo do CNPJ.
            credentials: 'omit',
            referrerPolicy: 'no-referrer',
        })
        if (resposta.status === 404) return { tipo: 'nao-encontrado' }
        if (!resposta.ok) return { tipo: 'falhou' }
        const achada = empresa(await resposta.json())
        return achada ? { tipo: 'achou', empresa: achada } : { tipo: 'falhou' }
    } catch {
        return { tipo: 'falhou' }
    }
}
