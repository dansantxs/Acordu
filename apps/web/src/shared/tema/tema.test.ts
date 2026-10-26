import { aplicarCouro, couroAtual, escolherCouro, escolherTema, temaAtual } from './tema'

describe('tema', () => {
    beforeEach(() => {
        localStorage.clear()
        window.matchMedia = vi.fn().mockReturnValue({ matches: true }) as unknown as typeof window.matchMedia
    })

    it('sem escolha, segue o sistema', () => {
        expect(temaAtual()).toBe('escuro')
    })

    it('guarda a escolha e aplica no html', () => {
        escolherTema('claro')

        expect(temaAtual()).toBe('claro')
        expect(document.documentElement.dataset.tema).toBe('claro')
    })

    it('null volta a seguir o sistema', () => {
        escolherTema('claro')
        escolherTema(null)

        expect(temaAtual()).toBe('escuro')
    })
})

describe('couro', () => {
    beforeEach(() => {
        localStorage.clear()
        aplicarCouro(null)
    })

    it('sem escolha, Café, sem atributo no html', () => {
        expect(couroAtual()).toBe('cafe')
        expect(document.documentElement.dataset.couro).toBeUndefined()
    })

    it('guarda Bordô e aplica; voltar ao Café limpa', () => {
        escolherCouro('bordo')
        expect(couroAtual()).toBe('bordo')
        expect(document.documentElement.dataset.couro).toBe('bordo')

        escolherCouro('cafe')
        expect(localStorage.getItem('acordu.couro')).toBeNull()
        expect(document.documentElement.dataset.couro).toBeUndefined()
    })
})
