import { useMutation } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useId, useState } from 'react'
import { Form } from 'react-aria-components'
import { AceiteTextosLegais, erroDoAceite, versoesAceitas } from '@/features/legal/AceiteTextosLegais'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { CHAVE_TURNSTILE, CONTATO_NEBRYS } from '@/shared/config/ambiente'
import { documentoValido } from '@/shared/format/documento'
import { caracteresDoDocumento, formatarDocumento } from '@/shared/format/mascaras'
import { cadastrar } from '@/shared/sessao/acoes'
import { descartarTokenDeLink } from '@/shared/sessao/tokenDaUrl'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoMascarado } from '@/shared/ui/CampoMascarado'
import { CampoSenha } from '@/shared/ui/CampoSenha'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { DesafioTurnstile } from './DesafioTurnstile'

/** Mesma regra da API (contrato §2): senha com pelo menos 10 caracteres. */
export const SENHA_MINIMA = 10

export function validarSenha(valor: string): string | null {
    return valor.length < SENHA_MINIMA ? `Use pelo menos ${SENHA_MINIMA} caracteres.` : null
}

/**
 * CPF ou CNPJ do cadastro (RF-AUT-09, spec do F14, F14-56): obrigatório e com os dígitos verificadores certos. A API
 * confere de novo e recusa o documento que já teve teste (F14-57), com a mensagem no campo.
 */
export function validarDocumentoCadastro(valor: string): string | null {
    if (valor.trim() === '') return 'Informe o CPF ou CNPJ.'
    return documentoValido(valor) ? null : 'CPF ou CNPJ inválido.'
}

/** Um teste por documento (RF-AUT-09); o canal da Nebrys (`VITE_CONTATO_NEBRYS`) para quem precisa de um novo. */
export const DESCRICAO_DOCUMENTO = CONTATO_NEBRYS
    ? `Cada CPF ou CNPJ tem um período de teste. Para um novo teste, fale com a Nebrys: ${CONTATO_NEBRYS}.`
    : 'Cada CPF ou CNPJ tem um período de teste. Para um novo teste, fale com a Nebrys.'

/** O limite do cadastro é por hora (RNF-SEG-10), não o "aguarde um minuto" das demais rotas. */
const MUITOS_CADASTROS = 'Muitos cadastros a partir desta rede em pouco tempo. Tente de novo mais tarde.'

/** O convite de empresa que abriu o cadastro (F14-46): o token do link e o e-mail para o qual ele vale. */
export interface ConviteDoCadastro {
    token: string
    email: string
}

/**
 * Cadastro da empresa. Com `chaveTurnstile` (a `VITE_TURNSTILE_CHAVE` do build), mostra o desafio do Cloudflare e só
 * envia com o token; recusa do token ou limite atingido renovam o desafio (spec do F9, F9-39 a F9-41).
 *
 * Com `convite` (F14-46, pergunta 7 do G2), o e-mail é o do convite, sem poder trocá-lo, e o token vai no corpo; a recusa
 * do convite pela API (`errors.convite`) aparece no topo, porque não há campo para ela. Sem convite, a mesma recusa quer
 * dizer que o cadastro é só por convite (a tela não soube o modo): `aoExigirConvite` troca para a lista de espera.
 */
export function FormularioCadastro({
    chaveTurnstile = CHAVE_TURNSTILE,
    convite,
    aoExigirConvite,
}: {
    chaveTurnstile?: string
    convite?: ConviteDoCadastro
    aoExigirConvite?: () => void
}) {
    const navegar = useNavigate()
    const [token, setToken] = useState<string | null>(null)
    const [versaoDesafio, setVersaoDesafio] = useState(0)
    const motivo = useId()
    const aguardandoDesafio = Boolean(chaveTurnstile) && !token
    const envio = useMutation({
        mutationFn: cadastrar,
        onSuccess: () => {
            if (convite) descartarTokenDeLink('convite-empresa')
            return navegar({ to: '/' })
        },
        // O token só vale uma vez: qualquer recusa pede outro.
        onError: (falha) => {
            if (chaveTurnstile) setVersaoDesafio((v) => v + 1)
            if (!convite && falha instanceof ErroApi && falha.doCampo('convite')) aoExigirConvite?.()
        },
    })
    const erro = envio.error
    const mensagem =
        erro instanceof ErroApi && erro.status === 429
            ? MUITOS_CADASTROS
            : erro instanceof ErroApi && (erro.doCampo('tokenTurnstile') || erro.doCampo('convite'))
              ? (erro.doCampo('tokenTurnstile') ?? erro.doCampo('convite'))
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
                    email: convite ? convite.email : String(dados.get('email')),
                    senha: String(dados.get('senha')),
                    nomeEmpresa: String(dados.get('nomeEmpresa')),
                    documento: String(dados.get('documento')),
                    ...versoesAceitas(dados),
                    ...(chaveTurnstile ? { tokenTurnstile: token } : {}),
                    ...(convite ? { convite: convite.token } : {}),
                })
            }}
        >
            {mensagem && <Alerta>{mensagem}</Alerta>}
            <CampoTexto name="nome" rotulo="Seu nome" autoComplete="name" isRequired autoFocus />
            {convite ? (
                <CampoTexto
                    name="email"
                    type="email"
                    rotulo="E-mail"
                    value={convite.email}
                    isReadOnly
                    descricao="O e-mail do convite. O cadastro é com ele."
                    className="[&_input]:bg-trilho"
                />
            ) : (
                <CampoTexto name="email" type="email" rotulo="E-mail" autoComplete="email" isRequired />
            )}
            <CampoSenha
                name="senha"
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
            <CampoMascarado
                name="documento"
                rotulo="CPF ou CNPJ"
                mascara={formatarDocumento}
                caracteres={caracteresDoDocumento}
                teclado="text"
                validate={validarDocumentoCadastro}
                descricao={DESCRICAO_DOCUMENTO}
                isRequired
            />
            <AceiteTextosLegais erro={erroDoAceite(envio.error)} />
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
