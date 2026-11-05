import { useMutation, useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { Form } from 'react-aria-components'
import { SENHA_MINIMA, validarSenha } from '@/features/sessao/FormularioCadastro'
import { linkRecusado } from '@/features/sessao/linkInvalido'
import { api } from '@/shared/api/cliente'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { aceitarConvitePortal } from '@/shared/sessao/acoes'
import { descartarTokenDeLink } from '@/shared/sessao/tokenDaUrl'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSenha } from '@/shared/ui/CampoSenha'
import { CampoTexto } from '@/shared/ui/CampoTexto'

/**
 * Aceite do convite do portal (contrato §2, F7a): conta nova pede nome (já sugerido pelo contato) e senha; conta que já
 * existe, só a senha dela. Depois de aceitar, o contato cai na lista do portal.
 */
export function AceiteConvitePortal({ token }: { token: string | null }) {
    const navegar = useNavigate()
    const convite = useQuery({
        queryKey: ['conta', 'convite-portal', token],
        queryFn: async () =>
            (await api.POST('/api/v1/auth/portal/convite/consulta', { body: { token: token ?? '' } })).data!,
        enabled: token !== null,
        retry: false,
        staleTime: Infinity,
    })
    const aceite = useMutation({
        mutationFn: aceitarConvitePortal,
        onSuccess: () => {
            descartarTokenDeLink('convite-portal')
            return navegar({ to: '/portal' })
        },
    })

    if (token === null || linkRecusado(convite.error) || linkRecusado(aceite.error)) {
        return (
            <Alerta>
                Este convite não vale mais: ele expira em 7 dias, só pode ser usado uma vez e pode ter sido trocado por
                um mais novo. Peça um convite novo a quem atende você.
            </Alerta>
        )
    }
    if (convite.isPending) return <p className="text-suave">Carregando o convite…</p>
    if (convite.isError) return <Alerta>{mensagemDeErro(convite.error)}</Alerta>

    const { nomePrestador, nomeContato, email, temConta } = convite.data
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
                <strong>{nomePrestador}</strong> convidou <strong>{email}</strong> para acompanhar o atendimento pelo
                portal: abrir solicitações, conversar com a equipe e aprovar as entregas.
            </p>
            {mensagem && <Alerta>{mensagem}</Alerta>}
            {temConta ? (
                <CampoSenha
                    name="senha"
                    rotulo="Senha da sua conta"
                    descricao="Você já tem conta no Acordu: entre com a mesma senha."
                    autoComplete="current-password"
                    isRequired
                    autoFocus
                />
            ) : (
                <>
                    <CampoTexto
                        name="nome"
                        rotulo="Seu nome"
                        defaultValue={nomeContato}
                        autoComplete="name"
                        isRequired
                        autoFocus
                    />
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
                    Entrar no portal
                </Botao>
            </div>
        </Form>
    )
}
