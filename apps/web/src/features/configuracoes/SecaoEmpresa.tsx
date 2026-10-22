import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useRouter } from '@tanstack/react-router'
import { useState } from 'react'
import { Form } from 'react-aria-components'
import { lerTextosSla, TabelaSla, textosSla } from '@/features/contratos/TabelaSla'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar } from '@/shared/avisos/avisos'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import { consultaEmpresa, salvarEmpresa, type Empresa } from './consultas'
import { CabecalhoSecao } from './TelaConfiguracoes'

/** Empresa (F5b): nome, para onde vão as respostas dos clientes e o SLA que os contratos novos sugerem. */
export function SecaoEmpresa() {
    const empresa = useQuery(consultaEmpresa)
    return (
        <>
            <CabecalhoSecao titulo="Empresa">
                O nome aparece para a equipe e, se o portal não tiver um nome próprio, para os clientes.
            </CabecalhoSecao>
            {empresa.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : empresa.isError ? (
                <Alerta>{mensagemDeErro(empresa.error)}</Alerta>
            ) : (
                <Formulario empresa={empresa.data} />
            )}
        </>
    )
}

function Formulario({ empresa }: { empresa: Empresa }) {
    const consultas = useQueryClient()
    const roteador = useRouter()
    const [sla, setSla] = useState(() => textosSla(empresa.slaPadrao))
    const [invalidos, setInvalidos] = useState<string[]>([])
    const envio = useMutation({
        mutationFn: salvarEmpresa,
        onSuccess: async (salva) => {
            consultas.setQueryData(consultaEmpresa.queryKey, salva)
            // O nome da empresa vem no perfil (capa lateral, troca de empresa).
            await consultas.invalidateQueries({ queryKey: ['sessao', 'eu'] })
            await roteador.invalidate()
            avisar('Dados da empresa salvos.')
        },
    })
    const erroSla = envio.error instanceof ErroApi ? envio.error.doCampo('slaPadrao') : undefined
    return (
        <Form
            className="flex max-w-2xl flex-col gap-5"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                const dados = new FormData(evento.currentTarget)
                const { prazos, invalidos: ruins } = lerTextosSla(sla)
                setInvalidos(ruins)
                if (ruins.length) return
                envio.mutate({
                    nome: String(dados.get('nome')).trim(),
                    responderPara: String(dados.get('responderPara') ?? '').trim() || null,
                    slaPadrao: prazos,
                })
            }}
        >
            <LegendaObrigatorios />
            {envio.isError && mensagemDeErro(envio.error) && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
            <CampoTexto name="nome" rotulo="Nome da empresa" defaultValue={empresa.nome} maxLength={200} isRequired />
            <CampoTexto
                name="responderPara"
                rotulo="Responder para"
                type="email"
                defaultValue={empresa.responderPara ?? ''}
                descricao="Quando o cliente responde um e-mail do Acordu, a resposta vai para este endereço. Vazio: a resposta não chega a ninguém, e o cliente responde pelo portal."
            />
            <fieldset className="flex flex-col gap-2">
                <legend className="text-sm font-semibold">SLA padrão dos contratos novos (horas úteis)</legend>
                <p className="text-xs text-suave">
                    {empresa.slaPersonalizado
                        ? 'Já vem preenchido em cada contrato novo, que pode ter os seus prazos. Mudar aqui não altera contratos existentes.'
                        : 'Por enquanto, a sugestão do Acordu. Ajuste ao que a empresa costuma combinar; contratos existentes não mudam.'}
                </p>
                {erroSla && <p className="text-sm text-vermelho">{erroSla}</p>}
                <TabelaSla valores={sla} aoMudar={setSla} invalidos={invalidos} />
            </fieldset>
            <div>
                <Botao type="submit" isPending={envio.isPending}>
                    Salvar
                </Botao>
            </div>
        </Form>
    )
}
