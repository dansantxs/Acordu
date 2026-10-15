import { useMutation } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import { mensagemDeErro } from '@/shared/api/mensagens'
import type { Perfil } from '@/shared/sessao/perfil'
import { Botao } from '@/shared/ui/Botao'

/** O login não exige e-mail confirmado (contrato §2); o app lembra e oferece reenviar. */
export function AvisoConfirmacao({ perfil }: { perfil: Perfil }) {
    const reenvio = useMutation({ mutationFn: () => api.POST('/api/v1/auth/confirmacao-email/reenvio') })
    if (perfil.emailConfirmado) return null

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
                      : `Confirme seu e-mail: mandamos um link para ${perfil.email}.`}
            </span>
            {!reenvio.isSuccess && (
                <Botao variante="secundario" isPending={reenvio.isPending} onPress={() => reenvio.mutate()}>
                    Reenviar link
                </Botao>
            )}
        </div>
    )
}
