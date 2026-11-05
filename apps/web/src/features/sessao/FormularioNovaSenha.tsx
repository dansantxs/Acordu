import { useMutation } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { redefinirSenha } from '@/shared/sessao/acoes'
import { descartarTokenDeLink } from '@/shared/sessao/tokenDaUrl'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSenha } from '@/shared/ui/CampoSenha'
import { SENHA_MINIMA, validarSenha } from './FormularioCadastro'
import { linkRecusado } from './linkInvalido'

export function FormularioNovaSenha({ token }: { token: string | null }) {
    const [senha, setSenha] = useState('')
    const envio = useMutation({
        mutationFn: (novaSenha: string) => redefinirSenha(token ?? '', novaSenha),
        onSuccess: () => descartarTokenDeLink('senha'),
    })

    if (envio.isSuccess) {
        return (
            <div className="flex flex-col gap-4">
                <Alerta tom="sucesso">Senha trocada. Por segurança, todas as sessões abertas foram encerradas.</Alerta>
                <Link to="/entrar" className="font-semibold text-acento-texto underline-offset-4 hover:underline">
                    Entrar com a senha nova
                </Link>
            </div>
        )
    }

    if (!token || linkRecusado(envio.error)) {
        return (
            <div className="flex flex-col gap-4">
                <Alerta>Este link não vale mais. Ele expira em 1 hora e só pode ser usado uma vez.</Alerta>
                <Link to="/conta/senha" className="font-semibold text-acento-texto underline-offset-4 hover:underline">
                    Pedir um link novo
                </Link>
            </div>
        )
    }

    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null
    return (
        <Form
            className="flex flex-col gap-4"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                envio.mutate(senha)
            }}
        >
            {mensagem && <Alerta>{mensagem}</Alerta>}
            <CampoSenha
                name="novaSenha"
                rotulo="Senha nova"
                autoComplete="new-password"
                descricao={`Pelo menos ${SENHA_MINIMA} caracteres.`}
                value={senha}
                onChange={setSenha}
                validate={validarSenha}
                isRequired
                autoFocus
            />
            <CampoSenha
                name="confirmacao"
                rotulo="Repita a senha nova"
                autoComplete="new-password"
                validate={(valor) => (valor !== senha ? 'As senhas não são iguais.' : null)}
                isRequired
            />
            <div className="flex justify-end">
                <Botao type="submit" isPending={envio.isPending}>
                    Trocar senha
                </Botao>
            </div>
        </Form>
    )
}
