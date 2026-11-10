import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json } from '@/testes/app'

describe('ajuda', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('mostra só as seções e os primeiros passos que o papel alcança', async () => {
        comPerfil(['solicitacao.ver', 'tarefa.ver'])
        await abrir('/ajuda')
        expect(await screen.findByRole('region', { name: 'Solicitações' })).toBeInTheDocument()
        expect(screen.getByRole('region', { name: 'Tarefas' })).toBeInTheDocument()
        expect(screen.queryByRole('region', { name: 'Configurações' })).not.toBeInTheDocument()
        expect(screen.queryByRole('region', { name: 'Contratos' })).not.toBeInTheDocument()
        const passos = screen.getByRole('region', { name: 'Primeiros passos' })
        expect(within(passos).getByText('Atenda as solicitações')).toBeInTheDocument()
        expect(within(passos).queryByText('Configure a empresa')).not.toBeInTheDocument()
        expect(screen.getByText(/dependem de permissões que o seu papel não tem/)).toBeInTheDocument()
    })

    it('mostra a captura da tela na seção, com legenda e link para a imagem inteira', async () => {
        comPerfil(['solicitacao.ver'])
        await abrir('/ajuda')
        const secao = await screen.findByRole('region', { name: 'Solicitações' })
        const imagem = within(secao).getByRole('img', { name: 'A fila com o prazo de SLA de cada solicitação.' })
        expect(imagem).toHaveAttribute('src', '/ajuda/solicitacoes.webp')
        expect(imagem.closest('a')).toHaveAttribute('href', '/ajuda/solicitacoes.webp')
    })

    it('"Instalar o Acordu" aparece para qualquer papel, com o caminho do iPhone (F12-30)', async () => {
        comPerfil([])
        await abrir('/ajuda')
        const secao = await screen.findByRole('region', { name: 'Instalar o Acordu' })
        expect(within(secao).getByText(/Adicionar à Tela de Início/)).toBeInTheDocument()
        expect(within(secao).getByText(/Instalar o Acordu/, { selector: 'li' })).toBeInTheDocument()
    })

    it('"Sugerir melhoria" aparece para qualquer papel, com o aviso dos dados de clientes (F13f)', async () => {
        comPerfil([])
        await abrir('/ajuda')
        const secao = await screen.findByRole('region', { name: 'Sugerir melhoria' })
        expect(within(secao).getByText(/Não escreva dados dos seus clientes/)).toBeInTheDocument()
    })

    it('busca no texto das seções, sem diferenciar acentos', async () => {
        comPerfil(['contrato.ver'])
        await abrir('/ajuda')
        await userEvent.setup().type(await screen.findByRole('searchbox', { name: 'Buscar no manual' }), 'fechar o mes')
        expect(await screen.findByRole('region', { name: 'Contratos' })).toBeInTheDocument()
        expect(screen.queryByRole('region', { name: 'Hoje' })).not.toBeInTheDocument()
        expect(screen.queryByRole('region', { name: 'Primeiros passos' })).not.toBeInTheDocument()
    })

    it('o "?" de uma tela abre a ajuda na seção dela', async () => {
        comPerfil(['recorrencia.ver'], {
            'GET /api/v1/equipe': () => json([]),
            'GET /api/v1/recorrencias': () => json({ itens: [], pagina: 1, tamanhoPagina: 50, total: 0 }),
        })
        const roteador = await abrir('/recorrencias')
        await userEvent.setup().click(await screen.findByRole('link', { name: 'Ajuda: Recorrências' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ topico: 'recorrencias' }))
        const secao = await screen.findByRole('region', { name: 'Recorrências' })
        expect(secao).toHaveFocus()
        expect(within(secao).getByRole('link', { name: 'Abrir a tela' })).toHaveAttribute('href', '/recorrencias')
    })
})
