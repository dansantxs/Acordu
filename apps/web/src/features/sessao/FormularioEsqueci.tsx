import { useMutation } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Form } from 'react-aria-components'
import { api } from '@/shared/api/cliente'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'

export function FormularioEsqueci() {
    const envio = useMutation({
        mutationFn: (email: string) => api.POST('/api/v1/auth/senha/esqueci', { body: { email } }),
    })

    if (envio.isSuccess) {
        return (
            <div className="flex flex-col gap-4">
                {/* A API responde igual tendo conta ou não: a tela também. */}
                <Alerta tom="sucesso">
                    Se existir uma conta com esse e-mail, o link para criar uma senha nova já está a caminho. Ele vale
                    por 1 hora.
                </Alerta>
                <Link to="/entrar" className="text-sm text-acento-texto underline-offset-4 hover:underline">
                    Voltar para entrar
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
                envio.mutate(String(new FormData(evento.currentTarget).get('email')))
            }}
        >
            {mensagem && <Alerta>{mensagem}</Alerta>}
            <CampoTexto
                name="email"
                type="email"
                rotulo="E-mail da conta"
                autoComplete="username"
                isRequired
                autoFocus
            />
            <div className="flex items-center justify-between gap-4">
                <Link to="/entrar" className="text-sm text-acento-texto underline-offset-4 hover:underline">
                    Lembrei a senha
                </Link>
                <Botao type="submit" isPending={envio.isPending}>
                    Enviar link
                </Botao>
            </div>
        </Form>
    )
}
