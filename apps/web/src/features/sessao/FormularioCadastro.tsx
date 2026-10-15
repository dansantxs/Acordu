import { useMutation } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { cadastrar } from '@/shared/sessao/acoes'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'

/** Mesma regra da API (contrato §2): senha com pelo menos 10 caracteres. */
export const SENHA_MINIMA = 10

export function validarSenha(valor: string): string | null {
    return valor.length < SENHA_MINIMA ? `Use pelo menos ${SENHA_MINIMA} caracteres.` : null
}

export function FormularioCadastro() {
    const navegar = useNavigate()
    const envio = useMutation({ mutationFn: cadastrar, onSuccess: () => navegar({ to: '/' }) })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null

    return (
        <Form
            className="flex flex-col gap-4"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                const dados = new FormData(evento.currentTarget)
                envio.mutate({
                    nome: String(dados.get('nome')),
                    email: String(dados.get('email')),
                    senha: String(dados.get('senha')),
                    nomeEmpresa: String(dados.get('nomeEmpresa')),
                })
            }}
        >
            {mensagem && <Alerta>{mensagem}</Alerta>}
            <CampoTexto name="nome" rotulo="Seu nome" autoComplete="name" isRequired autoFocus />
            <CampoTexto name="email" type="email" rotulo="E-mail" autoComplete="email" isRequired />
            <CampoTexto
                name="senha"
                type="password"
                rotulo="Senha"
                autoComplete="new-password"
                descricao={`Pelo menos ${SENHA_MINIMA} caracteres.`}
                validate={validarSenha}
                isRequired
            />
            <CampoTexto
                name="nomeEmpresa"
                rotulo="Nome da empresa"
                descricao="Como seus clientes conhecem você. Dá para mudar depois."
                autoComplete="organization"
                isRequired
            />
            <div className="flex items-center justify-between gap-4">
                <Link to="/entrar" className="text-sm text-acento-texto underline-offset-4 hover:underline">
                    Já tenho conta
                </Link>
                <Botao type="submit" isPending={envio.isPending}>
                    Criar conta
                </Botao>
            </div>
        </Form>
    )
}
