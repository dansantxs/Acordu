import { useMutation } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useId, useState } from 'react'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { CHAVE_TURNSTILE, CONTATO_NEBRYS } from '@/shared/config/ambiente'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { entrarNaListaEspera } from './cadastro'
import { DesafioTurnstile } from './DesafioTurnstile'

/** O motivo de a tela de cadastro mostrar a lista de espera (F14-44). */
export const MOTIVO_LISTA_ESPERA =
    'No momento, o cadastro no Acordu é só por convite. Deixe seus dados: a Nebrys escolhe a quem convidar e manda o convite por e-mail.'

/** Mesmo limite da API (contrato §16): mensagem opcional, até 1.000 caracteres. */
export const MENSAGEM_MAXIMA = 1000

/** O limite da lista de espera é por hora (RNF-SEG-10), como o do cadastro. */
const MUITOS_ENVIOS = 'Muitos envios a partir desta rede em pouco tempo. Tente de novo mais tarde.'

/**
 * Formulário da lista de espera (RF-AUT-08; contrato §16; F14-52 a F14-55). Com `chaveTurnstile`, o desafio usa a ação
 * `lista-espera` e só envia com o token; recusa do token ou limite renovam o desafio. A confirmação é a mesma para quem
 * entra e para quem já estava na lista (a API responde igual, F14-53). Não pede aceite dos textos legais (F14-109).
 */
export function FormularioListaEspera({ chaveTurnstile = CHAVE_TURNSTILE }: { chaveTurnstile?: string }) {
    const [token, setToken] = useState<string | null>(null)
    const [versaoDesafio, setVersaoDesafio] = useState(0)
    const motivo = useId()
    const aguardandoDesafio = Boolean(chaveTurnstile) && !token
    const envio = useMutation({
        mutationFn: entrarNaListaEspera,
        // O token só vale uma vez: qualquer recusa pede outro.
        onError: () => {
            if (chaveTurnstile) setVersaoDesafio((v) => v + 1)
        },
    })

    if (envio.isSuccess)
        return (
            <div className="flex flex-col gap-4">
                <Alerta tom="sucesso">
                    Recebemos seus dados. Quando a Nebrys puder convidar você, o convite chega por e-mail, com o link
                    para criar a conta.
                </Alerta>
                {CONTATO_NEBRYS && <p className="text-sm text-suave">Para falar com a Nebrys: {CONTATO_NEBRYS}.</p>}
                <Link to="/entrar" className="text-sm text-acento-texto underline-offset-4 hover:underline">
                    Ir para a tela de entrar
                </Link>
            </div>
        )

    const erro = envio.error
    const mensagem =
        erro instanceof ErroApi && erro.status === 429
            ? MUITOS_ENVIOS
            : erro instanceof ErroApi && erro.doCampo('tokenTurnstile')
              ? erro.doCampo('tokenTurnstile')
              : envio.isError
                ? mensagemDeErro(erro)
                : null

    return (
        <Form
            className="flex flex-col gap-4"
            aria-label="Lista de espera"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                const dados = new FormData(evento.currentTarget)
                const texto = String(dados.get('mensagem') ?? '').trim()
                envio.mutate({
                    nome: String(dados.get('nome')),
                    email: String(dados.get('email')),
                    nomeEmpresa: String(dados.get('nomeEmpresa')),
                    mensagem: texto === '' ? null : texto,
                    ...(chaveTurnstile ? { tokenTurnstile: token } : {}),
                })
            }}
        >
            {mensagem && <Alerta>{mensagem}</Alerta>}
            <CampoTexto name="nome" rotulo="Seu nome" autoComplete="name" isRequired autoFocus />
            <CampoTexto name="email" type="email" rotulo="E-mail" autoComplete="email" isRequired />
            <CampoTexto name="nomeEmpresa" rotulo="Nome da empresa" autoComplete="organization" isRequired />
            <CampoTexto
                name="mensagem"
                rotulo="O que você quer organizar com o Acordu?"
                descricao="Opcional. Ajuda a Nebrys a entender o seu trabalho."
                maxLength={MENSAGEM_MAXIMA}
                multilinha
            />
            {chaveTurnstile && (
                <DesafioTurnstile
                    chave={chaveTurnstile}
                    versao={versaoDesafio}
                    aoMudar={setToken}
                    acao="lista-espera"
                />
            )}
            {aguardandoDesafio && (
                <p id={motivo} className="text-sm text-suave">
                    Conclua a verificação acima para enviar.
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
                    Entrar na lista de espera
                </Botao>
            </div>
        </Form>
    )
}
