import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { Form } from 'react-aria-components'
import { ErroApi } from '@/shared/api/problema'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { documentoValido } from '@/shared/format/documento'
import { caracteresDoDocumento, formatarDocumento } from '@/shared/format/mascaras'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoMascarado } from '@/shared/ui/CampoMascarado'
import { Pagina } from '@/shared/ui/Pagina'
import { apiNebrys } from './api'

function validar(valor: string): string | null {
    if (valor.trim() === '') return 'Informe o CPF ou CNPJ.'
    return documentoValido(valor) ? null : 'CPF ou CNPJ inválido.'
}

/**
 * Liberação de documento (F14-71, F14-108): um CPF ou CNPJ que já teve teste ganha direito a um novo cadastro. O
 * documento só existe no campo enquanto se digita: não vai para o endereço, para o armazenamento do navegador nem
 * para o cache (a mutação não fica guardada), e o campo se esvazia depois do `204`.
 */
export function LiberacaoDocumento() {
    // Trocar a chave remonta o formulário vazio depois de liberar.
    const [rodada, setRodada] = useState(0)
    const [liberado, setLiberado] = useState(false)
    const envio = useMutation({
        mutationFn: async (documento: string) => {
            await apiNebrys.POST('/api/v1/nebrys/documentos/liberacao', { body: { documento } })
        },
        onSuccess: () => {
            setLiberado(true)
            setRodada((r) => r + 1)
            envio.reset()
        },
        gcTime: 0,
    })
    const mensagem = envio.isError
        ? envio.error instanceof ErroApi && envio.error.status === 422
            ? (envio.error.detalhe ?? envio.error.message)
            : mensagemDeErro(envio.error)
        : null

    return (
        <Pagina titulo="Liberar documento" contexto="Área da Nebrys">
            <p className="max-w-prose text-sm text-suave">
                Cada CPF ou CNPJ tem um período de teste. Liberar deixa passar um novo cadastro com ele, uma vez só,
                inclusive o de uma empresa já expurgada. O documento não fica guardado aqui nem na trilha.
            </p>
            <Form
                key={rodada}
                className="flex max-w-md flex-col gap-4"
                validationErrors={errosDeCampo(envio.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    setLiberado(false)
                    envio.mutate(String(new FormData(evento.currentTarget).get('documento') ?? ''))
                }}
            >
                {liberado && <Alerta tom="sucesso">Documento liberado. O próximo cadastro com ele vai passar.</Alerta>}
                {mensagem && <Alerta>{mensagem}</Alerta>}
                <CampoMascarado
                    name="documento"
                    rotulo="CPF ou CNPJ"
                    mascara={formatarDocumento}
                    caracteres={caracteresDoDocumento}
                    teclado="text"
                    autoComplete="off"
                    validate={validar}
                    isRequired
                />
                <div className="flex justify-end">
                    <Botao type="submit" isPending={envio.isPending}>
                        Liberar
                    </Botao>
                </div>
            </Form>
        </Pagina>
    )
}
