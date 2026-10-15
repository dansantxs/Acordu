/** Lançado pelas guardas quando a sessão ficou `indisponivel`: a tela de erro oferece tentar de novo. */
export class SessaoIndisponivel extends Error {
    constructor() {
        super('Não deu para conferir a sessão.')
        this.name = 'SessaoIndisponivel'
    }
}
