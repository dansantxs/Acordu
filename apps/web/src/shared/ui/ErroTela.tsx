import { Link, useRouter, type ErrorComponentProps } from '@tanstack/react-router'
import { ErroApi } from '../api/problema'
import { mensagemDeErro } from '../api/mensagens'
import { Botao } from './Botao'

function textoDoErro(erro: unknown): { titulo: string; detalhe: string } {
    if (erro instanceof ErroApi && erro.status === 403)
        return {
            titulo: 'Sem acesso a esta página',
            detalhe: 'Seu papel nesta empresa não inclui isso. Fale com quem administra a conta.',
        }
    if (erro instanceof ErroApi && erro.status === 404)
        return { titulo: 'Não encontramos isso', detalhe: 'Pode ter sido excluído, ou o link está incompleto.' }
    return { titulo: 'Esta página não carregou', detalhe: mensagemDeErro(erro) ?? 'Algo deu errado.' }
}

/** Erro ao carregar uma tela (componente de erro padrão do roteador): diz o que houve e oferece tentar de novo. */
export function ErroTela({ error, reset }: ErrorComponentProps) {
    const roteador = useRouter()
    const { titulo, detalhe } = textoDoErro(error)
    return (
        <div
            role="alert"
            className="flex flex-col items-start gap-3 rounded-couro border border-linha bg-superficie px-6 py-10"
        >
            <p className="font-titulo text-xl font-semibold">{titulo}</p>
            <p className="max-w-prose text-sm text-suave">{detalhe}</p>
            <div className="flex gap-3">
                <Botao
                    variante="secundario"
                    onPress={() => {
                        reset()
                        void roteador.invalidate()
                    }}
                >
                    Tentar de novo
                </Botao>
                <Link to="/" className="self-center text-sm text-acento-texto underline-offset-4 hover:underline">
                    Ir para o início
                </Link>
            </div>
        </div>
    )
}

/** Endereço que não existe, dentro da casca. */
export function NaoEncontrada() {
    return (
        <div className="flex flex-col items-start gap-3 rounded-couro border border-dashed border-linha px-6 py-10">
            <p className="font-titulo text-xl font-semibold">Página não encontrada</p>
            <p className="text-sm text-suave">Confira o endereço ou volte para o início.</p>
            <Link to="/" className="text-sm font-semibold text-acento-texto underline-offset-4 hover:underline">
                Ir para o início
            </Link>
        </div>
    )
}
