import { useState } from 'react'

/**
 * Captura de tela dos manuais (equipe e portal), tirada pelo bot com a empresa de demonstração. Clicar abre a imagem
 * inteira em outra aba; se o arquivo faltar (bot ainda não rodou), a figura some em vez de mostrar uma imagem quebrada.
 * `largura` e `altura` reservam a proporção antes de a imagem chegar.
 */
export function Captura({
    caminho,
    legenda,
    largura,
    altura,
}: {
    caminho: string
    legenda: string
    largura: number
    altura: number
}) {
    const [falhou, setFalhou] = useState(false)
    if (falhou) return null
    return (
        <figure className="flex flex-col gap-1.5">
            <a href={caminho} target="_blank" rel="noreferrer" aria-label={`${legenda} (abre a imagem em outra aba)`}>
                <img
                    src={caminho}
                    alt={legenda}
                    width={largura}
                    height={altura}
                    loading="lazy"
                    decoding="async"
                    onError={() => setFalhou(true)}
                    className="h-auto w-full rounded-couro border border-linha"
                />
            </a>
            <figcaption className="text-xs text-suave">{legenda}</figcaption>
        </figure>
    )
}
