import { useMutation } from '@tanstack/react-query'
import { useRouterState } from '@tanstack/react-router'
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
    // A tela atual pelo roteador (o endereço real em produção; o da memória nos testes).
    const caminho = useRouterState({ select: (estado) => estado.location.pathname })
    const envio = useMutation({
        mutationFn: () => enviarSugestao(texto.trim(), rotaModelo(caminho)),
        onSuccess: () => {
            avisar('Obrigado! A sugestão foi para a equipe do Acordu.')
            setTexto('')
            aoMudar(false)
        },
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null
    const tamanho = texto.trim().length
    const curto = tamanho < TEXTO_MINIMO
    const numero = (n: number) => n.toLocaleString('pt-BR')
    // Fechar por qualquer caminho (Esc, fora, Cancelar) esquece o erro do envio: reabrir vem limpo.
    const mudar = (abrir: boolean) => {
        if (!abrir) envio.reset()
        aoMudar(abrir)
    }

    return (
        <Janela titulo="Sugerir melhoria" aberta={aberta} ocupada={envio.isPending} aoMudar={mudar}>
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
                    descricao={`De ${TEXTO_MINIMO} a ${numero(TEXTO_MAXIMO)} caracteres (${numero(tamanho)}/${numero(TEXTO_MAXIMO)}).`}
                    multilinha
                    isRequired
                    maxLength={TEXTO_MAXIMO}
                    value={texto}
                    onChange={setTexto}
                    autoFocus
                />
                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={() => mudar(false)} isDisabled={envio.isPending}>
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
