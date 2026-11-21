import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useState, type ReactNode } from 'react'
import { Form } from 'react-aria-components'
import { seVersao } from '@/shared/api/cliente'
import { ErroApi } from '@/shared/api/problema'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { formatarDocumento } from '@/shared/format/mascaras'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Janela } from '@/shared/ui/Janela'
import { Pagina } from '@/shared/ui/Pagina'
import { apiNebrys, type Empresa } from './api'
import { consultaEmpresa } from './consultas'
import {
    ACOES_POR_SITUACAO,
    data,
    ESTILO_SITUACAO,
    marcoDaSituacao,
    ROTULO_ACAO,
    ROTULO_SITUACAO,
    type Acao,
} from './situacao'

const MUDOU =
    'A empresa mudou desde que você abriu esta tela (o diário ou outra ação). A situação de agora está abaixo: confira e decida de novo.'
const SUMIU = 'Esta empresa não existe mais: pode ter sido expurgada.'

/** O que cada ação faz, dito na janela antes de confirmar (contrato §17). */
const EXPLICACAO: Record<Exclude<Acao, 'prorrogacao'>, string> = {
    ativacao: 'A empresa fica ativa, sem fim de teste, e volta a gravar na próxima requisição da equipe.',
    cancelamento:
        'A empresa fica cancelada e só leitura. O expurgo dos dados fica previsto; até lá, ela pode ser reativada.',
    reativacao: 'A empresa sai do só leitura e fica ativa, sem fim de teste.',
}

const ROTULO_CONFIRMAR: Record<Acao, string> = {
    ativacao: 'Ativar a empresa',
    prorrogacao: 'Prorrogar',
    cancelamento: 'Cancelar a empresa',
    reativacao: 'Reativar a empresa',
}

async function agir(empresa: Empresa, acao: Acao, ate?: string): Promise<Empresa> {
    const path = { id: empresa.id }
    const versao = seVersao(empresa.versao)
    const resposta =
        acao === 'ativacao'
            ? await apiNebrys.POST('/api/v1/nebrys/empresas/{id}/ativacao', { params: { path }, ...versao })
            : acao === 'prorrogacao'
              ? await apiNebrys.POST('/api/v1/nebrys/empresas/{id}/prorrogacao', {
                    params: { path },
                    body: { ate: ate || null },
                    ...versao,
                })
              : acao === 'cancelamento'
                ? await apiNebrys.POST('/api/v1/nebrys/empresas/{id}/cancelamento', { params: { path }, ...versao })
                : await apiNebrys.POST('/api/v1/nebrys/empresas/{id}/reativacao', { params: { path }, ...versao })
    return resposta.data!
}

/**
 * Detalhe de uma empresa (F14-62) e as ações sobre a situação (F14-65 a F14-69, F14-106, F14-107). Cada ação manda o
 * `If-Match` com a `versao` lida (D5): `409` recarrega a empresa e avisa; `422` mostra a explicação da API; `404` diz
 * que a empresa não existe mais.
 */
export function DetalheEmpresa({ id }: { id: string }) {
    const consulta = useQuery(consultaEmpresa(id))
    const [aviso, setAviso] = useState<string | null>(null)

    if (consulta.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (consulta.isError) {
        const naoExiste = consulta.error instanceof ErroApi && consulta.error.status === 404
        return (
            <Pagina titulo={naoExiste ? 'Empresa não encontrada' : 'Empresa'} contexto={<VoltarParaLista />}>
                {naoExiste ? (
                    <EstadoVazio titulo="Empresa não encontrada">{SUMIU}</EstadoVazio>
                ) : (
                    <Alerta>{mensagemDeErro(consulta.error)}</Alerta>
                )}
            </Pagina>
        )
    }
    const e = consulta.data
    const marco = marcoDaSituacao(e)

    return (
        <Pagina titulo={e.nome} contexto={<VoltarParaLista />}>
            {aviso && <Alerta tom="aviso">{aviso}</Alerta>}
            <section aria-labelledby="dados-empresa" className="rounded-couro border border-linha bg-superficie p-6">
                <h2 id="dados-empresa" className="sr-only">
                    Dados da empresa
                </h2>
                <dl className="grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
                    <Dado rotulo="Situação">
                        <span className={`rounded-couro px-2 py-0.5 text-xs ${ESTILO_SITUACAO[e.situacao]}`}>
                            {ROTULO_SITUACAO[e.situacao]}
                        </span>
                        {marco && <span className="mt-1 block text-suave">{marco}</span>}
                    </Dado>
                    <Dado rotulo="CPF ou CNPJ">
                        <span className="tabular-nums">
                            {e.documento ? formatarDocumento(e.documento) : 'Sem documento'}
                        </span>
                    </Dado>
                    <Dado rotulo="Criada em">{data(e.criadaEm)}</Dado>
                    <Dado rotulo="Criada por">
                        {e.criador ? (
                            <>
                                {e.criador.nome}
                                <span className="block text-suave">{e.criador.email}</span>
                            </>
                        ) : (
                            'Conta removida'
                        )}
                    </Dado>
                    <Dado rotulo="Pessoas na equipe">{Number(e.pessoasNaEquipe)}</Dado>
                    <Dado rotulo="Expurgo previsto">{e.expurgoPrevistoEm ? data(e.expurgoPrevistoEm) : '—'}</Dado>
                </dl>
            </section>
            <AcoesEmpresa empresa={e} aoAvisar={setAviso} />
        </Pagina>
    )
}

function VoltarParaLista() {
    return (
        <Link to="/nebrys/empresas" className="underline-offset-4 hover:underline">
            Empresas
        </Link>
    )
}

function Dado({ rotulo, children }: { rotulo: string; children: ReactNode }) {
    return (
        <div className="flex flex-col gap-1">
            <dt className="font-semibold">{rotulo}</dt>
            <dd>{children}</dd>
        </div>
    )
}

function AcoesEmpresa({ empresa, aoAvisar }: { empresa: Empresa; aoAvisar: (texto: string | null) => void }) {
    const consultas = useQueryClient()
    const [aberta, setAberta] = useState<Acao | null>(null)
    const acao = useMutation({
        mutationFn: ({ qual, ate }: { qual: Acao; ate?: string }) => agir(empresa, qual, ate),
        onSuccess: (nova) => {
            consultas.setQueryData(consultaEmpresa(nova.id).queryKey, nova)
            // A lista relê quando voltar a aparecer; o detalhe já está com a versão nova.
            void consultas.invalidateQueries({ queryKey: ['nebrys', 'empresas'], refetchType: 'none' })
            setAberta(null)
            aoAvisar(null)
            avisar(`${nova.nome}: ${ROTULO_SITUACAO[nova.situacao].toLowerCase()}.`)
        },
        onError: (erro) => {
            if (!(erro instanceof ErroApi)) return
            // Versão divergente (F14-107) ou empresa que sumiu (F14-106): a janela fecha, a empresa é lida de novo e
            // o aviso fica no topo. Os demais erros (422, 400) ficam na janela.
            if (erro.status === 409 || erro.status === 404) {
                setAberta(null)
                aoAvisar(erro.status === 409 ? MUDOU : SUMIU)
                void consultas.invalidateQueries({ queryKey: consultaEmpresa(empresa.id).queryKey })
            }
        },
    })
    const acoes = ACOES_POR_SITUACAO[empresa.situacao]
    const abrir = (qual: Acao) => {
        acao.reset()
        setAberta(qual)
    }
    const fechar = (aberto: boolean) => {
        if (!aberto) setAberta(null)
    }
    const erroNaJanela =
        acao.error instanceof ErroApi && acao.error.status !== 409 && acao.error.status !== 404
            ? acao.error.status === 422
                ? (acao.error.detalhe ?? acao.error.message)
                : mensagemDeErro(acao.error)
            : acao.error && !(acao.error instanceof ErroApi)
              ? mensagemDeErro(acao.error)
              : null

    return (
        <section aria-labelledby="acoes-empresa" className="flex flex-col gap-3">
            <h2 id="acoes-empresa" className="font-titulo text-xl font-semibold">
                Situação
            </h2>
            <div className="flex flex-wrap gap-2">
                {acoes.map((qual) => (
                    <Botao
                        key={qual}
                        variante={qual === 'cancelamento' ? 'secundario' : 'primario'}
                        onPress={() => abrir(qual)}
                    >
                        {ROTULO_ACAO[qual]}
                    </Botao>
                ))}
            </div>

            {aberta && aberta !== 'prorrogacao' && (
                <Janela
                    titulo={`${ROTULO_ACAO[aberta]} ${empresa.nome}?`}
                    aberta
                    aoMudar={fechar}
                    ocupada={acao.isPending}
                >
                    <p className="text-sm text-suave">{EXPLICACAO[aberta]}</p>
                    {erroNaJanela && <Alerta>{erroNaJanela}</Alerta>}
                    <div className="flex justify-end gap-3">
                        <Botao variante="secundario" onPress={() => setAberta(null)} isDisabled={acao.isPending}>
                            Voltar
                        </Botao>
                        <Botao
                            onPress={() => acao.mutate({ qual: aberta })}
                            isPending={acao.isPending}
                            className={aberta === 'cancelamento' ? '!bg-vermelho !text-botao-tinta' : undefined}
                        >
                            {ROTULO_CONFIRMAR[aberta]}
                        </Botao>
                    </div>
                </Janela>
            )}

            {aberta === 'prorrogacao' && (
                <Janela
                    titulo={`Prorrogar o teste de ${empresa.nome}`}
                    aberta
                    aoMudar={fechar}
                    ocupada={acao.isPending}
                >
                    <Form
                        className="flex flex-col gap-4"
                        onSubmit={(evento) => {
                            evento.preventDefault()
                            const ate = String(new FormData(evento.currentTarget).get('ate') ?? '')
                            acao.mutate({ qual: 'prorrogacao', ate })
                        }}
                    >
                        <p className="text-sm text-suave">
                            A empresa fica (ou volta a ficar) em teste até o fim do dia escolhido, no fuso dela.
                        </p>
                        <CampoAte erro={acao.error instanceof ErroApi ? acao.error.doCampo('ate') : undefined} />
                        {erroNaJanela && <Alerta>{erroNaJanela}</Alerta>}
                        <div className="flex justify-end gap-3">
                            <Botao variante="secundario" onPress={() => setAberta(null)} isDisabled={acao.isPending}>
                                Voltar
                            </Botao>
                            <Botao type="submit" isPending={acao.isPending}>
                                {ROTULO_CONFIRMAR.prorrogacao}
                            </Botao>
                        </div>
                    </Form>
                </Janela>
            )}
        </section>
    )
}

/** Último dia do teste: `<input type="date">` (convenções §4), com o erro de campo da API (`errors.ate`) embaixo. */
function CampoAte({ erro }: { erro?: string }) {
    return (
        <div className="flex flex-col gap-1">
            <span className="flex items-baseline gap-0.5">
                <label htmlFor="prorrogacao-ate" className="text-sm font-semibold">
                    Último dia do teste
                </label>
                <span aria-hidden="true" className="text-sm font-semibold text-acento-texto">
                    *
                </span>
            </span>
            <input
                id="prorrogacao-ate"
                name="ate"
                type="date"
                required
                aria-invalid={erro ? true : undefined}
                aria-describedby={erro ? 'prorrogacao-ate-erro' : undefined}
                className="rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
            />
            {erro && (
                <p id="prorrogacao-ate-erro" className="text-sm text-vermelho">
                    {erro}
                </p>
            )}
        </div>
    )
}
