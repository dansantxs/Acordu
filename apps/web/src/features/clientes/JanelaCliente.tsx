import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import { criarCliente, editarCliente, opcional, type Cliente } from './consultas'

/** Criar (sem `cliente`) ou editar nome, documento e observações. */
export function JanelaCliente({
    cliente,
    aberta,
    aoMudar,
    aoSalvar,
}: {
    cliente?: Cliente
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    aoSalvar?: (cliente: Cliente) => void
}) {
    const consultas = useQueryClient()
    const envio = useMutation({
        mutationFn: (dados: Parameters<typeof criarCliente>[0]) =>
            cliente ? editarCliente(cliente, dados) : criarCliente(dados),
        onSuccess: async (salvo) => {
            await consultas.invalidateQueries({ queryKey: ['clientes'] })
            aoMudar(false)
            aoSalvar?.(salvo)
        },
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null
    const mudar = (estado: boolean) => {
        if (!estado) envio.reset()
        aoMudar(estado)
    }

    return (
        <Janela
            titulo={cliente ? 'Editar cliente' : 'Novo cliente'}
            aberta={aberta}
            aoMudar={mudar}
            ocupada={envio.isPending}
        >
            <Form
                className="flex flex-col gap-4"
                validationErrors={errosDeCampo(envio.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    const dados = new FormData(evento.currentTarget)
                    envio.mutate({
                        nome: String(dados.get('nome')).trim(),
                        documento: opcional(dados.get('documento')),
                        observacoes: opcional(dados.get('observacoes')),
                    })
                }}
            >
                {mensagem && <Alerta>{mensagem}</Alerta>}
                <CampoTexto name="nome" rotulo="Nome" defaultValue={cliente?.nome} isRequired autoFocus />
                <CampoTexto
                    name="documento"
                    rotulo="CPF ou CNPJ"
                    descricao="Opcional."
                    defaultValue={cliente?.documento ?? ''}
                />
                <CampoTexto
                    name="observacoes"
                    rotulo="Observações"
                    descricao="Só a equipe vê."
                    defaultValue={cliente?.observacoes ?? ''}
                    multilinha
                />
                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={() => mudar(false)} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao type="submit" isPending={envio.isPending}>
                        {cliente ? 'Salvar' : 'Criar cliente'}
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}
