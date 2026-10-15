import { api } from '../api/cliente'
import { abrirSessao, encerrarSessao } from './sessao'

/** Ações que abrem, trocam ou encerram a sessão. Erros chegam como `ErroApi` (campos em `campos`). */
export async function entrar(email: string, senha: string): Promise<void> {
    const { data } = await api.POST('/api/v1/auth/login', { body: { email, senha } })
    abrirSessao(data!)
}

export async function cadastrar(corpo: { nome: string; email: string; senha: string; nomeEmpresa: string }) {
    const { data } = await api.POST('/api/v1/auth/registro', { body: corpo })
    abrirSessao(data!)
}

/** Troca a empresa ativa: o token novo tem outro tenant, então o cache inteiro deixa de valer (em todas as abas). */
export async function trocarEmpresa(tenantId: string): Promise<void> {
    const { data } = await api.POST('/api/v1/auth/tenant-ativo', { body: { tenantId } })
    abrirSessao(data!)
}

/** Sai mesmo se a API não responder: o token some da memória e as outras abas saem junto. */
export async function sair(): Promise<void> {
    try {
        await api.POST('/api/v1/auth/logout')
    } finally {
        encerrarSessao()
    }
}

/** Destino depois de entrar: só caminhos internos (evita redirecionamento aberto por `?voltar=`). */
export function destinoSeguro(voltar: unknown): string {
    return typeof voltar === 'string' && voltar.startsWith('/') && !voltar.startsWith('//') && !voltar.startsWith('/\\')
        ? voltar
        : '/'
}
