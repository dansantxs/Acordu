import { api } from '../api/cliente'
import { abrirSessao, encerrarSessao, estadoSessao, type TokenDeAcesso } from './sessao'

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

/** Aceite do convite da equipe: conta nova manda `nome`; conta existente, só a senha dela. */
export async function aceitarConvite(corpo: { token: string; nome: string | null; senha: string }): Promise<void> {
    const { data } = await api.POST('/api/v1/auth/convite', { body: corpo })
    abrirSessao(data as unknown as TokenDeAcesso)
}

/** Aceite do convite do portal (contrato §2): mesmo corpo e cookie do login; o contato cai no portal. */
export async function aceitarConvitePortal(corpo: {
    token: string
    nome: string | null
    senha: string
}): Promise<void> {
    const { data } = await api.POST('/api/v1/auth/portal/convite', { body: corpo })
    abrirSessao(data as unknown as TokenDeAcesso)
}

/**
 * Nova senha pelo link do e-mail. A API revoga todas as sessões da pessoa e apaga o cookie, então aqui a sessão
 * (se houver) também acaba, em todas as abas.
 */
export async function redefinirSenha(token: string, novaSenha: string): Promise<void> {
    await api.POST('/api/v1/auth/senha/redefinir', { body: { token, novaSenha } })
    if (estadoSessao().situacao === 'ativa') encerrarSessao()
}

/** Destino depois de entrar: só caminhos internos (evita redirecionamento aberto por `?voltar=`). */
export function destinoSeguro(voltar: unknown): string {
    return typeof voltar === 'string' && voltar.startsWith('/') && !voltar.startsWith('//') && !voltar.startsWith('/\\')
        ? voltar
        : '/'
}
