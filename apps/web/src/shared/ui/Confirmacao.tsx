import type { ReactNode } from 'react'
import { Alerta } from './Alerta'
import { Botao } from './Botao'
import { Janela } from './Janela'

/**
 * Confirmação só para o que não tem volta (convenções §4: o resto acontece e oferece desfazer). O botão diz o que
 * acontece ("Inativar cliente"), nunca "OK".
 */
export function Confirmacao({
    titulo,
    acao,
    aberta,
    aoMudar,
    aoConfirmar,
    ocupada,
    erro,
    children,
}: {
    titulo: string
    acao: string
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    aoConfirmar: () => void
    ocupada: boolean
    erro?: string | null
    children: ReactNode
}) {
    return (
        <Janela titulo={titulo} aberta={aberta} aoMudar={aoMudar} ocupada={ocupada}>
            <div className="flex flex-col gap-3 text-sm text-suave">{children}</div>
            {erro && <Alerta>{erro}</Alerta>}
            <div className="flex justify-end gap-3">
                <Botao variante="secundario" onPress={() => aoMudar(false)} isDisabled={ocupada}>
                    Voltar
                </Botao>
                <Botao onPress={aoConfirmar} isPending={ocupada} className="!bg-vermelho !text-botao-tinta">
                    {acao}
                </Botao>
            </div>
        </Janela>
    )
}
