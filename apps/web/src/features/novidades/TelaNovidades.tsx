import { useEffect } from 'react'
import { Pagina } from '@/shared/ui/Pagina'
import { ListaNotas } from './ListaNotas'
import { notasDo } from './notas'
import { marcarVistas } from './vistas'

/** Novidades da equipe (M4): o que mudou a cada atualização. Abrir a tela apaga o ponto do menu. */
export function TelaNovidades() {
    useEffect(() => marcarVistas('equipe'), [])
    return (
        <Pagina titulo="Novidades" contexto="O que mudou no Acordu" ajuda="novidades">
            <div className="max-w-3xl">
                <ListaNotas notas={notasDo('equipe')} />
            </div>
        </Pagina>
    )
}
