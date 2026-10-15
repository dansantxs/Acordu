import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useEffect } from 'react'
import { api } from '@/shared/api/cliente'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { consultaPerfil } from '@/shared/sessao/perfil'
import { descartarTokenDeLink } from '@/shared/sessao/tokenDaUrl'
import { useSessao } from '@/shared/sessao/useSessao'
import { Alerta } from '@/shared/ui/Alerta'
import { linkRecusado } from './linkInvalido'

/**
 * Confirma ao abrir. É consulta (e não mutação) de propósito: o TanStack Query deduplica a chamada, então o
 * StrictMode, que monta duas vezes, não gasta o token na primeira e recebe 401 na segunda.
 */
export function ConfirmacaoEmail({ token }: { token: string | null }) {
    const consultas = useQueryClient()
    const { situacao } = useSessao()
    const confirmacao = useQuery({
        queryKey: ['conta', 'confirmacao', token],
        queryFn: async () => {
            await api.POST('/api/v1/auth/confirmacao-email', { body: { token: token ?? '' } })
            return true
        },
        enabled: token !== null,
        retry: false,
        staleTime: Infinity,
        gcTime: Infinity,
    })

    useEffect(() => {
        if (!confirmacao.isSuccess) return
        descartarTokenDeLink('confirmacao')
        void consultas.invalidateQueries({ queryKey: consultaPerfil.queryKey })
    }, [confirmacao.isSuccess, consultas])

    const seguir =
        situacao === 'ativa'
            ? { to: '/' as const, texto: 'Ir para o Acordu' }
            : { to: '/entrar' as const, texto: 'Entrar' }

    if (token !== null && confirmacao.isPending) return <p className="text-suave">Confirmando…</p>

    return (
        <div className="flex flex-col gap-4">
            {confirmacao.isSuccess ? (
                <Alerta tom="sucesso">E-mail confirmado. Obrigado!</Alerta>
            ) : token === null || linkRecusado(confirmacao.error) ? (
                <Alerta>
                    Este link de confirmação não vale mais. Ele expira em 48 horas; dentro do Acordu dá para pedir
                    outro.
                </Alerta>
            ) : (
                <Alerta>{mensagemDeErro(confirmacao.error)}</Alerta>
            )}
            <Link to={seguir.to} className="font-semibold text-acento-texto underline-offset-4 hover:underline">
                {seguir.texto}
            </Link>
        </div>
    )
}
