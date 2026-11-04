import { useMutation } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useId, useState } from 'react'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { CHAVE_TURNSTILE } from '@/shared/config/ambiente'
import { cadastrar } from '@/shared/sessao/acoes'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { DesafioTurnstile } from './DesafioTurnstile'

/** Mesma regra da API (contrato §2): senha com pelo menos 10 caracteres. */
export const SENHA_MINIMA = 10

export function validarSenha(valor: string): string | null {
    return valor.length < SENHA_MINIMA ? `Use pelo menos ${SENHA_MINIMA} caracteres.` : null
}

/** O limite do cadastro é por hora (RNF-SEG-10), não o "aguarde um minuto" das demais rotas. */
const MUITOS_CADASTROS = 'Muitos cadastros a partir desta rede em pouco tempo. Tente de novo mais tarde.'

/**
 * Cadastro da empresa. Com `chaveTurnstile` (a `VITE_TURNSTILE_CHAVE` do build), mostra o desafio do Cloudflare e só
 * envia com o token; recusa do token ou limite atingido renovam o desafio (spec do F9, F9-39 a F9-41).
 */
export function FormularioCadastro({ chaveTurnstile = CHAVE_TURNSTILE }: { chaveTurnstile?: string }) {
    const navegar = useNavigate()
    const [token, setToken] = useState<string | null>(null)
    const [versaoDesafio, setVersaoDesafio] = useState(0)
    const motivo = useId()
    const aguardandoDesafio = Boolean(chaveTurnstile) && !token
    const envio = useMutation({
        mutationFn: cadastrar,
        onSuccess: () => navegar({ to: '/' }),
        // O token só vale uma vez: qualquer recusa pede outro.
        onError: () => {
            if (chaveTurnstile) setVersaoDesafio((v) => v + 1)
        },
    })
    const erro = envio.error
    const mensagem =
        erro instanceof ErroApi && erro.status === 429
            ? MUITOS_CADASTROS
            : erro instanceof ErroApi && erro.doCampo('tokenTurnstile')
              ? erro.doCampo('tokenTurnstile')
              : envio.isError
                ? mensagemDeErro(erro)
                : null

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
                    ...(chaveTurnstile ? { tokenTurnstile: token } : {}),
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
            {chaveTurnstile && <DesafioTurnstile chave={chaveTurnstile} versao={versaoDesafio} aoMudar={setToken} />}
            {aguardandoDesafio && (
                <p id={motivo} className="text-sm text-suave">
                    Conclua a verificação acima para criar a conta.
                </p>
            )}
            <div className="flex items-center justify-between gap-4">
                <Link to="/entrar" className="text-sm text-acento-texto underline-offset-4 hover:underline">
                    Já tenho conta
                </Link>
                <Botao
                    type="submit"
                    isPending={envio.isPending}
                    isDisabled={aguardandoDesafio}
                    aria-describedby={aguardandoDesafio ? motivo : undefined}
                >
                    Criar conta
                </Botao>
            </div>
        </Form>
    )
}
