import { useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { TIPO_CONTRATO } from '@/features/solicitacoes/rotulos'
import { api } from '@/shared/api/cliente'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { fimDoMes, mesAtual, nomeDoMes, somarMeses } from '@/shared/format/datas'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { Janela } from '@/shared/ui/Janela'
import { Marcador } from '@/shared/ui/Marcador'
import { consultaContrato, fecharPeriodo } from './consultas'

type Resultado = { ok: true } | { ok: false; mensagem: string }

/**
 * Fechar o mês de vários contratos (decisão 4 do F5): só no front, um contrato por vez, com o resultado de cada um. A
 * lista traz os contratos cuja vigência cobre o mês (qualquer situação: o domínio só exige mês encerrado e dentro da
 * vigência); os já fechados aparecem sem marcar. Cada fechamento fica na auditoria por conta própria.
 */
export function JanelaFechamentoLote({
    fuso,
    aberta,
    aoMudar,
}: {
    fuso: string
    aberta: boolean
    aoMudar: (aberta: boolean) => void
}) {
    return (
        <Janela titulo="Fechar o mês" aberta={aberta} aoMudar={aoMudar} larga>
            {aberta && <Conteudo fuso={fuso} aoFechar={() => aoMudar(false)} />}
        </Janela>
    )
}

function Conteudo({ fuso, aoFechar }: { fuso: string; aoFechar: () => void }) {
    const consultas = useQueryClient()
    const ultimoPossivel = somarMeses(mesAtual(fuso), -1)
    const [mes, setMes] = useState(ultimoPossivel)
    const [desmarcados, setDesmarcados] = useState<string[]>([])
    const [resultados, setResultados] = useState<Record<string, Resultado>>({})
    const [fechando, setFechando] = useState(false)

    const todos = useQuery({
        queryKey: ['contratos', 'lote'],
        queryFn: async () =>
            (await api.GET('/api/v1/contratos', { params: { query: { tamanhoPagina: 200 } } })).data!.itens,
    })
    const doMes = (todos.data ?? []).filter((c) => c.inicio <= fimDoMes(mes) && (!c.fim || c.fim >= `${mes}-01`))
    const detalhes = useQueries({ queries: doMes.map((c) => consultaContrato(c.id)) })
    const fechado = (id: string) =>
        detalhes.find((d) => d.data?.id === id)?.data?.periodosFechados.includes(`${mes}-01`) ?? false
    const carregando = todos.isPending || detalhes.some((d) => d.isPending)
    const aFechar = doMes.filter((c) => !fechado(c.id) && !desmarcados.includes(c.id) && !resultados[c.id]?.ok)

    const fechar = async () => {
        setFechando(true)
        let certos = 0
        let falhas = 0
        for (const c of aFechar) {
            let resultado: Resultado
            try {
                await fecharPeriodo(c.id, mes)
                resultado = { ok: true }
                certos++
            } catch (erro) {
                resultado = { ok: false, mensagem: mensagemDeErro(erro) ?? 'Não deu para fechar.' }
                falhas++
            }
            setResultados((atual) => ({ ...atual, [c.id]: resultado }))
        }
        await consultas.invalidateQueries({ queryKey: ['contratos'] })
        setFechando(false)
        avisar(
            falhas === 0
                ? `${nomeDoMes(mes)} fechado em ${certos} contrato${certos > 1 ? 's' : ''}.`
                : `${certos} fechado${certos === 1 ? '' : 's'}, ${falhas} com problema: veja na lista.`,
        )
    }

    const mudarMes = (m: string) => {
        setMes(m)
        setDesmarcados([])
        setResultados({})
    }

    return (
        <div className="flex flex-col gap-4">
            <p className="text-sm text-suave">
                Fechado, o mês não aceita apontamentos novos nem edição dos que já existem. Cada contrato é fechado por
                vez; se um falhar, os outros seguem.
            </p>
            <div className="flex items-center gap-2">
                <Botao
                    variante="secundario"
                    aria-label="Mês anterior"
                    isDisabled={fechando}
                    onPress={() => mudarMes(somarMeses(mes, -1))}
                >
                    ‹
                </Botao>
                <span className="min-w-40 text-center font-semibold first-letter:uppercase">{nomeDoMes(mes)}</span>
                <Botao
                    variante="secundario"
                    aria-label="Mês seguinte"
                    isDisabled={fechando || mes >= ultimoPossivel}
                    onPress={() => mudarMes(somarMeses(mes, 1))}
                >
                    ›
                </Botao>
            </div>
            {todos.isError ? (
                <Alerta>{mensagemDeErro(todos.error)}</Alerta>
            ) : carregando ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : doMes.length === 0 ? (
                <p className="text-sm text-suave">Nenhum contrato vigente nesse mês.</p>
            ) : (
                <ul
                    aria-label="Contratos do mês"
                    className="divide-y divide-linha rounded-couro border border-linha text-sm"
                >
                    {doMes.map((c) => {
                        const r = resultados[c.id]
                        const jaFechado = fechado(c.id) || r?.ok === true
                        const nome = `${c.clienteNome} · ${TIPO_CONTRATO[c.tipo]}`
                        return (
                            <li key={c.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-2.5">
                                {jaFechado ? (
                                    <span className="text-suave">{nome}</span>
                                ) : (
                                    <Marcador
                                        valor={!desmarcados.includes(c.id)}
                                        isDisabled={fechando}
                                        aoMudar={(v) =>
                                            setDesmarcados((atual) =>
                                                v ? atual.filter((x) => x !== c.id) : [...atual, c.id],
                                            )
                                        }
                                    >
                                        {nome}
                                    </Marcador>
                                )}
                                <span
                                    className={`text-xs ${r && !r.ok ? 'text-vermelho' : jaFechado ? 'text-verde' : 'text-suave'}`}
                                >
                                    {r && !r.ok ? r.mensagem : jaFechado ? 'fechado' : 'aberto'}
                                </span>
                            </li>
                        )
                    })}
                </ul>
            )}
            <div className="flex justify-end gap-2">
                <Botao variante="secundario" onPress={aoFechar} isDisabled={fechando}>
                    {Object.keys(resultados).length ? 'Concluir' : 'Cancelar'}
                </Botao>
                <Botao
                    onPress={() => void fechar()}
                    isPending={fechando}
                    isDisabled={carregando || aFechar.length === 0}
                >
                    {aFechar.length === 0
                        ? 'Nada para fechar'
                        : `Fechar ${aFechar.length} contrato${aFechar.length > 1 ? 's' : ''}`}
                </Botao>
            </div>
        </div>
    )
}
