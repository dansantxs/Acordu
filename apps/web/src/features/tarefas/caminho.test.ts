import { describe, expect, it } from 'vitest'
import { caminho } from './consultas'

describe('caminho entre colunas do quadro', () => {
    it('segue a máquina de estados, passando por Fazendo quando precisa', () => {
        expect(caminho('A_FAZER', 'FAZENDO')).toEqual(['inicio'])
        expect(caminho('A_FAZER', 'CONCLUIDA')).toEqual(['inicio', 'conclusao'])
        expect(caminho('FAZENDO', 'A_FAZER')).toEqual(['retorno'])
        expect(caminho('FAZENDO', 'CONCLUIDA')).toEqual(['conclusao'])
        expect(caminho('CONCLUIDA', 'FAZENDO')).toEqual(['reabertura'])
        expect(caminho('CONCLUIDA', 'A_FAZER')).toEqual(['reabertura', 'retorno'])
        expect(caminho('FAZENDO', 'FAZENDO')).toEqual([])
    })
})
