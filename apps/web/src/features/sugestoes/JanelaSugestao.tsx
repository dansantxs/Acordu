import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { rotaModelo } from '@/shared/operacao/relatoDeErros'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import { enviarSugestao, TEXTO_MAXIMO, TEXTO_MINIMO } from './consultas'

/**
 * "Sugerir melhoria" (spec do F13, F13-22, F13-23): qualquer pessoa da equipe. Vai junto a tela de onde veio, como rota
 * modelo (sem ids); a pessoa não vai na mensagem, e o aviso pede para não escrever dados de clientes.
 */
export function JanelaSugestao({ aberta, aoMudar }: { aberta: boolean; aoMudar: (aberta: boolean) => void }) {
    const [texto, setTexto] = useState('')
    const envio = useMutation({
        mutationFn: () => enviarSugestao(texto.trim(), rotaModelo(globalThis.location.pathname)),
        onSuccess: () => {
            avisar('Obrigado! A sugestão foi para a equipe do Acordu.')
            setTexto('')
            aoMudar(false)
        },
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null
    const curto = texto.trim().length < TEXTO_MINIMO

    return (
        <Janela
            titulo="Sugerir melhoria"
            aberta={aberta}
            ocupada={envio.isPending}
            aoMudar={(abrir) => {
                if (!abrir) envio.reset()
                aoMudar(abrir)
            }}
        >
            <Form
                className="flex flex-col gap-4"
                validationErrors={errosDeCampo(envio.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    if (!curto) envio.mutate()
                }}
            >
                <Alerta tom="aviso">
                    Não escreva dados dos seus clientes: a sugestão vai para a equipe do Acordu.
                </Alerta>
                {mensagem && <Alerta>{mensagem}</Alerta>}
                <CampoTexto
                    name="texto"
                    rotulo="O que poderia ser melhor?"
                    descricao={`De ${TEXTO_MINIMO} a ${TEXTO_MAXIMO} caracteres (${texto.length}/${TEXTO_MAXIMO}).`}
                    multilinha
                    isRequired
                    maxLength={TEXTO_MAXIMO}
                    value={texto}
                    onChange={setTexto}
                    autoFocus
                />
                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={() => aoMudar(false)} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao type="submit" isPending={envio.isPending} isDisabled={curto}>
                        Enviar
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}
