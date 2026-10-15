import { lerProblema } from './problema'

function resposta(status: number, corpo: unknown, tipo = 'application/problem+json') {
    return new Response(JSON.stringify(corpo), { status, headers: { 'content-type': tipo } })
}

describe('lerProblema', () => {
    it('lê tipo, mensagem e erros de campo', async () => {
        const erro = await lerProblema(
            resposta(400, {
                type: 'https://acordu.app.br/erros/validacao',
                title: 'Dados inválidos',
                status: 400,
                detail: 'A capacidade semanal vai de 0 a 168h.',
                errors: { capacidadeSemanalMinutos: ['A capacidade semanal vai de 0 a 168h.'] },
            }),
        )

        expect(erro.status).toBe(400)
        expect(erro.tipo).toBe('validacao')
        expect(erro.message).toBe('A capacidade semanal vai de 0 a 168h.')
        expect(erro.doCampo('capacidadeSemanalMinutos')).toBe('A capacidade semanal vai de 0 a 168h.')
    })

    it('aguenta resposta que não é JSON', async () => {
        const erro = await lerProblema(
            new Response('falhou', { status: 502, headers: { 'content-type': 'text/plain' } }),
        )

        expect(erro.status).toBe(502)
        expect(erro.tipo).toBe('')
        expect(erro.message).toBe('Erro 502')
    })
})
