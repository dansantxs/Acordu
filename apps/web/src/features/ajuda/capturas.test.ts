import { describe, expect, it } from 'vitest'
import { CAPTURAS } from './capturas'
import { SECOES_AJUDA } from './conteudo'

// Só os nomes dos arquivos interessam: o glob preguiçoso não carrega as imagens.
const arquivos = Object.keys(import.meta.glob('../../../public/ajuda/*.webp')).map((c) =>
    c.slice(c.lastIndexOf('/') + 1, -'.webp'.length),
)

describe('capturas do manual', () => {
    it('toda captura listada tem o arquivo gerado pelo bot (peça com .github/ajuda-request)', () => {
        expect(CAPTURAS.filter((c) => !arquivos.includes(c))).toEqual([])
    })

    it('não sobra arquivo de captura que saiu da lista', () => {
        expect(arquivos.filter((a) => !(CAPTURAS as readonly string[]).includes(a))).toEqual([])
    })

    it('toda captura aparece em alguma seção do manual', () => {
        const usadas = new Set(SECOES_AJUDA.flatMap((s) => s.imagens?.map((i) => i.captura) ?? []))
        expect(CAPTURAS.filter((c) => !usadas.has(c))).toEqual([])
    })
})
