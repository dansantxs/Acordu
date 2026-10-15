import { escolherTema, temaAtual } from './tema'

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
