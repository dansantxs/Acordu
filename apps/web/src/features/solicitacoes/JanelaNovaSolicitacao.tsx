import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Form } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { CampoCliente } from '@/features/clientes/CampoCliente'
import { consultaCliente, opcional } from '@/features/clientes/consultas'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import { abrirSolicitacao, consultaCategorias, consultaContratosAtivos, type Solicitacao } from './consultas'
import { PRIORIDADE, TIPO_CONTRATO, type Prioridade } from './rotulos'

const dataCurta = (data: string) => data.split('-').reverse().slice(0, 2).join('/') + '/' + data.slice(2, 4)

/**
 * Abrir solicitação pela equipe (F2c; contrato §7, origem INTERNA). O responsável começa com o responsável interno do
 * cliente (RF-CLI-06) e muda se a pessoa escolher outro; o contrato, sem escolha, fica com a API (o único ativo).
 */
export function JanelaNovaSolicitacao({
    perfil,
    aberta,
    aoMudar,
    clienteInicial,
    aoAbrir,
}: {
    perfil: Perfil
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    clienteInicial?: string | null
    aoAbrir: (solicitacao: Solicitacao) => void
}) {
    return (
        <Janela titulo="Nova solicitação" aberta={aberta} aoMudar={aoMudar} larga>
            {aberta && (
                <Formulario
                    perfil={perfil}
                    clienteInicial={clienteInicial ?? null}
                    aoFechar={() => aoMudar(false)}
                    aoAbrir={aoAbrir}
                />
            )}
        </Janela>
    )
}

function Formulario({
    perfil,
    clienteInicial,
    aoFechar,
    aoAbrir,
}: {
    perfil: Perfil
    clienteInicial: string | null
    aoFechar: () => void
    aoAbrir: (solicitacao: Solicitacao) => void
}) {
    const consultas = useQueryClient()
    const [clienteId, setClienteId] = useState<string | null>(clienteInicial)
    const [contato, setContato] = useState<string | null>(null)
    const [prioridade, setPrioridade] = useState<Prioridade>('MEDIA')
    const [categoria, setCategoria] = useState<string | null>(null)
    const [contrato, setContrato] = useState<string | null>(null)
    // `undefined`: ainda não mexeram, vale o responsável do cliente.
    const [responsavel, setResponsavel] = useState<string | null | undefined>(undefined)

    const cliente = useQuery({ ...consultaCliente(clienteId ?? ''), enabled: clienteId !== null })
    const contratos = useQuery({
        ...consultaContratosAtivos(clienteId ?? ''),
        enabled: clienteId !== null && pode(perfil, 'contrato.ver'),
    })
    const equipe = useQuery(consultaEquipe)
    const categorias = useQuery(consultaCategorias)

    const responsavelEfetivo = responsavel === undefined ? (cliente.data?.responsavel?.id ?? null) : responsavel
    const contatos = (cliente.data?.contatos ?? []).filter((c) => c.ativo)

    const envio = useMutation({
        mutationFn: abrirSolicitacao,
        onSuccess: async (solicitacao) => {
            await consultas.invalidateQueries({ queryKey: ['solicitacoes'] })
            avisar(`Solicitação #${solicitacao.numero} aberta.`)
            aoFechar()
            aoAbrir(solicitacao)
        },
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null

    function trocarCliente(id: string | null) {
        setClienteId(id)
        // Contato e contrato são do cliente: trocar de cliente zera os dois. O responsável volta a seguir o cliente.
        setContato(null)
        setContrato(null)
        setResponsavel(undefined)
    }

    return (
        <Form
            className="flex flex-col gap-4"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                if (!clienteId) return
                const dados = new FormData(evento.currentTarget)
                envio.mutate({
                    clienteId,
                    contatoId: contato,
                    contratoId: contrato,
                    titulo: String(dados.get('titulo')).trim(),
                    descricao: opcional(dados.get('descricao')),
                    prioridade,
                    categoriaId: categoria,
                    responsavelId: responsavelEfetivo,
                })
            }}
        >
            <LegendaObrigatorios />
            {mensagem && <Alerta>{mensagem}</Alerta>}

            <div className="grid gap-4 sm:grid-cols-2">
                <CampoCliente name="clienteId" rotulo="Cliente" valor={clienteId} aoMudar={trocarCliente} isRequired />
                <CampoSelecao
                    name="contatoId"
                    rotulo="Contato"
                    descricao={clienteId ? 'Quem recebe os avisos por e-mail.' : 'Escolha o cliente primeiro.'}
                    opcoes={contatos.map((c) => ({ id: c.id, rotulo: c.nome, detalhe: c.cargo ?? undefined }))}
                    valor={contato}
                    aoMudar={setContato}
                    vazio="Sem contato"
                />
            </div>

            <CampoTexto
                name="titulo"
                rotulo="Título"
                descricao="O que precisa ser feito, em uma linha."
                isRequired
                autoFocus
            />
            <CampoTexto name="descricao" rotulo="Descrição" multilinha />

            <div className="grid gap-4 sm:grid-cols-3">
                <CampoSelecao
                    name="prioridade"
                    rotulo="Prioridade"
                    opcoes={(Object.keys(PRIORIDADE) as Prioridade[]).map((p) => ({ id: p, rotulo: PRIORIDADE[p] }))}
                    valor={prioridade}
                    aoMudar={(p) => p && setPrioridade(p as Prioridade)}
                    isRequired
                />
                <CampoSelecao
                    name="responsavelId"
                    rotulo="Responsável"
                    opcoes={(equipe.data ?? []).map((p) => ({ id: p.id, rotulo: p.nome }))}
                    valor={responsavelEfetivo}
                    aoMudar={setResponsavel}
                    vazio="Ninguém"
                    descricao={
                        responsavel === undefined && cliente.data?.responsavel
                            ? 'O responsável pelo cliente.'
                            : undefined
                    }
                />
                {(categorias.data ?? []).some((c) => c.ativa) && (
                    <CampoSelecao
                        name="categoriaId"
                        rotulo="Categoria"
                        opcoes={(categorias.data ?? [])
                            .filter((c) => c.ativa)
                            .map((c) => ({ id: c.id, rotulo: c.nome }))}
                        valor={categoria}
                        aoMudar={setCategoria}
                        vazio="Sem categoria"
                    />
                )}
            </div>

            {clienteId && (contratos.data?.length ?? 0) > 1 && (
                <CampoSelecao
                    name="contratoId"
                    rotulo="Contrato"
                    descricao="O prazo (SLA) e as horas seguem o contrato escolhido."
                    opcoes={(contratos.data ?? []).map((c) => ({
                        id: c.id,
                        rotulo: TIPO_CONTRATO[c.tipo],
                        detalhe: `desde ${dataCurta(c.inicio)}`,
                    }))}
                    valor={contrato}
                    aoMudar={setContrato}
                    vazio="Sem contrato"
                />
            )}
            {clienteId && contratos.data?.length === 1 && (
                <p className="text-xs text-suave">
                    Vai pelo contrato {TIPO_CONTRATO[contratos.data[0]!.tipo].toLowerCase()} ativo deste cliente (prazo
                    e horas).
                </p>
            )}
            {clienteId && contratos.data?.length === 0 && (
                <p className="text-xs text-suave">
                    Este cliente não tem contrato ativo: a solicitação fica sem prazo (SLA).
                </p>
            )}

            <div className="flex justify-end gap-3">
                <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                    Cancelar
                </Botao>
                <Botao grava type="submit" isPending={envio.isPending}>
                    Abrir solicitação
                </Botao>
            </div>
        </Form>
    )
}
