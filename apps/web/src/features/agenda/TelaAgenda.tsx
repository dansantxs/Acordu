import { useQuery } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { Tab, TabList, Tabs } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { dataCurta, hojeNoFuso, segundaDaSemana, somarDias } from '@/shared/format/datas'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Pagina } from '@/shared/ui/Pagina'
import { consultaAgenda, consultaCapacidade, type FiltrosAgenda } from './consultas'
import { LinhaDoTempo, ListaPorDia, montarFaixas } from './LinhaDoTempo'

/**
 * Agenda da equipe (F6c): linha do tempo de duas semanas úteis. A janela e o agrupamento ficam no endereço; o plano da
 * semana (arrastar para planejar) chega no F6d.
 */
export function TelaAgenda({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosAgenda }) {
    const navegar = useNavigate()
    const hoje = hojeNoFuso(perfil.tenant.fusoHorario)
    const estaSemana = segundaDaSemana(hoje)
    const inicio = filtros.inicio ?? estaSemana
    const agenda = useQuery(consultaAgenda(inicio))
    const verCarga = pode(perfil, 'tarefa.ver')
    const semana1 = useQuery({ ...consultaCapacidade(inicio), enabled: verCarga })
    const semana2 = useQuery({ ...consultaCapacidade(somarDias(inicio, 7)), enabled: verCarga })
    const porCliente = filtros.faixas === 'cliente'
    const ir = (mudanca: Partial<FiltrosAgenda>) =>
        void navegar({ to: '/agenda', search: (atual: FiltrosAgenda) => ({ ...atual, ...mudanca }), replace: true })
    const mover = (dias: number) => {
        const novo = somarDias(inicio, dias)
        ir({ inicio: novo === estaSemana ? undefined : novo })
    }
    const faixas = agenda.data ? montarFaixas(agenda.data, porCliente ? 'cliente' : 'pessoa') : []

    return (
        <Pagina
            titulo="Agenda"
            contexto={`${dataCurta(inicio, hoje)} a ${dataCurta(somarDias(inicio, 11), hoje)}`}
            acoes={
                <div className="flex flex-wrap items-center gap-2">
                    <Botao variante="secundario" aria-label="Semana anterior" onPress={() => mover(-7)}>
                        ‹
                    </Botao>
                    <Botao
                        variante="secundario"
                        isDisabled={inicio === estaSemana}
                        onPress={() => ir({ inicio: undefined })}
                    >
                        Hoje
                    </Botao>
                    <Botao variante="secundario" aria-label="Semana seguinte" onPress={() => mover(7)}>
                        ›
                    </Botao>
                </div>
            }
        >
            <Tabs
                selectedKey={porCliente ? 'cliente' : 'pessoa'}
                onSelectionChange={(v) => ir({ faixas: v === 'cliente' ? 'cliente' : undefined })}
            >
                <TabList aria-label="Agrupar por" className="flex w-fit gap-1 rounded-couro bg-trilho p-1">
                    {(['pessoa', 'cliente'] as const).map((v) => (
                        <Tab
                            key={v}
                            id={v}
                            className="cursor-default rounded-couro px-3 py-1.5 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm"
                        >
                            {v === 'pessoa' ? 'Por pessoa' : 'Por cliente'}
                        </Tab>
                    ))}
                </TabList>
            </Tabs>
            {agenda.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : agenda.isError ? (
                <Alerta>{mensagemDeErro(agenda.error)}</Alerta>
            ) : agenda.data.itens.length === 0 && porCliente ? (
                <EstadoVazio titulo="Nada com data nestas duas semanas" />
            ) : (
                <div className={agenda.isPlaceholderData ? 'opacity-60' : ''}>
                    <div className="hidden md:block">
                        <LinhaDoTempo
                            agenda={agenda.data}
                            faixas={faixas}
                            capacidades={[semana1.data, semana2.data]}
                            porCliente={porCliente}
                        />
                    </div>
                    <div className="md:hidden">
                        <ListaPorDia agenda={agenda.data} faixas={faixas} />
                    </div>
                    <p className="mt-3 text-xs text-suave">
                        Borda vermelha: vencido ou violado; âmbar: em risco; tracejada: previsto (recorrência ou fim de
                        contrato). Fim de semana aparece na sexta.
                    </p>
                </div>
            )}
        </Pagina>
    )
}
