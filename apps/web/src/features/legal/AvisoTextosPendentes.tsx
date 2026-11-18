import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useRouter } from '@tanstack/react-router'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { TEXTOS_LEGAIS_PUBLICADOS } from '@/shared/config/ambiente'
import { marcarCienteDosTextos } from '@/shared/sessao/acoes'
import { consultaPerfil, type Perfil } from '@/shared/sessao/perfil'
import { Botao } from '@/shared/ui/Botao'

const NOMES = {
    TERMOS: { nome: 'termos de uso', caminho: '/termos' },
    PRIVACIDADE: { nome: 'política de privacidade', caminho: '/privacidade' },
} as const

/**
 * Aviso dos textos legais que a pessoa ainda não viu na versão vigente (F14-83; e quem não tem aceite nenhum, F14-119):
 * link para cada página e "Ciente", que grava a versão e a data. Não bloqueia o uso (decisão 12 do G2). Só com os
 * textos publicados neste build **e** pendentes no `/auth/eu` (D16 do F14): uma divergência entre o SPA e a API
 * esconde o aviso em vez de mostrar link para "não encontrada" (F14-118). "Ciente" não é gravação da empresa: segue
 * numa empresa só leitura (decisão 10 do G2).
 */
export function AvisoTextosPendentes({ perfil }: { perfil: Perfil }) {
    const consultas = useQueryClient()
    const roteador = useRouter()
    const ciente = useMutation({
        mutationFn: marcarCienteDosTextos,
        onSuccess: () =>
            consultas.refetchQueries({ queryKey: consultaPerfil.queryKey }).then(() => roteador.invalidate()),
    })
    const pendentes = perfil.textosPendentes ?? []
    if (!TEXTOS_LEGAIS_PUBLICADOS || pendentes.length === 0 || ciente.isSuccess) return null

    const textos = pendentes.map((p) => NOMES[p.texto])
    return (
        <div
            role="status"
            aria-label="Textos legais"
            className="flex flex-wrap items-center justify-between gap-3 rounded-couro border border-linha bg-superficie px-4 py-3 text-sm"
        >
            <span>
                {ciente.isError ? (
                    mensagemDeErro(ciente.error)
                ) : (
                    <>
                        {textos.length > 1 ? 'Há versões dos textos do Acordu' : 'Há uma versão de um texto do Acordu'}{' '}
                        que você ainda não viu:{' '}
                        {textos.map((t, i) => (
                            <span key={t.caminho}>
                                {i > 0 && ' e '}
                                <Link
                                    to={t.caminho}
                                    target="_blank"
                                    rel="noopener"
                                    className="text-acento-texto underline underline-offset-2"
                                >
                                    {t.nome}
                                    <span className="sr-only"> (abre em outra aba)</span>
                                </Link>
                            </span>
                        ))}
                        . Leia quando puder e marque que está ciente.
                    </>
                )}
            </span>
            <Botao variante="secundario" isPending={ciente.isPending} onPress={() => ciente.mutate()}>
                Ciente
            </Botao>
        </div>
    )
}
