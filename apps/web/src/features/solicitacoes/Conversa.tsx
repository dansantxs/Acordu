import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useId, useLayoutEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import {
    Button,
    DropZone,
    FileTrigger,
    isFileDropItem,
    Label,
    Radio,
    RadioGroup,
    type DropItem,
} from 'react-aria-components'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { baixarAnexo, MAXIMO_ANEXOS, TAMANHO_MAXIMO, tamanhoLegivel } from '@/shared/anexos/anexos'
import { avisarErro } from '@/shared/avisos/avisos'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { mencaoEmDigitacao, partesDoTexto, textoComTokens, type Mencionado } from '@/shared/format/mencoes'
import { dataHora } from '@/shared/format/prazo'
import { contem } from '@/shared/format/texto'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { Icone } from '@/shared/ui/Icone'
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
}: {
    solicitacao: Solicitacao
    fuso: string
    podeComentar: boolean
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
            {podeComentar && <Composicao solicitacao={solicitacao} equipe={equipe.data ?? []} />}
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
            {i.texto && (
                <p className="break-words whitespace-pre-line">
                    {partesDoTexto(i.texto, nomes).map((parte, n) =>
                        parte.tipo === 'texto' ? (
                            parte.texto
                        ) : (
                            <span key={n} className="font-semibold text-acento-texto">
                                @{parte.nome ?? 'pessoa que saiu da equipe'}
                            </span>
                        ),
                    )}
                </p>
            )}
            {i.anexos.length > 0 && (
                <ul aria-label="Anexos" className="flex flex-wrap gap-2">
                    {i.anexos.map((a) => (
                        <li key={a.id}>
                            <Button
                                onPress={() => void baixarAnexo(a.id).catch(avisarErro)}
                                className="flex items-center gap-2 rounded-couro border border-linha bg-superficie px-2 py-1 text-xs hover:bg-trilho"
                            >
                                <Icone nome="clipe" className="size-3.5" />
                                <span className="max-w-56 truncate">{a.nomeArquivo}</span>
                                <span className="text-suave">{tamanhoLegivel(Number(a.tamanhoBytes))}</span>
                            </Button>
                        </li>
                    ))}
                </ul>
            )}
        </li>
    )
}

interface Pessoa {
    id: string
    nome: string
}

function Composicao({ solicitacao, equipe }: { solicitacao: Solicitacao; equipe: Pessoa[] }) {
    const consultas = useQueryClient()
    const idLista = useId()
    const campo = useRef<HTMLTextAreaElement>(null)
    // Depois de inserir a menção, o cursor vai para logo após o nome antes da próxima tecla (no layout, não num
    // requestAnimationFrame, que deixava a digitação rápida escapar para o fim do texto).
    const cursorPendente = useRef<number | null>(null)
    // RF-SOL-06: o padrão é INTERNA; mandar ao cliente é escolha explícita.
    const [visibilidade, setVisibilidade] = useState<Visibilidade>('INTERNA')
    const [texto, setTexto] = useState('')
    const [anexos, setAnexos] = useState<File[]>([])
    const [mencionados, setMencionados] = useState<Mencionado[]>([])
    const [busca, setBusca] = useState<string | null>(null)
    const [ativa, setAtiva] = useState(0)
    const [problemaAnexo, setProblemaAnexo] = useState<string | null>(null)

    useLayoutEffect(() => {
        const posicao = cursorPendente.current
        if (posicao === null) return
        cursorPendente.current = null
        campo.current?.focus()
        campo.current?.setSelectionRange(posicao, posicao)
    }, [texto])

    const interna = visibilidade === 'INTERNA'
    const sugestoes = interna && busca !== null ? equipe.filter((p) => contem(p.nome, busca)).slice(0, 6) : []
    const listaAberta = sugestoes.length > 0

    const envio = useMutation({
        mutationFn: () =>
            registrarInteracao(solicitacao.id, {
                texto: textoComTokens(texto, interna ? mencionados : []).trim() || null,
                visibilidade,
                anexos,
            }),
        onSuccess: async () => {
            setTexto('')
            setAnexos([])
            setMencionados([])
            setBusca(null)
            // A primeira mensagem ao cliente marca a primeira resposta do SLA; a resposta do contato pode retomar.
            await consultas.invalidateQueries({ queryKey: ['solicitacoes'] })
        },
    })

    function adicionar(arquivos: File[]) {
        setProblemaAnexo(null)
        const grandes = arquivos.filter((a) => a.size > TAMANHO_MAXIMO)
        const aceitos = arquivos.filter((a) => a.size <= TAMANHO_MAXIMO)
        const juntos = [...anexos, ...aceitos]
        if (grandes.length > 0)
            setProblemaAnexo(`${grandes.map((a) => a.name).join(', ')}: acima de 20 MB, não dá para anexar.`)
        else if (juntos.length > MAXIMO_ANEXOS) setProblemaAnexo(`No máximo ${MAXIMO_ANEXOS} arquivos por mensagem.`)
        setAnexos(juntos.slice(0, MAXIMO_ANEXOS))
    }

    async function aoSoltar(itens: DropItem[]) {
        const arquivos = await Promise.all(itens.filter(isFileDropItem).map((item) => item.getFile()))
        adicionar(arquivos)
    }

    function aoDigitar(valor: string) {
        setTexto(valor)
        const cursor = campo.current?.selectionStart ?? valor.length
        setBusca(mencaoEmDigitacao(valor.slice(0, cursor)))
        setAtiva(0)
    }

    function mencionar(pessoa: Pessoa) {
        const cursor = campo.current?.selectionStart ?? texto.length
        const antes = texto.slice(0, cursor).replace(/@[\p{L}\p{N}._-]*$/u, `@${pessoa.nome} `)
        const novo = antes + texto.slice(cursor)
        setTexto(novo)
        setMencionados((atuais) => (atuais.some((m) => m.id === pessoa.id) ? atuais : [...atuais, pessoa]))
        setBusca(null)
        cursorPendente.current = antes.length
    }

    function aoTeclar(evento: KeyboardEvent<HTMLTextAreaElement>) {
        if (listaAberta) {
            if (evento.key === 'ArrowDown' || evento.key === 'ArrowUp') {
                evento.preventDefault()
                const passo = evento.key === 'ArrowDown' ? 1 : -1
                setAtiva((a) => (a + passo + sugestoes.length) % sugestoes.length)
                return
            }
            if (evento.key === 'Enter' || evento.key === 'Tab') {
                evento.preventDefault()
                mencionar(sugestoes[ativa]!)
                return
            }
            if (evento.key === 'Escape') {
                // Fecha a lista, não o painel.
                evento.preventDefault()
                setBusca(null)
                return
            }
        }
        if (evento.key === 'Enter' && (evento.ctrlKey || evento.metaKey)) {
            evento.preventDefault()
            enviar()
        }
    }

    const vazio = !texto.trim() && anexos.length === 0
    function enviar() {
        if (vazio || envio.isPending) return
        envio.mutate()
    }

    const destinatario = solicitacao.contato?.nome
    return (
        <DropZone
            onDrop={(e) => void aoSoltar(e.items)}
            aria-label="Soltar arquivos para anexar"
            className="flex flex-col gap-3 rounded-couro border border-linha bg-superficie p-3 outline-none data-[drop-target]:border-acento data-[drop-target]:bg-acento-suave"
        >
            <RadioGroup
                aria-label="Tipo de mensagem"
                orientation="horizontal"
                value={visibilidade}
                onChange={(v) => setVisibilidade(v as Visibilidade)}
                className="flex w-fit gap-1 rounded-couro bg-trilho p-1"
            >
                {(['INTERNA', 'EXTERNA'] as const).map((v) => (
                    <Radio
                        key={v}
                        value={v}
                        className="cursor-default rounded-couro px-3 py-1 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                    >
                        {v === 'INTERNA' ? 'Nota interna' : 'Resposta ao cliente'}
                    </Radio>
                ))}
            </RadioGroup>

            <div className="relative flex flex-col gap-1">
                <Label htmlFor={`${idLista}-campo`} className="sr-only">
                    {interna ? 'Nota interna' : 'Resposta ao cliente'}
                </Label>
                <textarea
                    id={`${idLista}-campo`}
                    ref={campo}
                    rows={3}
                    value={texto}
                    maxLength={10_000}
                    onChange={(e) => aoDigitar(e.target.value)}
                    onKeyDown={aoTeclar}
                    onBlur={() => setBusca(null)}
                    role="combobox"
                    aria-expanded={listaAberta}
                    aria-controls={listaAberta ? `${idLista}-lista` : undefined}
                    aria-activedescendant={listaAberta ? `${idLista}-${ativa}` : undefined}
                    aria-autocomplete="list"
                    placeholder={
                        interna
                            ? 'Só a equipe vê. Use @ para chamar alguém.'
                            : `Mensagem para ${destinatario ?? 'o cliente'}`
                    }
                    className={`resize-y rounded-couro border px-3 py-2 text-sm text-tinta placeholder:text-suave ${
                        interna ? 'border-ambar/40 bg-ambar-suave/40' : 'border-linha bg-superficie'
                    }`}
                />
                {listaAberta && (
                    <ul
                        id={`${idLista}-lista`}
                        role="listbox"
                        aria-label="Pessoas da equipe"
                        className="absolute top-full left-0 z-10 mt-1 w-64 rounded-couro border border-linha bg-superficie py-1 shadow-lg"
                    >
                        {sugestoes.map((p, n) => (
                            <li
                                key={p.id}
                                id={`${idLista}-${n}`}
                                role="option"
                                aria-selected={n === ativa}
                                // mousedown: escolhe antes do blur fechar a lista.
                                onMouseDown={(e) => {
                                    e.preventDefault()
                                    mencionar(p)
                                }}
                                className="cursor-default px-3 py-1.5 text-sm aria-selected:bg-acento-suave"
                            >
                                {p.nome}
                            </li>
                        ))}
                    </ul>
                )}
                <p className="text-xs text-suave">
                    {interna
                        ? 'Nota interna: o cliente não vê. Ctrl+Enter envia.'
                        : destinatario
                          ? `${destinatario} recebe por e-mail e vê no portal. Ctrl+Enter envia.`
                          : 'Aparece no portal do cliente (sem contato, ninguém recebe e-mail). Ctrl+Enter envia.'}
                </p>
            </div>

            {anexos.length > 0 && (
                <ul aria-label="Arquivos para enviar" className="flex flex-wrap gap-2">
                    {anexos.map((a, n) => (
                        <li
                            key={`${a.name}-${n}`}
                            className="flex items-center gap-2 rounded-couro border border-linha px-2 py-1 text-xs"
                        >
                            <span className="max-w-48 truncate">{a.name}</span>
                            <span className="text-suave">{tamanhoLegivel(a.size)}</span>
                            <Button
                                aria-label={`Tirar ${a.name}`}
                                onPress={() => setAnexos((atuais) => atuais.filter((_, i) => i !== n))}
                                className="grid size-5 place-items-center rounded-couro text-suave hover:bg-trilho hover:text-tinta"
                            >
                                <Icone nome="fechar" className="size-3" />
                            </Button>
                        </li>
                    ))}
                </ul>
            )}
            {problemaAnexo && <Alerta tom="aviso">{problemaAnexo}</Alerta>}
            {envio.isError && <Alerta>{mensagemDeErro(envio.error) ?? 'Confira o texto.'}</Alerta>}

            <div className="flex flex-wrap items-center gap-3">
                <FileTrigger allowsMultiple onSelect={(lista) => lista && adicionar([...lista])}>
                    <Botao variante="discreto" className="!px-0">
                        <Icone nome="clipe" />
                        Anexar
                    </Botao>
                </FileTrigger>
                <span className="text-xs text-suave">ou arraste até aqui (até 5, de 20 MB)</span>
                <Botao onPress={enviar} isDisabled={vazio} isPending={envio.isPending} className="ml-auto">
                    {interna ? 'Registrar nota' : 'Enviar ao cliente'}
                </Botao>
            </div>
        </DropZone>
    )
}
