import { useMutation } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import { mensagemDeErro } from '@/shared/api/mensagens'
import type { Perfil } from '@/shared/sessao/perfil'
import { Botao } from '@/shared/ui/Botao'

/** Explicação ao lado dos botões de convite e de link enquanto a empresa não envia a terceiros (RF-AUT-06, F9e). */
export const SEM_ENVIO = 'Disponível depois que você confirmar seu e-mail.'

/**
 * Lembrete de confirmar o e-mail da conta, na tela Hoje (o login não exige confirmação, contrato §2). Com a empresa
 * ainda não liberada quem fala é o {@link AvisoEnvioBloqueado}, na casca.
 */
export function AvisoConfirmacao({ perfil }: { perfil: Perfil }) {
    if (perfil.emailConfirmado || !perfil.tenant.envioLiberado) return null
    return <Faixa perfil={perfil} texto={`Confirme seu e-mail: mandamos um link para ${perfil.email}.`} />
}

/**
 * Em toda tela interna enquanto a empresa não foi liberada (RF-AUT-06; spec do F9, F9-42): o que fica parado e o
 * reenvio do link. Depende da empresa, não da conta (F9-26).
 */
export function AvisoEnvioBloqueado({ perfil }: { perfil: Perfil }) {
    if (perfil.tenant.envioLiberado) return null
    return (
        <Faixa
            perfil={perfil}
            texto={`Confirme seu e-mail para liberar convites, links do portal e avisos por e-mail aos clientes. Mandamos o link para ${perfil.email}.`}
        />
    )
}

function Faixa({ perfil, texto }: { perfil: Perfil; texto: string }) {
    const reenvio = useMutation({ mutationFn: () => api.POST('/api/v1/auth/confirmacao-email/reenvio') })
    return (
        <div
            role="status"
            className="flex flex-wrap items-center justify-between gap-3 rounded-couro border border-ambar/40 bg-ambar-suave px-4 py-3 text-sm text-ambar-texto"
        >
            <span>
                {reenvio.isSuccess
                    ? `Enviamos um link novo para ${perfil.email}. O anterior deixou de valer.`
                    : reenvio.isError
                      ? mensagemDeErro(reenvio.error)
                      : texto}
            </span>
            {!reenvio.isSuccess && !perfil.emailConfirmado && (
                <Botao variante="secundario" isPending={reenvio.isPending} onPress={() => reenvio.mutate()}>
                    Reenviar link
                </Botao>
            )}
        </div>
    )
}
