/**
 * Notas de versão (marco do manual, M4): escritas à mão, em linguagem de quem usa, no mesmo PR da mudança visível
 * (convenções §4). Até o lançamento, cada nota é identificada pela data; depois do 1.0, pela versão (1.1, 1.2…).
 * `publico`: "equipe" aparece em Novidades; "cliente", no guia do portal. Mais recente primeiro.
 */
export interface NotaVersao {
    data: string
    versao?: string
    publico: 'equipe' | 'cliente'
    titulo: string
    itens: string[]
}

export const NOTAS: NotaVersao[] = [
    {
        data: '2026-11-04',
        publico: 'equipe',
        titulo: 'Ver a senha enquanto digita',
        itens: [
            'Os campos de senha (entrar, criar conta, nova senha e aceite de convite) ganharam o botão de olho para mostrar o que foi digitado. A senha começa oculta.',
        ],
    },
    {
        data: '2026-11-04',
        publico: 'cliente',
        titulo: 'Ver a senha enquanto digita',
        itens: ['Ao entrar ou aceitar o convite do portal, o botão de olho mostra a senha que você digitou.'],
    },
    {
        data: '2026-11-04',
        publico: 'equipe',
        titulo: 'Cliente pelo CNPJ',
        itens: [
            'Ao digitar o CNPJ de um cliente, o Acordu busca os dados abertos da Receita e preenche o que estiver vazio: nome, razão social, endereço, telefone e e-mail. O que você já digitou não muda.',
            'Se a empresa estiver baixada, inapta, suspensa ou nula na Receita, aparece um aviso. Dá para salvar mesmo assim.',
            'Telefone e e-mail da Receita costumam ser do escritório de contabilidade: confira antes de salvar.',
        ],
    },
    {
        data: '2026-11-04',
        publico: 'equipe',
        titulo: 'CNPJ com letras',
        itens: [
            'O cadastro do cliente aceita o novo CNPJ com letras (como 12.ABC.345/01DE-35), e a busca da lista também o encontra.',
            'CPF ou CNPJ com dígito errado agora aparece como inválido já no campo, antes de salvar.',
        ],
    },
    {
        data: '2026-10-23',
        publico: 'equipe',
        titulo: 'Escolha o couro',
        itens: [
            'Além do Café, agora dá para usar os couros Conhaque e Bordô, no claro e no escuro. A escolha fica no menu com o seu nome e vale só para você, neste navegador.',
        ],
    },
    {
        data: '2026-10-23',
        publico: 'equipe',
        titulo: 'Primeiros passos no Hoje',
        itens: [
            'Para quem configura a empresa, o Hoje mostra os primeiros passos com o progresso: o que já foi feito (empresa, calendário, equipe, clientes, contratos, portal, solicitações, tarefas e recorrências) e o que falta.',
            'O cartão some quando tudo estiver feito, ou com "Dispensar".',
            'E-mails com acento antes do @ (como "joão@…") agora são recusados no cadastro, nos contatos e nos convites: as mensagens para esses endereços não chegavam.',
        ],
    },
    {
        data: '2026-10-23',
        publico: 'equipe',
        titulo: 'Manual, novidades e entregas no portal',
        itens: [
            'Ajuda: o manual do Acordu dentro do sistema, com a explicação de cada tela, os primeiros passos e imagens das telas.',
            'O botão "?" no topo de cada tela abre a parte do manual que fala dela.',
            'Novidades: esta tela, com o que muda a cada atualização. Um ponto no menu avisa quando há algo novo.',
            'No Hoje, o bloco "Novidades" passou a se chamar "Avisos": são os mesmos avisos do sino.',
            'Portal: a entrega esperando aprovação agora aparece para o cliente em "Esperando você", e não mais em "Concluídas".',
            'Portal: o cliente ganhou uma Ajuda com um guia curto de como usar.',
        ],
    },
    {
        data: '2026-10-23',
        publico: 'cliente',
        titulo: 'Ajuda no portal',
        itens: [
            'Ajuda: um guia curto de como abrir, acompanhar e aprovar as suas solicitações.',
            'Entregas para aprovar agora ficam em "Esperando você", junto com o que espera a sua resposta.',
        ],
    },
]

export const notasDo = (publico: NotaVersao['publico']) => NOTAS.filter((n) => n.publico === publico)

/** "23 de outubro de 2026". */
export const dataDaNota = (data: string) =>
    new Intl.DateTimeFormat('pt-BR', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' }).format(
        new Date(`${data}T12:00:00Z`),
    )
