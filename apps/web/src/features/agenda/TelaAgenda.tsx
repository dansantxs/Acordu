import { useQuery } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { Tab, TabList, Tabs } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { intervaloDePolling } from '@/shared/config/ambiente'
import { dataCurta, hojeNoFuso, segundaDaSemana, somarDias } from '@/shared/format/datas'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Pagina } from '@/shared/ui/Pagina'
import { consultaAgenda, consultaCapacidade, type FiltrosAgenda } from './consultas'
import { LinhaDoTempo, ListaPorDia, montarFaixas } from './LinhaDoTempo'
import { PlanoDaSemana } from './PlanoDaSemana'

const estiloAba =
    'cursor-default rounded-couro px-3 py-1.5 text-sm text-suave outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:bg-superficie data-[selected]:font-semibold data-[selected]:text-tinta data-[selected]:shadow-sm'

/**
 * Agenda da equipe (F6c e F6d): linha do tempo de duas semanas úteis ou plano da semana, para arrastar e planejar. A
 * janela, a vista e o agrupamento ficam no endereço; o quadro fica em Tarefas (decisão 2 do F6).
 */
export function TelaAgenda({ perfil, filtros }: { perfil: Perfil; filtros: FiltrosAgenda }) {
    const navegar = useNavigate()
    const hoje = hojeNoFuso(perfil.tenant.fusoHorario)
    const estaSemana = segundaDaSemana(hoje)
    const inicio = filtros.inicio ?? estaSemana
    const agenda = useQuery({ ...consultaAgenda(inicio), refetchInterval: intervaloDePolling() })
    const verCarga = pode(perfil, 'tarefa.ver')
    const semana1 = useQuery({
        ...consultaCapacidade(inicio),
        enabled: verCarga,
        refetchInterval: intervaloDePolling(),
    })
    const semana2 = useQuery({
        ...consultaCapacidade(somarDias(inicio, 7)),
        enabled: verCarga,
        refetchInterval: intervaloDePolling(),
    })
    const porCliente = filtros.faixas === 'cliente'
    const plano = filtros.vista === 'plano'
    const ir = (mudanca: Partial<FiltrosAgenda>) =>
        void navegar({ to: '/agenda', search: (atual: FiltrosAgenda) => ({ ...atual, ...mudanca }), replace: true })
    const mover = (dias: number) => {
        const novo = somarDias(inicio, dias)
        ir({ inicio: novo === estaSemana ? undefined : novo })
    }
    const faixas = agenda.data ? montarFaixas(agenda.data, porCliente ? 'cliente' : 'pessoa') : []

    return (
        <Pagina
            ajuda="agenda"
            titulo="Agenda"
            contexto={`${dataCurta(inicio, hoje)} a ${dataCurta(somarDias(inicio, plano ? 4 : 11), hoje)}`}
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
            <div className="flex flex-wrap gap-3">
                <Tabs
                    selectedKey={plano ? 'plano' : 'linha'}
                    onSelectionChange={(v) => ir({ vista: v === 'plano' ? 'plano' : undefined })}
                >
                    <TabList aria-label="Vista" className="flex w-fit gap-1 rounded-couro bg-trilho p-1">
                        {(['linha', 'plano'] as const).map((v) => (
                            <Tab key={v} id={v} className={estiloAba}>
                                {v === 'linha' ? 'Linha do tempo' : 'Plano da semana'}
                            </Tab>
                        ))}
                    </TabList>
                </Tabs>
                {!plano && (
                    <Tabs
                        selectedKey={porCliente ? 'cliente' : 'pessoa'}
                        onSelectionChange={(v) => ir({ faixas: v === 'cliente' ? 'cliente' : undefined })}
                    >
                        <TabList aria-label="Agrupar por" className="flex w-fit gap-1 rounded-couro bg-trilho p-1">
                            {(['pessoa', 'cliente'] as const).map((v) => (
                                <Tab key={v} id={v} className={estiloAba}>
                                    {v === 'pessoa' ? 'Por pessoa' : 'Por cliente'}
                                </Tab>
                            ))}
                        </TabList>
                    </Tabs>
                )}
            </div>
            {agenda.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : agenda.isError ? (
                <Alerta>{mensagemDeErro(agenda.error)}</Alerta>
            ) : plano ? (
                <div className={agenda.isPlaceholderData ? 'opacity-60' : ''}>
                    <PlanoDaSemana
                        agenda={agenda.data}
                        inicio={inicio}
                        capacidade={semana1.data}
                        podeEditar={pode(perfil, 'tarefa.editar')}
                    />
                    <p className="mt-3 text-xs text-suave">
                        Arraste uma tarefa (ou use ⋯) para outro dia ou outra pessoa. Solicitações seguem o prazo do SLA
                        e recorrências, a regra: aparecem, mas não se movem.
                    </p>
                </div>
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
