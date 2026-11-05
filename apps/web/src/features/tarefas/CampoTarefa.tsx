import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Button, ComboBox, FieldError, Input, ListBox, ListBoxItem, Popover, type Key } from 'react-aria-components'
import { api } from '@/shared/api/cliente'
import { useAtrasado } from '@/shared/hooks/useAtrasado'
import { Icone } from '@/shared/ui/Icone'
import { Rotulo } from '@/shared/ui/Rotulo'
import type { TarefaResumo } from './consultas'

const LIMITE = 20

/**
 * Escolha de tarefa buscando na API pelo título (lançar horas pela tela "Horas trabalhadas"): abertas e concluídas, as 20
 * primeiras. Devolve o resumo inteiro, porque quem usa precisa saber se a tarefa tem cliente.
 */
export function CampoTarefa({
    rotulo,
    valor,
    aoMudar,
    isRequired = false,
}: {
    rotulo: string
    valor: TarefaResumo | null
    aoMudar: (tarefa: TarefaResumo | null) => void
    isRequired?: boolean
}) {
    const [texto, setTexto] = useState('')
    const [digitando, setDigitando] = useState(false)
    const busca = useAtrasado(digitando ? texto.trim() : '')
    const resultados = useQuery({
        queryKey: ['tarefas', 'escolha', busca],
        queryFn: async () =>
            (
                await api.GET('/api/v1/tarefas', {
                    params: {
                        query: {
                            busca: busca || undefined,
                            status: 'A_FAZER,FAZENDO,CONCLUIDA',
                            tamanhoPagina: LIMITE,
                        },
                    },
                })
            ).data!.itens,
        placeholderData: keepPreviousData,
        staleTime: 30_000,
    })
    const itens = resultados.data ?? []

    return (
        <ComboBox
            items={itens}
            selectedKey={valor?.id ?? null}
            inputValue={digitando ? texto : (valor?.titulo ?? '')}
            onInputChange={(novo) => {
                setTexto(novo)
                setDigitando(true)
            }}
            onSelectionChange={(chave: Key | null) => {
                setDigitando(false)
                if (chave === null) return
                aoMudar(itens.find((t) => t.id === chave) ?? null)
            }}
            onOpenChange={(aberta) => {
                if (!aberta) setDigitando(false)
            }}
            menuTrigger="focus"
            isRequired={isRequired}
            className="flex flex-col gap-1"
        >
            <Rotulo texto={rotulo} obrigatorio={isRequired} />
            <div className="flex items-center rounded-couro border border-linha bg-superficie focus-within:outline-2 focus-within:outline-acento">
                <Input
                    placeholder="Buscar pelo título"
                    onFocus={(e) => e.currentTarget.select()}
                    className="w-full min-w-0 bg-transparent px-3 py-2 text-tinta outline-none placeholder:text-suave"
                />
                <Button aria-label={`Abrir ${rotulo}`} className="px-2 py-2 text-suave">
                    <Icone nome="seta" className="size-3.5 rotate-90" />
                </Button>
            </div>
            <FieldError className="text-sm text-vermelho" />
            <Popover className="max-h-72 min-w-[--trigger-width] overflow-y-auto rounded-couro border border-linha bg-superficie p-1 shadow-lg">
                <ListBox
                    className="outline-none"
                    renderEmptyState={() => (
                        <p className="px-3 py-2 text-sm text-suave">
                            {resultados.isFetching ? 'Buscando…' : 'Nenhuma tarefa encontrada.'}
                        </p>
                    )}
                >
                    {(t: TarefaResumo) => (
                        <ListBoxItem
                            id={t.id}
                            textValue={t.titulo}
                            className="flex cursor-default flex-col rounded-couro px-3 py-2 text-sm outline-none data-[focused]:bg-trilho data-[selected]:font-semibold"
                        >
                            <span>{t.titulo}</span>
                            <span className="text-xs text-suave">
                                {[
                                    t.clienteNome,
                                    t.solicitacaoNumero != null ? `#${t.solicitacaoNumero}` : null,
                                    t.projetoNome,
                                ]
                                    .filter(Boolean)
                                    .join(' · ') || 'Interna'}
                            </span>
                        </ListBoxItem>
                    )}
                </ListBox>
            </Popover>
        </ComboBox>
    )
}
