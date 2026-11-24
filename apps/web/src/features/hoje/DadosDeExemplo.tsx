import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { pode } from '@/features/casca/navegacao'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { podeGravar } from '@/shared/sessao/gravacao'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { apagarExemplos, carregarExemplos, consultaExemplos } from './consultas'

/**
 * Dados de exemplo no Hoje (RF-EMP-05, F14r; design do F14, D13). Para o Dono (`configuracao.gerenciar`) de empresa
 * gravável: na empresa em teste ainda sem dados, a oferta de carregar um conjunto fictício (F14-91); com os exemplos
 * carregados, apagá-los (F14-94), com confirmação, porque não tem volta. Quem decide é a API (`disponivel`,
 * `carregados`): a tela não confere a situação nem os dados por conta própria. Carregar ou apagar mexe em quase tudo,
 * então todas as consultas são relidas.
 */
export function DadosDeExemplo({ perfil }: { perfil: Perfil }) {
    const paraQuem = pode(perfil, 'configuracao.gerenciar') && podeGravar(perfil.tenant)
    const consulta = useQuery({ ...consultaExemplos, enabled: paraQuem })
    const consultas = useQueryClient()
    const [apagando, setApagando] = useState(false)
    const carga = useMutation({
        mutationFn: carregarExemplos,
        onSuccess: async () => {
            await consultas.invalidateQueries()
            avisar('Os dados de exemplo foram carregados. Nenhum e-mail foi enviado.')
        },
    })
    const remocao = useMutation({
        mutationFn: apagarExemplos,
        onSuccess: async () => {
            setApagando(false)
            await consultas.invalidateQueries()
            avisar('Os dados de exemplo foram apagados.')
        },
    })

    if (!paraQuem || !consulta.data) return null
    const { disponivel, carregados } = consulta.data
    if (!disponivel && !carregados) return null

    return (
        <section
            aria-labelledby="hoje-exemplos"
            className="flex flex-col gap-3 rounded-couro border border-dashed border-acento/50 bg-superficie p-4"
        >
            <h2 id="hoje-exemplos" className="font-titulo text-xl font-semibold tracking-tight">
                {disponivel ? 'Quer ver o Acordu com dados?' : 'Dados de exemplo'}
            </h2>
            {disponivel ? (
                <p className="text-sm text-suave">
                    Carregue alguns clientes, contatos, contratos, solicitações, tarefas e horas de exemplo, todos
                    fictícios, para ver as telas funcionando antes de cadastrar os seus. Nenhum e-mail é enviado, e dá
                    para apagar tudo depois.
                </p>
            ) : (
                <p className="text-sm text-suave">
                    Os clientes marcados como exemplo e tudo o que é deles são fictícios. Quando quiser começar com os
                    seus dados, apague os exemplos: o que você cadastrou fora deles fica.
                </p>
            )}
            {carga.isError && <Alerta>{mensagemDeErro(carga.error) ?? 'Não deu para carregar os exemplos.'}</Alerta>}
            <div>
                {disponivel ? (
                    <Botao grava isPending={carga.isPending} onPress={() => carga.mutate()}>
                        Carregar dados de exemplo
                    </Botao>
                ) : (
                    <Botao grava variante="secundario" onPress={() => setApagando(true)}>
                        Apagar dados de exemplo
                    </Botao>
                )}
            </div>
            <Confirmacao
                titulo="Apagar os dados de exemplo?"
                acao="Apagar dados de exemplo"
                aberta={apagando}
                aoMudar={(aberta) => {
                    if (!aberta) remocao.reset()
                    setApagando(aberta)
                }}
                aoConfirmar={() => remocao.mutate()}
                ocupada={remocao.isPending}
                erro={remocao.isError ? mensagemDeErro(remocao.error) : null}
            >
                <p>
                    Saem os clientes de exemplo e tudo o que é deles: contatos, contratos, solicitações, tarefas e
                    horas, inclusive o que a equipe criou dentro deles.
                </p>
                <p>O que não é exemplo fica como está.</p>
            </Confirmacao>
        </section>
    )
}
