import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { Janela } from '@/shared/ui/Janela'
import { consultaContratosAtivos, trocarContrato, type Solicitacao } from './consultas'
import { TIPO_CONTRATO } from './rotulos'

const dataCurta = (data: string) => data.split('-').reverse().join('/')

/**
 * Troca de contrato (contrato §7, marco 12): o prazo é recalculado pelo início do ciclo com os prazos do contrato novo
 * (sem zerar o que já venceu); sem contrato, a solicitação fica sem SLA. Apontamentos já feitos não mudam.
 */
export function JanelaContrato({
    solicitacao: s,
    aberta,
    aoMudar,
    aoTrocar,
}: {
    solicitacao: Solicitacao
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    aoTrocar: (nova: Solicitacao) => void
}) {
    const contratos = useQuery({ ...consultaContratosAtivos(s.cliente.id), enabled: aberta })
    const [escolha, setEscolha] = useState<string | null>(s.contratoId)
    const troca = useMutation({ mutationFn: () => trocarContrato(s, escolha), onSuccess: aoTrocar })

    const opcoes = (contratos.data ?? []).map((c) => ({
        id: c.id,
        rotulo: TIPO_CONTRATO[c.tipo],
        detalhe: `desde ${dataCurta(c.inicio)}`,
    }))
    return (
        <Janela
            titulo="Trocar contrato"
            aberta={aberta}
            aoMudar={(a) => {
                if (a) setEscolha(s.contratoId)
                else troca.reset()
                aoMudar(a)
            }}
            ocupada={troca.isPending}
        >
            <p className="text-sm text-suave">
                O prazo (SLA) passa a seguir o contrato escolhido, contado desde a abertura, e o que já venceu continua
                vencido. Sem contrato, a solicitação fica sem prazo. As horas já apontadas não mudam de contrato.
            </p>
            {troca.isError && <Alerta>{mensagemDeErro(troca.error)}</Alerta>}
            {contratos.isSuccess && contratos.data.length === 0 && (
                <Alerta tom="aviso">Este cliente não tem contrato ativo.</Alerta>
            )}
            <CampoSelecao rotulo="Contrato" opcoes={opcoes} valor={escolha} aoMudar={setEscolha} vazio="Sem contrato" />
            <div className="flex justify-end gap-3">
                <Botao variante="secundario" onPress={() => aoMudar(false)} isDisabled={troca.isPending}>
                    Voltar
                </Botao>
                <Botao
                    grava
                    onPress={() => troca.mutate()}
                    isPending={troca.isPending}
                    isDisabled={escolha === s.contratoId}
                >
                    Trocar contrato
                </Botao>
            </div>
        </Janela>
    )
}
