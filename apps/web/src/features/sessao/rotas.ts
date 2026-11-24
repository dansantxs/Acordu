import { redirect } from '@tanstack/react-router'
import { guardarTokenDeLink, type TipoDeLink } from '@/shared/sessao/tokenDaUrl'
import { destinoSeguro } from '@/shared/sessao/acoes'
import { aguardarSessao } from '@/shared/sessao/sessao'

/** `?voltar=` das telas de acesso: só caminho interno. */
export function validarVoltar(busca: Record<string, unknown>): { voltar?: string } {
    return typeof busca.voltar === 'string' ? { voltar: destinoSeguro(busca.voltar) } : {}
}

/** Quem já tem sessão não precisa ver o login nem o cadastro. */
export async function seguirSeJaEntrou(voltar?: string): Promise<void> {
    const estado = await aguardarSessao()
    if (estado.situacao === 'ativa') throw redirect({ to: destinoSeguro(voltar) })
}

/** Guarda o token do link e troca a URL pela versão sem ele, sem deixar a com token no histórico. */
export function retirarTokenDaUrl(
    tipo: TipoDeLink,
    token: string,
    destino: '/convite' | '/portal/convite' | '/cadastro/convite' | '/conta/senha/nova' | '/conta/confirmacao',
) {
    guardarTokenDeLink(tipo, token)
    return redirect({ to: destino, replace: true })
}
