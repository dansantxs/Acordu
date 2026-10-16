import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import { adicionarContato, editarContato, opcional, type Cliente, type Contato, type DadosContato } from './consultas'

/** Adicionar (sem `contato`) ou editar um contato do cliente. */
export function JanelaContato({
    cliente,
    contato,
    aberta,
    aoMudar,
}: {
    cliente: Cliente
    contato?: Contato
    aberta: boolean
    aoMudar: (aberta: boolean) => void
}) {
    const consultas = useQueryClient()
    const envio = useMutation({
        mutationFn: (dados: DadosContato) =>
            contato ? editarContato(cliente, contato, dados) : adicionarContato(cliente, dados),
        onSuccess: async () => {
            await consultas.invalidateQueries({ queryKey: ['clientes'] })
            aoMudar(false)
        },
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null
    const mudar = (estado: boolean) => {
        if (!estado) envio.reset()
        aoMudar(estado)
    }

    return (
        <Janela
            titulo={contato ? 'Editar contato' : 'Novo contato'}
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
                        email: String(dados.get('email')).trim(),
                        telefone: opcional(dados.get('telefone')),
                        cargo: opcional(dados.get('cargo')),
                    })
                }}
            >
                {mensagem && <Alerta>{mensagem}</Alerta>}
                <CampoTexto name="nome" rotulo="Nome" defaultValue={contato?.nome} isRequired autoFocus />
                <CampoTexto
                    name="email"
                    type="email"
                    rotulo="E-mail"
                    descricao="Recebe os avisos das solicitações e, se convidado, entra no portal com ele."
                    defaultValue={contato?.email}
                    isRequired
                />
                <div className="grid gap-4 sm:grid-cols-2">
                    <CampoTexto name="telefone" type="tel" rotulo="Telefone" defaultValue={contato?.telefone ?? ''} />
                    <CampoTexto name="cargo" rotulo="Cargo" defaultValue={contato?.cargo ?? ''} />
                </div>
                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={() => mudar(false)} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao type="submit" isPending={envio.isPending}>
                        {contato ? 'Salvar' : 'Adicionar contato'}
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}
