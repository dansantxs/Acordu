import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useState, type ReactNode } from 'react'
import { ErroApi } from '@/shared/api/problema'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Janela } from '@/shared/ui/Janela'
import { Pagina } from '@/shared/ui/Pagina'
import { apiNebrys, type Convite } from './api'
import { consultaConvite } from './consultas'
import {
    ACOES_CONVITE,
    dataHora,
    EMAIL_NAO_SAIU,
    ESTILO_SITUACAO_CONVITE,
    marcoDoConvite,
    recusaDaAcao,
    ROTULO_SITUACAO_CONVITE,
    type AcaoConvite,
} from './convites'

const SUMIU = 'Este convite não existe mais.'

const ROTULO_ACAO: Record<AcaoConvite, string> = { reenvio: 'Reenviar', revogacao: 'Revogar' }

const ROTULO_CONFIRMAR: Record<AcaoConvite, string> = {
    reenvio: 'Reenviar o convite',
    revogacao: 'Revogar o convite',
}

/** O que cada ação faz, dito na janela antes de confirmar (contrato §17). */
const EXPLICACAO: Record<AcaoConvite, string> = {
    reenvio: 'Sai um e-mail com um link novo, que vale por mais 7 dias. O link anterior para de valer.',
    revogacao:
        'O link para de valer, e o cadastro com ele é recusado. Para convidar este e-mail de novo, emita um convite novo.',
}

async function agir(convite: Convite, acao: AcaoConvite): Promise<{ convite: Convite; emailEnviado: boolean }> {
    const params = { path: { id: convite.id } }
    if (acao === 'reenvio') return (await apiNebrys.POST('/api/v1/nebrys/convites/{id}/reenvio', { params })).data!
    const revogado = (await apiNebrys.POST('/api/v1/nebrys/convites/{id}/revogacao', { params })).data!
    return { convite: revogado, emailEnviado: true }
}

/**
 * Um convite de empresa (F14-73, F14-74): a situação e as datas, e as ações que valem nela. Reenviar troca o link e
 * reinicia a validade; revogar faz o link parar de valer. `422` (o convite mudou: foi usado ou revogado) fica na janela;
 * `404` diz que o convite não existe mais.
 */
export function DetalheConvite({ id }: { id: string }) {
    const consulta = useQuery(consultaConvite(id))
    const [aviso, setAviso] = useState<string | null>(null)

    if (consulta.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (consulta.isError) {
        const naoExiste = consulta.error instanceof ErroApi && consulta.error.status === 404
        return (
            <Pagina titulo={naoExiste ? 'Convite não encontrado' : 'Convite'} contexto={<VoltarParaConvites />}>
                {naoExiste ? (
                    <EstadoVazio titulo="Convite não encontrado">{SUMIU}</EstadoVazio>
                ) : (
                    <Alerta>{mensagemDeErro(consulta.error)}</Alerta>
                )}
            </Pagina>
        )
    }
    const c = consulta.data

    return (
        <Pagina titulo={c.email} contexto={<VoltarParaConvites />}>
            {aviso && <Alerta tom="aviso">{aviso}</Alerta>}
            <section aria-labelledby="dados-convite" className="rounded-couro border border-linha bg-superficie p-6">
                <h2 id="dados-convite" className="sr-only">
                    Dados do convite
                </h2>
                <dl className="grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
                    <Dado rotulo="Situação">
                        <span className={`rounded-couro px-2 py-0.5 text-xs ${ESTILO_SITUACAO_CONVITE[c.situacao]}`}>
                            {ROTULO_SITUACAO_CONVITE[c.situacao]}
                        </span>
                        <span className="mt-1 block text-suave">{marcoDoConvite(c)}</span>
                    </Dado>
                    <Dado rotulo="Emitido em">{dataHora(c.criadoEm)}</Dado>
                    <Dado rotulo="Último envio">{dataHora(c.enviadoEm)}</Dado>
                </dl>
            </section>
            <AcoesConvite convite={c} aoAvisar={setAviso} />
        </Pagina>
    )
}

function VoltarParaConvites() {
    return (
        <Link to="/nebrys/convites" className="underline-offset-4 hover:underline">
            Convites
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

function AcoesConvite({ convite, aoAvisar }: { convite: Convite; aoAvisar: (texto: string | null) => void }) {
    const consultas = useQueryClient()
    const [aberta, setAberta] = useState<AcaoConvite | null>(null)
    const acao = useMutation({
        mutationFn: (qual: AcaoConvite) => agir(convite, qual),
        onSuccess: (resultado, qual) => {
            consultas.setQueryData(consultaConvite(convite.id).queryKey, resultado.convite)
            void consultas.invalidateQueries({ queryKey: ['nebrys', 'convites'], refetchType: 'none' })
            setAberta(null)
            if (!resultado.emailEnviado) {
                aoAvisar(EMAIL_NAO_SAIU)
                return
            }
            aoAvisar(null)
            avisar(qual === 'reenvio' ? `Convite reenviado para ${convite.email}.` : 'Convite revogado.')
        },
        onError: (erro) => {
            // O convite sumiu: a janela fecha, o detalhe é lido de novo e o aviso fica no topo. `422` fica na janela.
            if (erro instanceof ErroApi && erro.status === 404) {
                setAberta(null)
                aoAvisar(SUMIU)
                void consultas.invalidateQueries({ queryKey: consultaConvite(convite.id).queryKey })
            }
        },
    })
    const acoes = ACOES_CONVITE[convite.situacao]
    if (acoes.length === 0) return null
    const erroNaJanela = acao.error instanceof ErroApi && acao.error.status === 404 ? null : recusaDaAcao(acao.error)

    return (
        <section aria-labelledby="acoes-convite" className="flex flex-col gap-3">
            <h2 id="acoes-convite" className="font-titulo text-xl font-semibold">
                Ações
            </h2>
            <div className="flex flex-wrap gap-2">
                {acoes.map((qual) => (
                    <Botao
                        key={qual}
                        variante={qual === 'revogacao' ? 'secundario' : 'primario'}
                        onPress={() => {
                            acao.reset()
                            setAberta(qual)
                        }}
                    >
                        {ROTULO_ACAO[qual]}
                    </Botao>
                ))}
            </div>
            {aberta && (
                <Janela
                    titulo={`${ROTULO_ACAO[aberta]} o convite de ${convite.email}?`}
                    aberta
                    aoMudar={(aberto) => !aberto && setAberta(null)}
                    ocupada={acao.isPending}
                >
                    <p className="text-sm text-suave">{EXPLICACAO[aberta]}</p>
                    {erroNaJanela && <Alerta>{erroNaJanela}</Alerta>}
                    <div className="flex justify-end gap-3">
                        <Botao variante="secundario" onPress={() => setAberta(null)} isDisabled={acao.isPending}>
                            Voltar
                        </Botao>
                        <Botao
                            onPress={() => acao.mutate(aberta)}
                            isPending={acao.isPending}
                            className={aberta === 'revogacao' ? '!bg-vermelho !text-botao-tinta' : undefined}
                        >
                            {ROTULO_CONFIRMAR[aberta]}
                        </Botao>
                    </div>
                </Janela>
            )}
        </section>
    )
}
