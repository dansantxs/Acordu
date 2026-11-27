import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar } from '@/shared/avisos/avisos'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Janela } from '@/shared/ui/Janela'
import { Pagina } from '@/shared/ui/Pagina'
import { apiNebrys, encerrarSeRecusada, type Demonstracao as DadosDemonstracao, type PessoaIndicada } from './api'
import { consultaDemonstracao } from './consultas'
import { recusaDaAcao } from './convites'
import { data } from './situacao'
import { textoDoFornecedor } from '@/shared/config/fornecedor'

const SUMIU = 'A empresa de demonstração não existe mais. A tela abaixo é a de agora.'

type Confirmacao = { tipo: 'restauracao' } | { tipo: 'retirada'; pessoa: PessoaIndicada }

/**
 * Empresa de demonstração (RF-NEB-02; F14-87 a F14-90, F14-110): uma só, com os dados fictícios do manual, fora do ciclo
 * de situação e sem envio de e-mail. A Nebrys cria, restaura (os dados voltam ao início, as pessoas indicadas ficam) e
 * indica quem entra nela como Dono, só com conta que já existe no Acordu: a demonstração não manda convite. A pessoa
 * indicada a vê na troca de empresa do próprio app. `404` de uma ação (a demonstração não existe mais) relê a tela.
 */
export function Demonstracao() {
    const consultas = useQueryClient()
    const demonstracao = useQuery(consultaDemonstracao())
    // O GET responde 200 também sem demonstração: o 404 aqui é só a barreira (sessão vencida ou fora da lista).
    useEffect(() => encerrarSeRecusada(demonstracao.error), [demonstracao.error])
    const [aviso, setAviso] = useState<string | null>(null)
    const [confirmacao, setConfirmacao] = useState<Confirmacao | null>(null)

    const atualizar = (dados: DadosDemonstracao) => {
        consultas.setQueryData(consultaDemonstracao().queryKey, dados)
        // A demonstração aparece na lista de empresas (ATIVA): a lista é lida de novo quando for aberta.
        void consultas.invalidateQueries({ queryKey: ['nebrys', 'empresas'], refetchType: 'none' })
    }
    const sumiu = (erro: unknown) => {
        if (!(erro instanceof ErroApi && erro.status === 404)) return false
        setConfirmacao(null)
        setAviso(SUMIU)
        void consultas.invalidateQueries({ queryKey: consultaDemonstracao().queryKey })
        return true
    }

    const criacao = useMutation({
        mutationFn: async () => (await apiNebrys.POST('/api/v1/nebrys/demonstracao')).data!,
        onSuccess: (dados) => {
            setAviso(null)
            atualizar(dados)
            avisar('Empresa de demonstração criada.')
        },
        onError: (erro) => {
            // 422: outra criação passou na frente; a tela de agora mostra a que existe.
            if (erro instanceof ErroApi && erro.status === 422)
                void consultas.invalidateQueries({ queryKey: consultaDemonstracao().queryKey })
        },
    })
    const acao = useMutation({
        mutationFn: async (qual: Confirmacao): Promise<DadosDemonstracao | null> => {
            if (qual.tipo === 'restauracao')
                return (await apiNebrys.POST('/api/v1/nebrys/demonstracao/restauracao')).data!
            await apiNebrys.DELETE('/api/v1/nebrys/demonstracao/indicacoes/{usuarioId}', {
                params: { path: { usuarioId: qual.pessoa.usuarioId } },
            })
            return null
        },
        onSuccess: (dados, qual) => {
            setConfirmacao(null)
            setAviso(null)
            if (dados) {
                atualizar(dados)
                avisar('Dados da demonstração restaurados.')
            } else {
                void consultas.invalidateQueries({ queryKey: consultaDemonstracao().queryKey })
                if (qual.tipo === 'retirada') avisar(`${qual.pessoa.nome} saiu da demonstração.`)
            }
        },
        onError: (erro) => {
            sumiu(erro)
        },
    })
    const abrir = (qual: Confirmacao) => {
        acao.reset()
        setConfirmacao(qual)
    }
    const erroNaJanela = acao.error instanceof ErroApi && acao.error.status === 404 ? null : recusaDaAcao(acao.error)

    return (
        <Pagina titulo="Demonstração" contexto="Área da Nebrys">
            <p className="max-w-prose text-sm text-suave">
                Uma empresa com os dados fictícios do manual, para mostrar o Acordu. Ela nunca vence nem é expurgada, e
                nada sai dela por e-mail.{' '}
                {textoDoFornecedor('Entra nela, como Dono, só quem {{a fornecedor}} indicar.')}
            </p>
            {aviso && <Alerta tom="aviso">{aviso}</Alerta>}
            {demonstracao.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : demonstracao.isError ? (
                <Alerta>{mensagemDeErro(demonstracao.error)}</Alerta>
            ) : !demonstracao.data.empresaId ? (
                <div className="flex flex-col items-start gap-3 rounded-couro border border-dashed border-linha px-6 py-8">
                    <p className="font-titulo text-xl font-semibold">A empresa de demonstração ainda não existe</p>
                    <p className="max-w-prose text-sm text-suave">
                        Criar monta a equipe, os clientes, as solicitações, as tarefas e as horas fictícias, sem enviar
                        nenhum e-mail.
                    </p>
                    {criacao.isError && <Alerta>{recusaDaAcao(criacao.error)}</Alerta>}
                    <Botao onPress={() => criacao.mutate()} isPending={criacao.isPending}>
                        Criar a empresa de demonstração
                    </Botao>
                </div>
            ) : (
                <>
                    <section
                        aria-label="Empresa de demonstração"
                        className="flex flex-wrap items-start justify-between gap-4 rounded-couro border border-linha bg-superficie px-5 py-4"
                    >
                        <div className="flex flex-col gap-1">
                            <span className="font-titulo text-xl font-semibold">{demonstracao.data.nome}</span>
                            {demonstracao.data.criadaEm && (
                                <span className="text-xs text-suave">Criada em {data(demonstracao.data.criadaEm)}</span>
                            )}
                            <Link
                                to="/nebrys/empresas/$id"
                                params={{ id: demonstracao.data.empresaId }}
                                className="text-sm text-acento-texto underline-offset-4 hover:underline"
                            >
                                Ver o detalhe da empresa
                            </Link>
                        </div>
                        <Botao variante="secundario" onPress={() => abrir({ tipo: 'restauracao' })}>
                            Restaurar os dados
                        </Botao>
                    </section>
                    <PessoasIndicadas
                        indicadas={demonstracao.data.indicadas}
                        aoRetirar={(pessoa) => abrir({ tipo: 'retirada', pessoa })}
                    />
                    <IndicarPessoa
                        aoIndicar={(dados, email) => {
                            setAviso(null)
                            atualizar(dados)
                            avisar(`${email} entra na demonstração como Dono.`)
                        }}
                        aoSumir={sumiu}
                    />
                </>
            )}
            {confirmacao && (
                <Janela
                    titulo={
                        confirmacao.tipo === 'restauracao'
                            ? 'Restaurar os dados da demonstração?'
                            : `Tirar ${confirmacao.pessoa.nome} da demonstração?`
                    }
                    aberta
                    aoMudar={(aberta) => !aberta && setConfirmacao(null)}
                    ocupada={acao.isPending}
                >
                    <p className="text-sm text-suave">
                        {confirmacao.tipo === 'restauracao'
                            ? 'Tudo o que foi feito na demonstração é apagado, e os dados fictícios voltam ao início. As pessoas indicadas continuam, como Dono. Nenhuma outra empresa é tocada.'
                            : `${confirmacao.pessoa.email} deixa de ver a demonstração; a sessão dela lá para na próxima renovação. A conta e a empresa dela não mudam.`}
                    </p>
                    {erroNaJanela && <Alerta>{erroNaJanela}</Alerta>}
                    <div className="flex justify-end gap-3">
                        <Botao variante="secundario" onPress={() => setConfirmacao(null)} isDisabled={acao.isPending}>
                            Voltar
                        </Botao>
                        <Botao
                            onPress={() => acao.mutate(confirmacao)}
                            isPending={acao.isPending}
                            className="!bg-vermelho !text-botao-tinta"
                        >
                            {confirmacao.tipo === 'restauracao' ? 'Restaurar' : 'Tirar da demonstração'}
                        </Botao>
                    </div>
                </Janela>
            )}
        </Pagina>
    )
}

function PessoasIndicadas({
    indicadas,
    aoRetirar,
}: {
    indicadas: readonly PessoaIndicada[]
    aoRetirar: (pessoa: PessoaIndicada) => void
}) {
    return (
        <section aria-labelledby="titulo-indicadas" className="flex flex-col gap-3">
            <h2 id="titulo-indicadas" className="font-titulo text-lg font-semibold">
                Quem entra na demonstração
            </h2>
            {indicadas.length === 0 ? (
                <EstadoVazio titulo="Ninguém indicado ainda" />
            ) : (
                <ul className="flex flex-col divide-y divide-linha rounded-couro border border-linha bg-superficie">
                    {indicadas.map((pessoa) => (
                        <li key={pessoa.usuarioId} className="flex items-center justify-between gap-3 px-4 py-2.5">
                            <span>
                                <span className="block font-semibold">{pessoa.nome}</span>
                                <span className="text-xs text-suave">{pessoa.email}</span>
                            </span>
                            <Botao
                                variante="secundario"
                                onPress={() => aoRetirar(pessoa)}
                                aria-label={`Tirar ${pessoa.nome} da demonstração`}
                            >
                                Tirar
                            </Botao>
                        </li>
                    ))}
                </ul>
            )}
        </section>
    )
}

/**
 * Indicar pelo e-mail de uma conta que já existe (F14-90). Sem conta, a API recusa no campo, sem criar nada nem enviar
 * e-mail (F14-110); já indicada, `422` com a explicação.
 */
function IndicarPessoa({
    aoIndicar,
    aoSumir,
}: {
    aoIndicar: (dados: DadosDemonstracao, email: string) => void
    aoSumir: (erro: unknown) => boolean
}) {
    // Trocar a chave remonta o formulário vazio depois de indicar.
    const [rodada, setRodada] = useState(0)
    const indicacao = useMutation({
        mutationFn: async (email: string) =>
            (await apiNebrys.POST('/api/v1/nebrys/demonstracao/indicacoes', { body: { email } })).data!,
        onSuccess: (dados, email) => {
            setRodada((r) => r + 1)
            indicacao.reset()
            aoIndicar(dados, email)
        },
        onError: (erro) => {
            aoSumir(erro)
        },
    })
    const recusa =
        indicacao.error instanceof ErroApi && indicacao.error.status === 404 ? null : recusaDaAcao(indicacao.error)

    return (
        <section aria-labelledby="titulo-indicar" className="flex flex-col gap-3">
            <h2 id="titulo-indicar" className="font-titulo text-lg font-semibold">
                Indicar uma pessoa
            </h2>
            <p className="max-w-prose text-sm text-suave">
                A pessoa precisa já ter conta no Acordu: ela vê a demonstração na troca de empresa. Nenhum convite nem
                e-mail sai.
            </p>
            <Form
                key={rodada}
                className="flex max-w-md flex-col gap-4"
                validationErrors={errosDeCampo(indicacao.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    indicacao.mutate(String(new FormData(evento.currentTarget).get('email') ?? '').trim())
                }}
            >
                <CampoTexto name="email" type="email" rotulo="E-mail da conta" autoComplete="off" isRequired />
                {recusa && <Alerta>{recusa}</Alerta>}
                <div className="flex justify-end">
                    <Botao type="submit" isPending={indicacao.isPending}>
                        Indicar
                    </Botao>
                </div>
            </Form>
        </section>
    )
}
