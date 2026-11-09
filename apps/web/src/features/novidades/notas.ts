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
        data: '2026-11-09',
        publico: 'equipe',
        titulo: 'Cronômetro ajustável',
        itens: [
            'Esqueceu de iniciar o cronômetro? "Começou antes?", no painel da tarefa, começa a contar a partir do horário informado, de até 12 horas atrás.',
            'Ao parar, a duração vem preenchida e pode ser ajustada antes de apontar. O apontamento sai no dia em que o cronômetro começou.',
            'Na janela de nova tarefa, "Iniciar o cronômetro" já começa a contar na tarefa criada.',
        ],
    },
    {
        data: '2026-11-09',
        publico: 'equipe',
        titulo: 'Projeto a partir da solicitação',
        itens: [
            'No detalhe da solicitação, "Criar projeto" abre o projeto novo com o cliente, o contrato e o título dela, e leva as tarefas abertas para o projeto.',
            'Concluir ou cancelar um projeto oferece fazer o mesmo com as tarefas abertas dele, e concluir a última tarefa sugere concluir o projeto.',
        ],
    },
    {
        data: '2026-11-09',
        publico: 'equipe',
        titulo: 'Tarefa muda de projeto',
        itens: [
            'No painel da tarefa, o campo Projeto leva a tarefa para um projeto em andamento do mesmo cliente, troca de projeto ou tira do projeto. O aviso tem "Desfazer".',
            'As horas já apontadas vão junto para o progresso do projeto novo e continuam no contrato em que foram lançadas.',
        ],
    },
    {
        data: '2026-11-09',
        publico: 'equipe',
        titulo: 'Solicitação e tarefas andam juntas',
        itens: [
            'Ao resolver uma solicitação, as tarefas que ainda estão abertas aparecem na janela e podem ser concluídas junto. Ao cancelar, podem ser canceladas junto.',
            'Concluiu a última tarefa de uma solicitação? O aviso oferece "Resolver" na hora.',
            'Criar uma tarefa numa solicitação ou iniciar o cronômetro já mostra que o trabalho começou: a solicitação sai de "Aberta" e a tarefa vai para "Fazendo".',
        ],
    },
    {
        data: '2026-11-09',
        publico: 'equipe',
        titulo: 'Solicitação encerrada é encerrada',
        itens: [
            'Solicitação fechada ou cancelada não recebe mais resposta ao cliente nem anexo. A nota interna continua, para registros como "o cliente ligou agradecendo".',
        ],
    },
    {
        data: '2026-11-09',
        publico: 'cliente',
        titulo: 'Assunto novo, solicitação nova',
        itens: ['Uma solicitação encerrada não recebe mais mensagens: para continuar, abra uma nova solicitação.'],
    },
    {
        data: '2026-11-06',
        publico: 'equipe',
        titulo: 'Responder ao cliente ficou mais direto',
        itens: [
            'Na conversa da solicitação, "Resposta ao cliente" agora vem marcada. A nota interna continua a um clique, com fundo diferente e o botão "Salvar nota interna".',
            '"Enviar e aguardar o cliente" responde e já põe a solicitação esperando o retorno, numa ação só. O cliente recebe um único e-mail.',
            'A primeira resposta ao cliente tira a solicitação de "Aberta" sozinha.',
        ],
    },
    {
        data: '2026-11-05',
        publico: 'equipe',
        titulo: '"Horas" agora é "Horas trabalhadas"',
        itens: ['A tela dos apontamentos da semana mudou de nome no menu: Horas trabalhadas. O conteúdo é o mesmo.'],
    },
    {
        data: '2026-11-05',
        publico: 'equipe',
        titulo: 'Cliente pelo CNPJ, mais certeiro',
        itens: [
            'No cadastro do cliente, CPF ou CNPJ agora é o primeiro campo: digite o CNPJ e o resto se preenche.',
            'Para empresário individual (MEI), os dados abertos da Receita não trazem rua nem telefone: o Acordu usa CEP, bairro e cidade e completa a rua pelo CEP.',
            'O e-mail não vem nos dados abertos da Receita; preencha à mão.',
        ],
    },
    {
        data: '2026-11-05',
        publico: 'equipe',
        titulo: 'Menu na ordem de uso',
        itens: [
            'A barra lateral agora segue a ordem do trabalho, em grupos: Hoje e Agenda; Cadastros (Clientes e Contratos); Atendimento (Solicitações, Tarefas, Projetos e Recorrências); Acompanhamento (Horas e Panorama); e, no fim, Configurações, Novidades e Ajuda.',
        ],
    },
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
        titulo: 'Mostrar a senha no portal',
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
