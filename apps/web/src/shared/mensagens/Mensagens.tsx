import { useId, useLayoutEffect, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { Button, DropZone, FileTrigger, isFileDropItem, Label, type DropItem } from 'react-aria-components'
import { mensagemDeErro } from '../api/mensagens'
import { baixarAnexo, MAXIMO_ANEXOS, TAMANHO_MAXIMO, tamanhoLegivel } from '../anexos/anexos'
import { avisarErro } from '../avisos/avisos'
import { mencaoEmDigitacao, partesDoTexto, textoComTokens, type Mencionado } from '../format/mencoes'
import { contem } from '../format/texto'
import { Alerta } from '../ui/Alerta'
import { Botao } from '../ui/Botao'
import { Icone } from '../ui/Icone'

/**
 * Peças de mensagem com @menções e anexos (convenções §4), usadas na conversa da solicitação e nos comentários de
 * tarefa: o texto gravado com `@{id}`, a lista de anexos para baixar e a caixa de escrever.
 */
export function TextoComMencoes({ texto, nomes }: { texto: string; nomes: ReadonlyMap<string, string> }) {
    return (
        <p className="break-words whitespace-pre-line">
            {partesDoTexto(texto, nomes).map((parte, n) =>
                parte.tipo === 'texto' ? (
                    parte.texto
                ) : (
                    <span key={n} className="font-semibold text-acento-texto">
                        @{parte.nome ?? 'pessoa que saiu da equipe'}
                    </span>
                ),
            )}
        </p>
    )
}

export interface AnexoBaixavel {
    id: string
    nomeArquivo: string
    tamanhoBytes: number | string
}

/** `baixar`: a rota de download (a da equipe por padrão; o portal passa a dele). */
export function AnexosParaBaixar({
    anexos,
    baixar = baixarAnexo,
}: {
    anexos: AnexoBaixavel[]
    baixar?: (id: string) => Promise<void>
}) {
    if (anexos.length === 0) return null
    return (
        <ul aria-label="Anexos" className="flex flex-wrap gap-2">
            {anexos.map((a) => (
                <li key={a.id}>
                    <Button
                        onPress={() => void baixar(a.id).catch(avisarErro)}
                        className="flex items-center gap-2 rounded-couro border border-linha bg-superficie px-2 py-1 text-xs hover:bg-trilho"
                    >
                        <Icone nome="clipe" className="size-3.5" />
                        <span className="max-w-56 truncate">{a.nomeArquivo}</span>
                        <span className="text-suave">{tamanhoLegivel(Number(a.tamanhoBytes))}</span>
                    </Button>
                </li>
            ))}
        </ul>
    )
}

export interface Pessoa {
    id: string
    nome: string
}

/**
 * Caixa de escrever: texto simples, @ abre a lista da equipe (quando `comMencoes`), anexos pelo botão ou arrastando
 * (5 × 20 MB conferidos antes), Ctrl+Enter envia. `enviar` recebe o texto já com os tokens `@{id}`; a caixa limpa
 * quando ele resolve e mostra o erro quando rejeita.
 */
export function CaixaMensagem({
    rotulo,
    equipe,
    comMencoes,
    destaque = false,
    placeholder,
    dica,
    rotuloEnviar,
    topo,
    enviar,
    outraAcao,
    semAnexos = false,
}: {
    /** Nome acessível do campo (o tipo da mensagem). */
    rotulo: string
    equipe: Pessoa[]
    comMencoes: boolean
    /** Fundo âmbar das notas internas. */
    destaque?: boolean
    placeholder: string
    dica: string
    rotuloEnviar: string
    /** Acima do campo (a escolha entre nota interna e resposta, na solicitação). */
    topo?: ReactNode
    enviar: (dados: { texto: string | null; anexos: File[] }) => Promise<unknown>
    /** Segundo botão, antes do principal, com o mesmo texto e anexos (ex.: "Enviar e aguardar o cliente"). */
    outraAcao?: { rotulo: string; enviar: (dados: { texto: string | null; anexos: File[] }) => Promise<unknown> }
    /** Sem anexos (ex.: solicitação encerrada, F11-09): some o botão, e soltar arquivo não faz nada. */
    semAnexos?: boolean
}) {
    const idLista = useId()
    const campo = useRef<HTMLTextAreaElement>(null)
    // Depois de inserir a menção, o cursor vai para logo após o nome antes da próxima tecla (no layout, não num
    // requestAnimationFrame, que deixava a digitação rápida escapar para o fim do texto).
    const cursorPendente = useRef<number | null>(null)
    const [texto, setTexto] = useState('')
    const [anexos, setAnexos] = useState<File[]>([])
    const [mencionados, setMencionados] = useState<Mencionado[]>([])
    const [busca, setBusca] = useState<string | null>(null)
    const [ativa, setAtiva] = useState(0)
    const [problemaAnexo, setProblemaAnexo] = useState<string | null>(null)
    const [enviando, setEnviando] = useState(false)
    const [erro, setErro] = useState<unknown>(null)

    useLayoutEffect(() => {
        const posicao = cursorPendente.current
        if (posicao === null) return
        cursorPendente.current = null
        campo.current?.focus()
        campo.current?.setSelectionRange(posicao, posicao)
    }, [texto])

    const sugestoes = comMencoes && busca !== null ? equipe.filter((p) => contem(p.nome, busca)).slice(0, 6) : []
    const listaAberta = sugestoes.length > 0

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
        adicionar(await Promise.all(itens.filter(isFileDropItem).map((item) => item.getFile())))
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
        setTexto(antes + texto.slice(cursor))
        setMencionados((atuais) => (atuais.some((m) => m.id === pessoa.id) ? atuais : [...atuais, pessoa]))
        setBusca(null)
        cursorPendente.current = antes.length
    }

    const vazio = !texto.trim() && anexos.length === 0
    async function mandar(acao = enviar) {
        if (vazio || enviando) return
        setEnviando(true)
        setErro(null)
        try {
            await acao({ texto: textoComTokens(texto, comMencoes ? mencionados : []).trim() || null, anexos })
            setTexto('')
            setAnexos([])
            setMencionados([])
            setBusca(null)
        } catch (e) {
            setErro(e)
        } finally {
            setEnviando(false)
        }
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
            void mandar()
        }
    }

    return (
        <DropZone
            onDrop={semAnexos ? undefined : (e) => void aoSoltar(e.items)}
            aria-label="Soltar arquivos para anexar"
            className="flex flex-col gap-3 rounded-couro border border-linha bg-superficie p-3 outline-none data-[drop-target]:border-acento data-[drop-target]:bg-acento-suave"
        >
            {topo}
            <div className="relative flex flex-col gap-1">
                <Label htmlFor={`${idLista}-campo`} className="sr-only">
                    {rotulo}
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
                    // Só com menções o campo é uma combobox (a lista da equipe); sem elas, um campo de texto comum.
                    role={comMencoes ? 'combobox' : undefined}
                    aria-expanded={comMencoes ? listaAberta : undefined}
                    aria-controls={listaAberta ? `${idLista}-lista` : undefined}
                    aria-activedescendant={listaAberta ? `${idLista}-${ativa}` : undefined}
                    aria-autocomplete={comMencoes ? 'list' : undefined}
                    placeholder={placeholder}
                    className={`resize-y rounded-couro border px-3 py-2 text-sm text-tinta placeholder:text-suave ${
                        destaque ? 'border-ambar/40 bg-ambar-suave/40' : 'border-linha bg-superficie'
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
                <p className="text-xs text-suave">{dica} Ctrl+Enter envia.</p>
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
            {erro !== null && <Alerta>{mensagemDeErro(erro) ?? 'Confira o texto.'}</Alerta>}

            <div className="flex flex-wrap items-center gap-3">
                {!semAnexos && (
                    <>
                        <FileTrigger allowsMultiple onSelect={(lista) => lista && adicionar([...lista])}>
                            <Botao variante="discreto" className="!px-0">
                                <Icone nome="clipe" />
                                Anexar
                            </Botao>
                        </FileTrigger>
                        <span className="text-xs text-suave">ou arraste até aqui (até 5, de 20 MB)</span>
                    </>
                )}
                {outraAcao && (
                    <Botao
                        variante="secundario"
                        onPress={() => void mandar(outraAcao.enviar)}
                        isDisabled={vazio || enviando}
                        className="ml-auto"
                    >
                        {outraAcao.rotulo}
                    </Botao>
                )}
                <Botao
                    onPress={() => void mandar()}
                    isDisabled={vazio}
                    isPending={enviando}
                    className={outraAcao ? '' : 'ml-auto'}
                >
                    {rotuloEnviar}
                </Botao>
            </div>
        </DropZone>
    )
}
