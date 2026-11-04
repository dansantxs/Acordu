import { SEM_ENVIO } from '@/features/sessao/AvisoConfirmacao'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button, Form, Radio, RadioGroup } from 'react-aria-components'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { dataHora } from '@/shared/format/prazo'
import { useAgora } from '@/shared/hooks/useAgora'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { Janela } from '@/shared/ui/Janela'
import {
    convidarParaPortal,
    mudarEscopoPortal,
    revogarPortal,
    type Cliente,
    type Contato,
    type EscopoPortal,
} from './consultas'

const ROTULO_ESCOPO: Record<EscopoPortal, string> = { PROPRIAS: 'só as próprias', TODAS_DO_CLIENTE: 'todas do cliente' }

/** Situação do contato no portal, com o fim do convite (o e-mail pode ainda não ter saído: `conviteExpiraEm` nulo). */
export function SituacaoPortal({ contato, fuso }: { contato: Contato; fuso: string }) {
    const agora = useAgora(60_000)
    const acesso = contato.acessoPortal
    if (!acesso) return <span className="text-suave">sem acesso ao portal</span>
    const escopo = acesso.escopoPortal ? ROTULO_ESCOPO[acesso.escopoPortal] : ''
    if (acesso.situacao === 'ATIVO') return <span className="text-verde">acessa o portal · {escopo}</span>
    if (!acesso.conviteExpiraEm) return <span className="text-ambar-texto">convite saindo por e-mail · {escopo}</span>
    if (new Date(acesso.conviteExpiraEm).getTime() <= agora)
        return <span className="text-vermelho">convite expirado · {escopo}</span>
    return (
        <span className="text-ambar-texto">
            convidado · {escopo} · vale até {dataHora(acesso.conviteExpiraEm, fuso)}
        </span>
    )
}

type Janelas = 'convite' | 'escopo' | 'revogacao' | null

/**
 * Ações do portal de um contato ativo (decisão 4 do F4: o convite sugere "Só as próprias"). Sem acesso: convidar;
 * convidado: reenviar (o link anterior deixa de valer) ou cancelar; com acesso: mudar o escopo ou revogar.
 */
export function AcoesPortal({
    cliente,
    contato,
    envioLiberado,
}: {
    cliente: Cliente
    contato: Contato
    /** Falso: convidar e reenviar ficam desabilitados até a confirmação do e-mail (RF-AUT-06). */
    envioLiberado: boolean
}) {
    const [aberta, setAberta] = useState<Janelas>(null)
    const acesso = contato.acessoPortal
    const consultas = useQueryClient()
    const revogacao = useMutation({
        mutationFn: () => revogarPortal(cliente, contato),
        onSuccess: async () => {
            await consultas.invalidateQueries({ queryKey: ['clientes'] })
            setAberta(null)
            avisar(
                acesso?.situacao === 'ATIVO'
                    ? `${contato.nome} não acessa mais o portal.`
                    : `Convite de ${contato.nome} cancelado.`,
            )
        },
    })
    const botao = 'text-acento-texto hover:underline'
    return (
        <>
            {!acesso ? (
                <>
                    <Button
                        onPress={() => setAberta('convite')}
                        aria-label={`Convidar ${contato.nome} para o portal`}
                        className={`${botao} disabled:text-suave disabled:no-underline`}
                        isDisabled={!envioLiberado}
                    >
                        Convidar para o portal
                    </Button>
                    {!envioLiberado && <span className="text-xs text-suave">{SEM_ENVIO}</span>}
                </>
            ) : acesso.situacao === 'CONVIDADO' ? (
                <>
                    <Button
                        onPress={() => setAberta('convite')}
                        aria-label={`Reenviar convite de ${contato.nome}`}
                        className={`${botao} disabled:text-suave disabled:no-underline`}
                        isDisabled={!envioLiberado}
                    >
                        Reenviar convite
                    </Button>
                    <Button
                        onPress={() => setAberta('revogacao')}
                        aria-label={`Cancelar convite de ${contato.nome}`}
                        className="text-suave hover:underline"
                    >
                        Cancelar convite
                    </Button>
                </>
            ) : (
                <>
                    <Button
                        onPress={() => setAberta('escopo')}
                        aria-label={`Mudar acesso de ${contato.nome}`}
                        className={botao}
                    >
                        Mudar acesso
                    </Button>
                    <Button
                        onPress={() => setAberta('revogacao')}
                        aria-label={`Revogar acesso de ${contato.nome}`}
                        className="text-suave hover:underline"
                    >
                        Revogar acesso
                    </Button>
                </>
            )}
            {(aberta === 'convite' || aberta === 'escopo') && (
                <JanelaAcesso cliente={cliente} contato={contato} modo={aberta} aoFechar={() => setAberta(null)} />
            )}
            <Confirmacao
                titulo={
                    acesso?.situacao === 'ATIVO'
                        ? `Revogar o acesso de ${contato.nome}?`
                        : `Cancelar o convite de ${contato.nome}?`
                }
                acao={acesso?.situacao === 'ATIVO' ? 'Revogar acesso' : 'Cancelar convite'}
                aberta={aberta === 'revogacao'}
                aoMudar={(a) => {
                    if (!a) {
                        revogacao.reset()
                        setAberta(null)
                    }
                }}
                aoConfirmar={() => revogacao.mutate()}
                ocupada={revogacao.isPending}
                erro={revogacao.isError ? mensagemDeErro(revogacao.error) : null}
            >
                <p>
                    {acesso?.situacao === 'ATIVO'
                        ? 'Sai do portal na hora, e os links enviados por solicitação param de funcionar.'
                        : 'O link do convite para de funcionar.'}{' '}
                    Dá para convidar de novo depois.
                </p>
            </Confirmacao>
        </>
    )
}

function JanelaAcesso({
    cliente,
    contato,
    modo,
    aoFechar,
}: {
    cliente: Cliente
    contato: Contato
    modo: 'convite' | 'escopo'
    aoFechar: () => void
}) {
    const consultas = useQueryClient()
    const [escopo, setEscopo] = useState<EscopoPortal>(contato.acessoPortal?.escopoPortal ?? 'PROPRIAS')
    const reenvio = modo === 'convite' && contato.acessoPortal !== null
    const envio = useMutation({
        mutationFn: () =>
            modo === 'convite'
                ? convidarParaPortal(cliente, contato, escopo)
                : mudarEscopoPortal(cliente, contato, escopo),
        onSuccess: async () => {
            await consultas.invalidateQueries({ queryKey: ['clientes'] })
            avisar(
                modo === 'convite' ? `Convite enviado para ${contato.email}.` : `Acesso de ${contato.nome} alterado.`,
            )
            aoFechar()
        },
    })
    return (
        <Janela
            titulo={
                modo === 'escopo' ? 'Mudar acesso ao portal' : reenvio ? 'Reenviar convite' : 'Convidar para o portal'
            }
            aberta
            aoMudar={(a) => !a && aoFechar()}
            ocupada={envio.isPending}
        >
            <Form
                className="flex flex-col gap-4"
                onSubmit={(evento) => {
                    evento.preventDefault()
                    envio.mutate()
                }}
            >
                {modo === 'convite' && (
                    <p className="text-sm text-suave">
                        {contato.nome} recebe em {contato.email} um link para criar a senha.
                        {reenvio && ' O link enviado antes para de funcionar.'}
                    </p>
                )}
                {envio.isError && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
                <RadioGroup
                    value={escopo}
                    onChange={(v) => setEscopo(v as EscopoPortal)}
                    className="flex flex-col gap-2"
                >
                    <span className="text-sm font-semibold">O que vê no portal</span>
                    {(
                        [
                            ['PROPRIAS', 'Só as próprias', 'As solicitações em que é o contato.'],
                            [
                                'TODAS_DO_CLIENTE',
                                'Todas do cliente',
                                `Todas as solicitações de ${cliente.nome}, de qualquer contato.`,
                            ],
                        ] as const
                    ).map(([valor, rotulo, descricao]) => (
                        <Radio
                            key={valor}
                            value={valor}
                            className="group flex cursor-default gap-3 rounded-couro border border-linha p-3 outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:border-acento"
                        >
                            <span
                                aria-hidden="true"
                                className="mt-0.5 size-4 shrink-0 rounded-full border-2 border-linha group-data-[selected]:border-[5px] group-data-[selected]:border-acento"
                            />
                            <span className="flex flex-col text-sm">
                                <span className="font-semibold">{rotulo}</span>
                                <span className="text-suave">{descricao}</span>
                            </span>
                        </Radio>
                    ))}
                </RadioGroup>
                <div className="flex justify-end gap-2">
                    <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao type="submit" isPending={envio.isPending}>
                        {modo === 'escopo' ? 'Salvar' : reenvio ? 'Reenviar convite' : 'Enviar convite'}
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}
