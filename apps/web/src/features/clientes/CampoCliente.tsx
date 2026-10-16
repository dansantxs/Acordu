import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import {
    Button,
    ComboBox,
    FieldError,
    Input,
    ListBox,
    ListBoxItem,
    Popover,
    Text,
    type Key,
} from 'react-aria-components'
import { api } from '@/shared/api/cliente'
import { useAtrasado } from '@/shared/hooks/useAtrasado'
import { Icone } from '@/shared/ui/Icone'
import { Rotulo } from '@/shared/ui/Rotulo'
import { consultaCliente } from './consultas'

const LIMITE = 20

/**
 * Escolha de cliente buscando na API enquanto se digita (a lista pode ter centenas): os 20 primeiros que casam com o
 * texto, só ativos. Mesmo comportamento do `CampoSelecao` (focar seleciona, Esc volta, vazio como dica).
 */
export function CampoCliente({
    rotulo,
    valor,
    aoMudar,
    vazio,
    dica,
    descricao,
    name,
    isRequired = false,
    rotuloOculto = false,
    className = '',
}: {
    rotulo: string
    valor: string | null
    aoMudar: (id: string | null) => void
    vazio?: string
    dica?: string
    descricao?: string
    name?: string
    isRequired?: boolean
    rotuloOculto?: boolean
    className?: string
}) {
    // Nome do escolhido (para o texto do campo quando o valor vem do endereço ou do formulário).
    const escolhido = useQuery({ ...consultaCliente(valor ?? ''), enabled: valor !== null })
    const nomeEscolhido = valor ? (escolhido.data?.nome ?? '') : ''
    // Enquanto digita, o campo mostra o texto da busca; fora disso, o nome do escolhido.
    const [texto, setTexto] = useState('')
    const [digitando, setDigitando] = useState(false)
    const busca = useAtrasado(digitando ? texto.trim() : '')

    const resultados = useQuery({
        queryKey: ['clientes', 'escolha', busca],
        queryFn: async () =>
            (
                await api.GET('/api/v1/clientes', {
                    params: { query: { busca: busca || undefined, ativo: true, tamanhoPagina: LIMITE } },
                })
            ).data!.itens,
        placeholderData: keepPreviousData,
        staleTime: 30_000,
    })

    const itens = [
        ...(vazio ? [{ id: '__nenhum__', nome: vazio }] : []),
        ...(resultados.data ?? []).map((c) => ({ id: c.id, nome: c.nome })),
    ]

    return (
        <ComboBox
            name={name}
            items={itens}
            selectedKey={valor}
            inputValue={digitando ? texto : nomeEscolhido}
            onInputChange={(novo) => {
                setTexto(novo)
                setDigitando(true)
            }}
            onSelectionChange={(chave: Key | null) => {
                setDigitando(false)
                if (chave === null) return
                aoMudar(chave === '__nenhum__' ? null : String(chave))
            }}
            onOpenChange={(aberta) => {
                if (!aberta) setDigitando(false)
            }}
            menuTrigger="focus"
            isRequired={isRequired}
            className={`flex flex-col gap-1 ${className}`}
        >
            <Rotulo texto={rotulo} obrigatorio={isRequired} oculto={rotuloOculto} />
            <div className="flex items-center rounded-couro border border-linha bg-superficie focus-within:outline-2 focus-within:outline-acento">
                <Input
                    placeholder={dica ?? vazio ?? 'Buscar cliente'}
                    onFocus={(e) => e.currentTarget.select()}
                    className="w-full min-w-0 bg-transparent px-3 py-2 text-tinta outline-none placeholder:text-suave"
                />
                <Button aria-label={`Abrir ${rotulo}`} className="px-2 py-2 text-suave">
                    <Icone nome="seta" className="size-3.5 rotate-90" />
                </Button>
            </div>
            {descricao && (
                <Text slot="description" className="text-xs text-suave">
                    {descricao}
                </Text>
            )}
            <FieldError className="text-sm text-vermelho" />
            <Popover className="max-h-72 min-w-[--trigger-width] overflow-y-auto rounded-couro border border-linha bg-superficie p-1 shadow-lg">
                <ListBox
                    className="outline-none"
                    renderEmptyState={() => (
                        <p className="px-3 py-2 text-sm text-suave">
                            {resultados.isFetching ? 'Buscando…' : 'Nenhum cliente encontrado.'}
                        </p>
                    )}
                >
                    {(item: { id: string; nome: string }) => (
                        <ListBoxItem
                            id={item.id}
                            textValue={item.nome}
                            className="cursor-default rounded-couro px-3 py-2 text-sm outline-none data-[focused]:bg-trilho data-[selected]:font-semibold"
                        >
                            {item.nome}
                        </ListBoxItem>
                    )}
                </ListBox>
            </Popover>
        </ComboBox>
    )
}
