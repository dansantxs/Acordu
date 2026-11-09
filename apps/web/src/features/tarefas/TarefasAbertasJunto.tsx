import { useState } from 'react'
import { Marcador } from '@/shared/ui/Marcador'
import type { TarefasAbertas } from './consultas'

export type TarefaAberta = { id: string; titulo: string }

/**
 * Tarefas abertas na hora de resolver ou cancelar a solicitação (F11-11, F11-14, F11-20) ou de concluir ou cancelar o
 * projeto (F11-27): com `tarefa.editar`, a ação conjunta vem marcada; sem, a lista fica só como aviso. Devolve o pedido para a API (as tarefas que a pessoa viu, F11-31).
 */
export function useTarefasAbertas(
    tarefas: TarefaAberta[],
    podeMexer: boolean,
    acao: 'CONCLUIR' | 'CANCELAR',
    aberta: boolean,
) {
    const [junto, setJunto] = useState(true)
    // A janela fica montada fechada: cada abertura volta ao padrão (marcado), mesmo que a anterior tenha desmarcado.
    const [abertaAntes, setAbertaAntes] = useState(aberta)
    if (aberta !== abertaAntes) {
        setAbertaAntes(aberta)
        if (aberta) setJunto(true)
    }
    const pedido: TarefasAbertas | undefined =
        tarefas.length > 0 && podeMexer ? { acao: junto ? acao : 'MANTER', ids: tarefas.map((t) => t.id) } : undefined
    const quadro =
        tarefas.length === 0 ? null : (
            <section aria-label="Tarefas abertas" className="flex flex-col gap-2 rounded-couro border border-linha p-3">
                <p className="text-sm">
                    {tarefas.length === 1
                        ? 'Uma tarefa ainda está aberta:'
                        : `${tarefas.length} tarefas ainda estão abertas:`}
                </p>
                <ul className="list-disc pl-5 text-sm text-suave">
                    {tarefas.map((t) => (
                        <li key={t.id}>{t.titulo}</li>
                    ))}
                </ul>
                {podeMexer ? (
                    <Marcador valor={junto} aoMudar={setJunto}>
                        {acao === 'CONCLUIR'
                            ? 'Concluir as tarefas abertas junto'
                            : 'Cancelar as tarefas abertas junto'}
                    </Marcador>
                ) : (
                    <p className="text-xs text-suave">Elas ficam como estão: mudar tarefas pede outra permissão.</p>
                )}
            </section>
        )
    return { pedido, quadro }
}
