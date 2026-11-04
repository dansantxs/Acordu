import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, type Rotas } from '@/testes/app'

const VER = ['cliente.ver']
const GERENCIAR = ['cliente.ver', 'cliente.gerenciar']
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })

/** Sessão com as permissões e as listas de apoio do formulário (equipe e etiquetas em uso). */
const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/equipe': () => json([{ id: 'u1', nome: 'Ana Prado' }]),
        'GET /api/v1/clientes/etiquetas': () => json(['Varejo']),
        ...rotas,
    })

const padaria = {
    id: 'c1',
    nome: 'Padaria Bom Grão',
    documento: '12.345.678/0001-90',
    observacoes: 'Atende de manhã.',
    ativo: true,
    versao: '42',
    razaoSocial: null,
    inscricaoEstadual: null,
    email: null,
    telefone: null,
    endereco: null,
    responsavel: null,
    etiquetas: [],
    contatos: [
        {
            id: 'k1',
            nome: 'Marta Souza',
            email: 'marta@bomgrao.com',
            telefone: null,
            cargo: 'Gerente',
            ativo: true,
            temConta: true,
            acessoPortal: { situacao: 'ATIVO', escopoPortal: 'TODAS_DO_CLIENTE', conviteExpiraEm: null },
        },
    ],
}

describe('clientes', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('sem clientes, convida a cadastrar o primeiro (só quem pode gerenciar)', async () => {
        app(GERENCIAR, { 'GET /api/v1/clientes': () => pagina([]) })
        await abrir('/clientes')
        expect(await screen.findByRole('button', { name: 'Cadastrar o primeiro cliente' })).toBeInTheDocument()
    })

    it('quem só vê não tem botão de criar', async () => {
        app(VER, { 'GET /api/v1/clientes': () => pagina([]) })
        await abrir('/clientes')
        expect(await screen.findByText('Nenhum cliente ainda')).toBeInTheDocument()
        expect(screen.queryByRole('button', { name: /Novo cliente|Cadastrar/ })).not.toBeInTheDocument()
    })

    it('a busca vai para o endereço e para a API; por padrão só ativos', async () => {
        const chamadas = app(VER, {
            'GET /api/v1/clientes': (_, url) =>
                pagina(
                    url.searchParams.get('busca')
                        ? [
                              {
                                  id: 'c1',
                                  nome: 'Padaria Bom Grão',
                                  documento: null,
                                  ativo: true,
                                  razaoSocial: null,
                                  responsavel: null,
                                  etiquetas: [],
                              },
                          ]
                        : [],
                ),
        })
        const roteador = await abrir('/clientes')
        await screen.findByText('Nenhum cliente ainda')
        await userEvent.setup().type(screen.getByRole('searchbox', { name: 'Buscar clientes' }), 'padaria')
        expect(await screen.findByRole('link', { name: 'Padaria Bom Grão' })).toBeInTheDocument()
        expect(roteador.state.location.search).toEqual({ busca: 'padaria' })
        const ultima = chamadas.filter((c) => c.caminho === '/api/v1/clientes').at(-1)
        expect(ultima?.consulta.get('busca')).toBe('padaria')
        expect(ultima?.consulta.get('ativo')).toBe('true')
    })

    it('cria o cliente e abre a ficha', async () => {
        const chamadas = app(GERENCIAR, {
            'GET /api/v1/clientes': () => pagina([]),
            'POST /api/v1/clientes': () => json({ ...padaria, contatos: [] }, 201),
            'GET /api/v1/clientes/c1': () => json(padaria),
        })
        const roteador = await abrir('/clientes')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Novo cliente' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo cliente' })
        await usuario.type(within(janela).getByLabelText('Nome'), 'Padaria Bom Grão')
        await usuario.click(within(janela).getByRole('button', { name: 'Criar cliente' }))
        expect(await screen.findByRole('heading', { level: 1, name: 'Padaria Bom Grão' })).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe('/clientes/c1')
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/clientes')?.corpo).toEqual({
            nome: 'Padaria Bom Grão',
            razaoSocial: null,
            documento: null,
            inscricaoEstadual: null,
            email: null,
            telefone: null,
            endereco: null,
            responsavelId: null,
            etiquetas: [],
            observacoes: null,
        })
    })

    it('a ficha mostra os contatos e adiciona um novo', async () => {
        const chamadas = app(GERENCIAR, {
            'GET /api/v1/clientes/c1': () => json(padaria),
            'POST /api/v1/clientes/c1/contatos': () => json({}, 201),
        })
        const roteador = await abrir('/clientes/c1')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('tab', { name: 'Contatos e portal' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ secao: 'contatos' }))
        expect(await screen.findByText('Marta Souza')).toBeInTheDocument()
        expect(screen.getByText('acessa o portal · todas do cliente')).toBeInTheDocument()

        await usuario.click(screen.getByRole('button', { name: 'Novo contato' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo contato' })
        await usuario.type(within(janela).getByLabelText('Nome'), 'João')
        await usuario.type(within(janela).getByLabelText(/E-mail/), 'joao@bomgrao.com')
        await usuario.click(within(janela).getByRole('button', { name: 'Adicionar contato' }))
        await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
        expect(chamadas.find((c) => c.caminho === '/api/v1/clientes/c1/contatos')?.corpo).toEqual({
            nome: 'João',
            email: 'joao@bomgrao.com',
            telefone: null,
            cargo: null,
        })
    })

    it('inativar pede confirmação e manda a versão', async () => {
        const chamadas = app(GERENCIAR, {
            'GET /api/v1/clientes/c1': () => json(padaria),
            'DELETE /api/v1/clientes/c1': () => new Response(null, { status: 204 }),
            'GET /api/v1/clientes': () => pagina([]),
        })
        const roteador = await abrir('/clientes/c1')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Inativar' }))
        const janela = await screen.findByRole('dialog', { name: 'Inativar Padaria Bom Grão?' })
        expect(within(janela).getByText(/perdem o acesso ao portal/)).toBeInTheDocument()
        await usuario.click(within(janela).getByRole('button', { name: 'Inativar cliente' }))
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/clientes'))
        expect(chamadas.find((c) => c.metodo === 'DELETE')?.ifMatch).toBe('"42"')
        expect(await screen.findByText('Padaria Bom Grão foi inativado.')).toBeInTheDocument()
    })

    it('cliente que não existe mostra "Não encontramos isso"', async () => {
        app(VER, { 'GET /api/v1/clientes/x': () => json({ status: 404, detail: 'Não encontrado.' }, 404) })
        await abrir('/clientes/x')
        expect(await screen.findByText('Não encontramos isso')).toBeInTheDocument()
    })

    it('o CEP preenche o endereço, as máscaras formatam e tudo vai no corpo', async () => {
        const chamadas = app(GERENCIAR, {
            'GET /api/v1/clientes': () => pagina([]),
            'POST /api/v1/clientes': () => json(padaria, 201),
            'GET /api/v1/clientes/c1': () => json(padaria),
        })
        const viaCep = vi.fn(async () =>
            Response.json({ logradouro: 'Rua das Flores', bairro: 'Centro', localidade: 'Bastos', uf: 'SP' }),
        )
        const fetchDaApi = globalThis.fetch
        vi.stubGlobal('fetch', (alvo: Request | string, opcoes?: RequestInit) =>
            typeof alvo === 'string' && alvo.startsWith('https://viacep.com.br')
                ? viaCep()
                : fetchDaApi(alvo as Request, opcoes),
        )
        await abrir('/clientes')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Novo cliente' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo cliente' })
        await usuario.type(within(janela).getByLabelText('Nome'), 'Padaria Bom Grão')
        await usuario.type(within(janela).getByLabelText('CPF ou CNPJ'), '11222333000181')
        await usuario.type(within(janela).getByLabelText('Telefone'), '1833330000')
        await usuario.type(within(janela).getByLabelText('CEP'), '19700000')
        expect(within(janela).getByLabelText('CPF ou CNPJ')).toHaveValue('11.222.333/0001-81')
        expect(within(janela).getByLabelText('Telefone')).toHaveValue('(18) 3333-0000')
        await vi.waitFor(() => expect(within(janela).getByLabelText('Logradouro')).toHaveValue('Rua das Flores'))
        expect(within(janela).getByLabelText('Cidade')).toHaveValue('Bastos')
        expect(within(janela).getByLabelText('Número')).toHaveFocus()
        await usuario.keyboard('120')
        const etiquetas = within(janela).getByLabelText('Etiquetas')
        await usuario.type(etiquetas, 'Varejo{Enter}varejo{Enter}Centro,')
        await usuario.click(within(janela).getByRole('button', { name: 'Criar cliente' }))
        await screen.findByRole('heading', { level: 1, name: 'Padaria Bom Grão' })
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/clientes')?.corpo).toMatchObject({
            documento: '11.222.333/0001-81',
            telefone: '(18) 3333-0000',
            endereco: {
                cep: '19700-000',
                logradouro: 'Rua das Flores',
                numero: '120',
                complemento: null,
                bairro: 'Centro',
                cidade: 'Bastos',
                uf: 'SP',
            },
            etiquetas: ['Varejo', 'Centro'],
        })
    })

    it('o erro do endereço aparece no campo', async () => {
        app(GERENCIAR, {
            'GET /api/v1/clientes': () => pagina([]),
            'POST /api/v1/clientes': () =>
                json(
                    {
                        type: 'https://acordu.app.br/erros/validacao',
                        status: 400,
                        errors: { 'endereco.uf': ['UF inválida.'] },
                    },
                    400,
                ),
        })
        await abrir('/clientes')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Novo cliente' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo cliente' })
        await usuario.type(within(janela).getByLabelText('Nome'), 'X')
        await usuario.type(within(janela).getByLabelText('Logradouro'), 'Rua A')
        // Começou o endereço: o mínimo passa a ser obrigatório.
        expect(within(janela).getByLabelText('CEP')).toBeRequired()
        expect(within(janela).getByLabelText('Cidade')).toBeRequired()
        expect(within(janela).getByRole('combobox', { name: 'UF' })).toBeRequired()
        expect(within(janela).getByLabelText('Número')).not.toBeRequired()
        await usuario.type(within(janela).getByLabelText('CEP'), '19700000')
        await usuario.type(within(janela).getByLabelText('Cidade'), 'Bastos')
        await usuario.type(within(janela).getByRole('combobox', { name: 'UF' }), 'sp')
        await usuario.click(await screen.findByRole('option', { name: 'SP' }))
        await usuario.click(within(janela).getByRole('button', { name: 'Criar cliente' }))
        expect(await within(janela).findByText('UF inválida.')).toBeInTheDocument()
    })

    it('a ficha mostra o cadastro formatado', async () => {
        app(VER, {
            'GET /api/v1/clientes/c1': () =>
                json({
                    ...padaria,
                    razaoSocial: 'Bom Grão Panificadora Ltda',
                    documento: '11222333000181',
                    inscricaoEstadual: 'ISENTO',
                    telefone: '18999990000',
                    endereco: {
                        cep: '19700000',
                        logradouro: 'Rua das Flores',
                        numero: '120',
                        complemento: null,
                        bairro: 'Centro',
                        cidade: 'Bastos',
                        uf: 'SP',
                    },
                    responsavel: { id: 'u1', nome: 'Ana Prado' },
                    etiquetas: ['Varejo'],
                }),
        })
        await abrir('/clientes/c1')
        expect(await screen.findByText('11.222.333/0001-81')).toBeInTheDocument()
        expect(screen.getByText('(18) 99999-0000')).toBeInTheDocument()
        expect(screen.getByText(/Centro · Bastos\/SP · 19700-000/)).toBeInTheDocument()
        const ficha = within(screen.getByRole('main'))
        expect(ficha.getByText('Ana Prado')).toBeInTheDocument()
        expect(ficha.getByText('Varejo')).toBeInTheDocument()
    })

    it('filtrar por etiqueta vai para o endereço e para a API', async () => {
        const chamadas = app(VER, { 'GET /api/v1/clientes': () => pagina([]) })
        const roteador = await abrir('/clientes')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByRole('combobox', { name: 'Etiqueta' }), 'vare')
        await usuario.click(await screen.findByRole('option', { name: 'Varejo' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ etiqueta: 'Varejo' }))
        await vi.waitFor(() =>
            expect(
                chamadas
                    .filter((c) => c.caminho === '/api/v1/clientes')
                    .at(-1)
                    ?.consulta.get('etiqueta'),
            ).toBe('Varejo'),
        )
    })

    it('cliente inativo pode ser reativado, sem confirmação', async () => {
        const chamadas = app(GERENCIAR, {
            'GET /api/v1/clientes/c1': () =>
                json({
                    ...padaria,
                    ativo: false,
                    contatos: [
                        {
                            ...padaria.contatos[0],
                            id: 'k2',
                            nome: 'Pedro',
                            email: 'pedro@x.com',
                            ativo: false,
                            acessoPortal: null,
                        },
                    ],
                }),
            'POST /api/v1/clientes/c1/reativacao': () => json(padaria),
        })
        await abrir('/clientes/c1')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Reativar cliente' }))
        expect(await screen.findByText(/foi reativado/)).toBeInTheDocument()
        const chamada = chamadas.find((c) => c.caminho === '/api/v1/clientes/c1/reativacao')
        expect(chamada?.ifMatch).toBe('"42"')
    })

    it('contatos inativos ficam escondidos e podem ser reativados', async () => {
        const chamadas = app(GERENCIAR, {
            'GET /api/v1/clientes/c1': () =>
                json({
                    ...padaria,
                    contatos: [
                        ...padaria.contatos,
                        {
                            ...padaria.contatos[0],
                            id: 'k2',
                            nome: 'Pedro',
                            email: 'pedro@x.com',
                            ativo: false,
                            acessoPortal: null,
                        },
                    ],
                }),
            'POST /api/v1/clientes/c1/contatos/k2/reativacao': () => json({}),
        })
        await abrir('/clientes/c1?secao=contatos')
        const usuario = userEvent.setup()
        expect(await screen.findByText('Marta Souza')).toBeInTheDocument()
        expect(screen.queryByText(/Pedro/)).not.toBeInTheDocument()
        await usuario.click(screen.getByRole('button', { name: 'Ver contatos inativos (1)' }))
        await usuario.click(screen.getByRole('button', { name: 'Reativar Pedro' }))
        await vi.waitFor(() =>
            expect(chamadas.some((c) => c.caminho === '/api/v1/clientes/c1/contatos/k2/reativacao')).toBe(true),
        )
        expect(await screen.findByText('Pedro foi reativado.')).toBeInTheDocument()
    })

    it('documento com dígito errado mostra o erro no campo e não salva (F10-22)', async () => {
        const chamadas = app(GERENCIAR, { 'GET /api/v1/clientes': () => pagina([]) })
        await abrir('/clientes')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Novo cliente' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo cliente' })
        await usuario.type(within(janela).getByLabelText('Nome'), 'Padaria Bom Grão')
        await usuario.type(within(janela).getByLabelText('CPF ou CNPJ'), '11222333000180')
        await usuario.tab()
        expect(await within(janela).findByText('CPF ou CNPJ inválido.')).toBeInTheDocument()
        await usuario.click(within(janela).getByRole('button', { name: 'Criar cliente' }))
        expect(await within(janela).findByText('CPF ou CNPJ inválido.')).toBeInTheDocument()
        expect(chamadas.some((c) => c.metodo === 'POST' && c.caminho === '/api/v1/clientes')).toBe(false)
    })

    it('aceita CNPJ alfanumérico, com letras em maiúsculas (F10-24)', async () => {
        app(GERENCIAR, { 'GET /api/v1/clientes': () => pagina([]) })
        await abrir('/clientes')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Novo cliente' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo cliente' })
        await usuario.type(within(janela).getByLabelText('CPF ou CNPJ'), '12abc34501de35')
        expect(within(janela).getByLabelText('CPF ou CNPJ')).toHaveValue('12.ABC.345/01DE-35')
    })

    it('lista e ficha mostram o CNPJ alfanumérico formatado, com as letras (F10-27)', async () => {
        const alfanumerico = { ...padaria, documento: '12ABC34501DE35' }
        app(VER, {
            'GET /api/v1/clientes': () => pagina([alfanumerico]),
            'GET /api/v1/clientes/c1': () => json(alfanumerico),
        })
        await abrir('/clientes')
        expect(await screen.findByText('12.ABC.345/01DE-35', { exact: false })).toBeInTheDocument()
        await abrir('/clientes/c1')
        expect(await screen.findByText('12.ABC.345/01DE-35', { exact: false })).toBeInTheDocument()
    })
})
