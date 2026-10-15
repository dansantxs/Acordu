import { useRouter } from '@tanstack/react-router'
import { useState } from 'react'
import { tentarDeNovo } from '../sessao/sessao'
import { Botao } from './Botao'

/**
 * Tela inteira quando a sessão não pôde ser conferida (sem rede, limite de chamadas, servidor fora). A sessão pode
 * estar valendo, por isso não manda para o login; "Tentar de novo" confere de novo e segue o caminho normal.
 */
export function TelaSemConexao() {
    const roteador = useRouter()
    const [tentando, setTentando] = useState(false)
    return (
        <main className="grid min-h-screen place-items-center bg-fundo p-6">
            <div role="alert" className="flex max-w-md flex-col items-start gap-3">
                <span className="font-titulo text-2xl font-semibold text-acento-texto italic">acordu</span>
                <h1 className="font-titulo text-2xl font-semibold">Não deu para conectar</h1>
                <p className="text-sm text-suave">
                    O Acordu não respondeu agora. Sua sessão continua guardada: confira a internet e tente de novo em
                    instantes.
                </p>
                <Botao
                    isPending={tentando}
                    onPress={async () => {
                        setTentando(true)
                        try {
                            await tentarDeNovo()
                            await roteador.invalidate()
                        } finally {
                            setTentando(false)
                        }
                    }}
                >
                    Tentar de novo
                </Botao>
            </div>
        </main>
    )
}
