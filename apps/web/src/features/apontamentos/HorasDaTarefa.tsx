import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { pode } from '@/features/casca/navegacao'
import { mensagemDeErro } from '@/shared/api/mensagens'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { JanelaApontamento, LinhasApontamento, type TarefaDoApontamento } from './Apontamentos'
import { consultaApontamentos } from './consultas'

/** Horas lançadas na tarefa (contrato §9), no painel dela, com "Lançar horas" para quem aponta. */
export function HorasDaTarefa({
    tarefa,
    perfil,
    aceitaLancamento,
}: {
    tarefa: TarefaDoApontamento
    perfil: Perfil
    /** Tarefa cancelada não recebe horas novas. */
    aceitaLancamento: boolean
}) {
    const podeVer = pode(perfil, 'apontamento.ver')
    const podeCriar = pode(perfil, 'apontamento.criar')
    const apontamentos = useQuery({ ...consultaApontamentos({ tarefaId: tarefa.id }), enabled: podeVer })
    const [lancando, setLancando] = useState(false)
    if (!podeVer && !podeCriar) return null

    return (
        <section aria-labelledby="horas-tarefa" className="flex flex-col gap-2">
            <h3 id="horas-tarefa" className="flex items-baseline justify-between text-sm font-semibold">
                Horas
                {podeCriar && aceitaLancamento && (
                    <Botao grava variante="discreto" onPress={() => setLancando(true)} className="!p-0">
                        Lançar horas
                    </Botao>
                )}
            </h3>
            {podeVer &&
                (apontamentos.isPending ? (
                    <p className="text-sm text-suave">Carregando…</p>
                ) : apontamentos.isError ? (
                    <Alerta>{mensagemDeErro(apontamentos.error)}</Alerta>
                ) : apontamentos.data.itens.length === 0 ? (
                    <p className="text-sm text-suave">Nenhuma hora lançada.</p>
                ) : (
                    <LinhasApontamento
                        itens={apontamentos.data.itens}
                        fuso={perfil.tenant.fusoHorario}
                        usuarioId={perfil.usuarioId}
                        podeEditarTodos={pode(perfil, 'apontamento.editar_todos')}
                        podeCriar={podeCriar}
                        mostrarPessoa
                    />
                ))}
            <JanelaApontamento
                fuso={perfil.tenant.fusoHorario}
                aberta={lancando}
                aoMudar={setLancando}
                tarefa={tarefa}
            />
        </section>
    )
}
