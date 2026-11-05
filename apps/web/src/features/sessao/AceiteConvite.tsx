import { useMutation, useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { Form } from 'react-aria-components'
import { api } from '@/shared/api/cliente'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { aceitarConvite } from '@/shared/sessao/acoes'
import { descartarTokenDeLink } from '@/shared/sessao/tokenDaUrl'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSenha } from '@/shared/ui/CampoSenha'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { SENHA_MINIMA, validarSenha } from './FormularioCadastro'
import { linkRecusado } from './linkInvalido'

/** Aceite do convite para a equipe (contrato §3): conta nova pede nome e senha; conta existente, só a senha dela. */
export function AceiteConvite({ token }: { token: string | null }) {
    const navegar = useNavigate()
    const convite = useQuery({
        queryKey: ['conta', 'convite', token],
        queryFn: async () => (await api.POST('/api/v1/auth/convite/consulta', { body: { token: token ?? '' } })).data!,
        enabled: token !== null,
        retry: false,
        staleTime: Infinity,
    })
    const aceite = useMutation({
        mutationFn: aceitarConvite,
        onSuccess: () => {
            descartarTokenDeLink('convite')
            return navegar({ to: '/' })
        },
    })

    if (token === null || linkRecusado(convite.error) || linkRecusado(aceite.error)) {
        return (
            <Alerta>
                Este convite não vale mais: ele expira em 7 dias, só pode ser usado uma vez e pode ter sido cancelado.
                Peça um convite novo a quem convidou você.
            </Alerta>
        )
    }
    if (convite.isPending) return <p className="text-suave">Carregando o convite…</p>
    if (convite.isError) return <Alerta>{mensagemDeErro(convite.error)}</Alerta>

    const { nomeTenant, email, papel, temConta } = convite.data
    const mensagem = aceite.isError ? mensagemDeErro(aceite.error) : null

    return (
        <Form
            className="flex flex-col gap-4"
            validationErrors={errosDeCampo(aceite.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                const dados = new FormData(evento.currentTarget)
                aceite.mutate({
                    token,
                    nome: temConta ? null : String(dados.get('nome')),
                    senha: String(dados.get('senha')),
                })
            }}
        >
            <p className="text-sm">
                <strong>{nomeTenant}</strong> convidou <strong>{email}</strong> para a equipe, como{' '}
                <strong>{papel}</strong>.
            </p>
            {mensagem && <Alerta>{mensagem}</Alerta>}
            {temConta ? (
                <CampoSenha
                    name="senha"
                    rotulo="Senha da sua conta"
                    descricao="Você já tem conta no Acordu: a empresa nova aparece ao lado das que você já usa."
                    autoComplete="current-password"
                    isRequired
                    autoFocus
                />
            ) : (
                <>
                    <CampoTexto name="nome" rotulo="Seu nome" autoComplete="name" isRequired autoFocus />
                    <CampoSenha
                        name="senha"
                        rotulo="Crie uma senha"
                        descricao={`Pelo menos ${SENHA_MINIMA} caracteres.`}
                        autoComplete="new-password"
                        validate={validarSenha}
                        isRequired
                    />
                </>
            )}
            <div className="flex items-center justify-between gap-4">
                {temConta ? (
                    <Link to="/conta/senha" className="text-sm text-acento-texto underline-offset-4 hover:underline">
                        Esqueci a senha
                    </Link>
                ) : (
                    <span />
                )}
                <Botao type="submit" isPending={aceite.isPending}>
                    Entrar na equipe
                </Botao>
            </div>
        </Form>
    )
}
