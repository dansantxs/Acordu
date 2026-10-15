import { useMutation } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { destinoSeguro, entrar } from '@/shared/sessao/acoes'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'

export function FormularioEntrar({ voltar }: { voltar?: string }) {
    const navegar = useNavigate()
    const envio = useMutation({
        mutationFn: ({ email, senha }: { email: string; senha: string }) => entrar(email, senha),
        onSuccess: () => navegar({ to: destinoSeguro(voltar) }),
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null

    return (
        <Form
            className="flex flex-col gap-4"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                const dados = new FormData(evento.currentTarget)
                envio.mutate({ email: String(dados.get('email')), senha: String(dados.get('senha')) })
            }}
        >
            {mensagem && <Alerta>{mensagem}</Alerta>}
            <CampoTexto name="email" type="email" rotulo="E-mail" autoComplete="username" isRequired autoFocus />
            <CampoTexto name="senha" type="password" rotulo="Senha" autoComplete="current-password" isRequired />
            <div className="flex items-center justify-end gap-4">
                <Botao type="submit" isPending={envio.isPending}>
                    Entrar
                </Botao>
            </div>
            <p className="border-t border-linha pt-4 text-sm text-suave">
                Ainda não usa o Acordu?{' '}
                <Link to="/cadastro" className="font-semibold text-acento-texto underline-offset-4 hover:underline">
                    Crie a conta da sua empresa
                </Link>
            </p>
        </Form>
    )
}
