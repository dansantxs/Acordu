import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Radio, RadioGroup } from 'react-aria-components'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { dataHora } from '@/shared/format/prazo'
import { AnexosParaBaixar, CaixaMensagem, TextoComMencoes, type Pessoa } from '@/shared/mensagens/Mensagens'
import { Alerta } from '@/shared/ui/Alerta'
import {
    consultaInteracoes,
    registrarInteracao,
    type Interacao,
    type Solicitacao,
    type Visibilidade,
} from './consultas'
import { consultaHistorico, type Atividade } from './historico'
import { STATUS, type Status } from './rotulos'

/**
 * Conversa da solicitação (RF-SOL-06/07, F2d2): linha do tempo com notas internas e mensagens ao cliente, em texto
 * simples com @menções (plano do F2), e anexos arrastando ou pelo botão.
 */
export function Conversa({
    solicitacao,
    fuso,
    podeComentar,
    podeAguardar = false,
}: {
    solicitacao: Solicitacao
    fuso: string
    podeComentar: boolean
    /** `solicitacao.editar`: além de responder, pôr em AGUARDANDO_CLIENTE (F11-04). */
    podeAguardar?: boolean
}) {
    const interacoes = useQuery(consultaInteracoes(solicitacao.id))
    // O histórico é complemento: se falhar, a conversa aparece sem ele.
    const historico = useQuery(consultaHistorico(solicitacao.id))
    const equipe = useQuery(consultaEquipe)
    const nomes = useMemo(() => new Map((equipe.data ?? []).map((p) => [p.id.toLowerCase(), p.nome])), [equipe.data])

    return (
        <section aria-labelledby="conversa" className="flex flex-col gap-3">
            <h3 id="conversa" className="text-sm font-semibold">
                Conversa
            </h3>
            {interacoes.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : interacoes.isError ? (
                <Alerta>{mensagemDeErro(interacoes.error)}</Alerta>
            ) : interacoes.data.length === 0 && !historico.data?.length ? (
                <p className="text-sm text-suave">Nenhuma mensagem ainda.</p>
            ) : (
                <ol className="flex flex-col gap-3">
                    {linhaDoTempo(interacoes.data, historico.data ?? []).map((item) =>
                        item.tipo === 'mensagem' ? (
                            <Mensagem key={item.valor.id} interacao={item.valor} fuso={fuso} nomes={nomes} />
                        ) : (
                            <Evento key={item.valor.id} atividade={item.valor} fuso={fuso} />
                        ),
                    )}
                </ol>
            )}
            {podeComentar && (
                <Composicao solicitacao={solicitacao} equipe={equipe.data ?? []} podeAguardar={podeAguardar} />
            )}
        </section>
    )
}

type ItemLinha =
    { tipo: 'mensagem'; valor: Interacao; quando: string } | { tipo: 'evento'; valor: Atividade; quando: string }

/** Mensagens e mudanças (abertura, responsável, status) numa linha só, da mais antiga para a mais nova. */
function linhaDoTempo(interacoes: Interacao[], historico: Atividade[]): ItemLinha[] {
    return [
        ...interacoes.map((i) => ({ tipo: 'mensagem' as const, valor: i, quando: i.registradaEm })),
        ...historico.map((a) => ({ tipo: 'evento' as const, valor: a, quando: a.ocorridaEm })),
    ].sort((a, b) => new Date(a.quando).getTime() - new Date(b.quando).getTime())
}

function fraseDoEvento(a: Atividade): string {
    const quem = a.autor ? a.autor.nome || 'Pessoa removida' : 'O sistema'
    const para = a.detalhe?.statusPara as Status | undefined
    switch (a.tipo) {
        case 'SOLICITACAO_ABERTA':
            return `${quem} abriu a solicitação`
        case 'SOLICITACAO_ATRIBUIDA': {
            const responsavel = a.detalhe?.responsavel
            if (!responsavel) return `${quem} tirou o responsável`
            if (a.autor && responsavel.id === a.autor.id) return `${quem} assumiu a solicitação`
            return `${quem} passou para ${responsavel.nome}`
        }
        default:
            return para && STATUS[para] ? `${quem} mudou para ${STATUS[para].toLowerCase()}` : `${quem} mudou o status`
    }
}

function Evento({ atividade: a, fuso }: { atividade: Atividade; fuso: string }) {
    return (
        <li className="flex items-baseline gap-2 px-3 text-xs text-suave">
            <span aria-hidden="true" className="size-1.5 shrink-0 translate-y-[-1px] rounded-full bg-linha" />
            <span>
                {fraseDoEvento(a)} · <time dateTime={a.ocorridaEm}>{dataHora(a.ocorridaEm, fuso)}</time>
            </span>
        </li>
    )
}

function Mensagem({
    interacao: i,
    fuso,
    nomes,
}: {
    interacao: Interacao
    fuso: string
    nomes: ReadonlyMap<string, string>
}) {
    const interna = i.visibilidade === 'INTERNA'
    return (
        <li
            className={`flex flex-col gap-2 rounded-couro border p-3 text-sm ${
                interna ? 'border-ambar/40 bg-ambar-suave' : 'border-linha bg-superficie'
            }`}
        >
            <p className="flex flex-wrap items-baseline gap-x-2 text-xs text-suave">
                <span className="text-sm font-semibold text-tinta">{i.autor.nome || 'Pessoa removida'}</span>
                {i.autorEhCliente && <span>cliente</span>}
                <time dateTime={i.registradaEm}>{dataHora(i.registradaEm, fuso)}</time>
                <span className={interna ? 'font-semibold text-ambar-texto' : ''}>
                    {interna ? 'Nota interna' : i.autorEhCliente ? 'Pelo portal' : 'Enviada ao cliente'}
                </span>
            </p>
            {i.texto && <TextoComMencoes texto={i.texto} nomes={nomes} />}
            <AnexosParaBaixar anexos={i.anexos} />
        </li>
    )
}

/** De onde "Enviar e aguardar o cliente" leva a AGUARDANDO_CLIENTE (F11-04). */
const PODE_AGUARDAR: Solicitacao['status'][] = ['ABERTA', 'EM_ANDAMENTO', 'AGUARDANDO_TERCEIRO']

function Composicao({
    solicitacao,
    equipe,
    podeAguardar,
}: {
    solicitacao: Solicitacao
    equipe: Pessoa[]
    podeAguardar: boolean
}) {
    const consultas = useQueryClient()
    // RF-SOL-06 (F11-01): o padrão é a resposta ao cliente; a nota interna é escolha explícita, com outro visual, e a
    // caixa volta ao padrão depois de cada envio (F11-03).
    const [visibilidade, setVisibilidade] = useState<Visibilidade>('EXTERNA')
    const interna = visibilidade === 'INTERNA'
    const destinatario = solicitacao.contato?.nome
    async function enviar(dados: { texto: string | null; anexos: File[] }, aguardarCliente = false) {
        await registrarInteracao(solicitacao.id, { ...dados, visibilidade, aguardarCliente })
        setVisibilidade('EXTERNA')
        // A resposta ao cliente marca a primeira resposta do SLA e pode mudar o status; a do contato pode retomar.
        await consultas.invalidateQueries({ queryKey: ['solicitacoes'] })
    }
    return (
        <CaixaMensagem
            rotulo={interna ? 'Nota interna' : 'Resposta ao cliente'}
            equipe={equipe}
            comMencoes={interna}
            destaque={interna}
            placeholder={
                interna ? 'Só a equipe vê. Use @ para chamar alguém.' : `Mensagem para ${destinatario ?? 'o cliente'}`
            }
            dica={
                interna
                    ? 'Nota interna: o cliente não vê.'
                    : destinatario
                      ? `${destinatario} recebe por e-mail e vê no portal.`
                      : 'Aparece no portal do cliente (sem contato, ninguém recebe e-mail).'
            }
            rotuloEnviar={interna ? 'Salvar nota interna' : 'Enviar ao cliente'}
            enviar={(dados) => enviar(dados)}
            outraAcao={
                !interna && podeAguardar && PODE_AGUARDAR.includes(solicitacao.status)
                    ? { rotulo: 'Enviar e aguardar o cliente', enviar: (dados) => enviar(dados, true) }
                    : undefined
            }
            topo={
                <RadioGroup
                    aria-label="Tipo de mensagem"
                    orientation="horizontal"
                    value={visibilidade}
                    onChange={(v) => setVisibilidade(v as Visibilidade)}
                    className="flex w-fit gap-1 rounded-couro bg-trilho p-1"
                >
                    {(['EXTERNA', 'INTERNA'] as const).map((v) => (
                        <Radio
                            key={v}
                            value={v}
                            className="cursor-default rounded-couro px-3 py-1 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {v === 'INTERNA' ? 'Nota interna' : 'Resposta ao cliente'}
                        </Radio>
                    ))}
                </RadioGroup>
            }
        />
    )
}
