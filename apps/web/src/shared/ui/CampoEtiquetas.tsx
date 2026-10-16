import { useId, useState } from 'react'
import { Button, Input, Label, Tag, TagGroup, TagList, Text } from 'react-aria-components'
import { Icone } from './Icone'

/**
 * Etiquetas livres: Enter ou vírgula adiciona, o X (ou Backspace com o campo vazio) tira. As já usadas na empresa
 * aparecem como sugestão. Repetida sem diferenciar maiúsculas não entra de novo (a API faz o mesmo).
 */
export function CampoEtiquetas({
    rotulo,
    valor,
    aoMudar,
    sugestoes,
    maximo,
    descricao,
    erro,
}: {
    rotulo: string
    valor: string[]
    aoMudar: (etiquetas: string[]) => void
    sugestoes: string[]
    maximo: number
    descricao?: string
    /** Erro da API em `etiquetas`. */
    erro?: string
}) {
    const [texto, setTexto] = useState('')
    const idLista = useId()
    const idCampo = useId()
    const cheia = valor.length >= maximo

    function adicionar(bruto: string) {
        const etiqueta = bruto.replace(/\s+/g, ' ').trim().slice(0, 30)
        setTexto('')
        if (!etiqueta || cheia || valor.some((e) => e.toLowerCase() === etiqueta.toLowerCase())) return
        aoMudar([...valor, etiqueta])
    }

    return (
        <div className="flex flex-col gap-1">
            <Label htmlFor={idCampo} className="text-sm font-semibold text-tinta">
                {rotulo}
            </Label>
            <div className="flex flex-wrap items-center gap-1.5 rounded-couro border border-linha bg-superficie px-2 py-1.5">
                {valor.length > 0 && (
                    <TagGroup
                        aria-label={rotulo}
                        onRemove={(chaves) => aoMudar(valor.filter((e) => !chaves.has(e)))}
                        className="contents"
                    >
                        <TagList className="contents">
                            {valor.map((etiqueta) => (
                                <Tag
                                    key={etiqueta}
                                    id={etiqueta}
                                    textValue={etiqueta}
                                    className="flex items-center gap-1 rounded-couro bg-acento-suave px-2 py-0.5 text-sm text-acento-texto outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento"
                                >
                                    {etiqueta}
                                    <Button
                                        slot="remove"
                                        aria-label={`Tirar ${etiqueta}`}
                                        className="rounded-full hover:bg-acento/20"
                                    >
                                        <Icone nome="fechar" className="size-3" />
                                    </Button>
                                </Tag>
                            ))}
                        </TagList>
                    </TagGroup>
                )}
                <Input
                    id={idCampo}
                    list={idLista}
                    value={texto}
                    disabled={cheia}
                    placeholder={cheia ? `Limite de ${maximo}` : 'Digite e tecle Enter'}
                    onChange={(e) => {
                        const v = e.target.value
                        if (v.endsWith(',')) adicionar(v.slice(0, -1))
                        else setTexto(v)
                    }}
                    onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                            e.preventDefault()
                            adicionar(texto)
                        } else if (e.key === 'Backspace' && texto === '' && valor.length > 0) {
                            aoMudar(valor.slice(0, -1))
                        }
                    }}
                    onBlur={() => texto.trim() && adicionar(texto)}
                    className="min-w-32 flex-1 bg-transparent px-1 py-0.5 text-tinta outline-none placeholder:text-suave"
                />
                <datalist id={idLista}>
                    {sugestoes
                        .filter((s) => !valor.some((e) => e.toLowerCase() === s.toLowerCase()))
                        .map((s) => (
                            <option key={s} value={s} />
                        ))}
                </datalist>
            </div>
            {descricao && <Text className="text-xs text-suave">{descricao}</Text>}
            {erro && <p className="text-sm text-vermelho">{erro}</p>}
        </div>
    )
}
