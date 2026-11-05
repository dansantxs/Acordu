import type { CaminhoNavegacao } from '@/features/casca/navegacao'
import type { CapturaAjuda } from './capturas'

/**
 * Conteúdo do manual (marco do manual, decisão 2): texto no código, versionado junto com as telas. Quem muda uma tela
 * atualiza a seção dela no mesmo PR (convenções §4). `permissoes`: qualquer uma basta; vazio = todos da equipe.
 */
export interface SecaoAjuda {
    id: string
    titulo: string
    grupo: 'comecar' | 'telas' | 'conceitos'
    permissoes: string[]
    /** Tela que a seção explica, para o botão "Abrir a tela". */
    tela?: CaminhoNavegacao | '/conta'
    resumo: string
    passos?: string[]
    dicas?: string[]
    /** Capturas da tela com a empresa de demonstração (geradas pelo bot; ver `capturas.ts`). */
    imagens?: { captura: CapturaAjuda; legenda: string }[]
}

/** O que mostra que o passo foi feito, no cartão do Hoje (M5). Sem chave, o passo é só orientação. */
export type ChaveProgresso =
    | 'empresa'
    | 'calendario'
    | 'equipe'
    | 'clientes'
    | 'contratos'
    | 'portal'
    | 'solicitacoes'
    | 'tarefas'
    | 'recorrencias'

export interface PassoInicial {
    titulo: string
    progresso?: ChaveProgresso
    texto: string
    permissoes: string[]
    para: CaminhoNavegacao | '/configuracoes/empresa' | '/configuracoes/calendario' | '/configuracoes/equipe'
    secao: string
}

/** A ordem de uso de uma empresa nova (Primeiros passos). */
export const PRIMEIROS_PASSOS: PassoInicial[] = [
    {
        titulo: 'Configure a empresa',
        progresso: 'empresa',
        texto: 'Nome, para onde vão as respostas dos clientes e o SLA padrão dos contratos.',
        permissoes: ['configuracao.gerenciar'],
        para: '/configuracoes/empresa',
        secao: 'configuracoes',
    },
    {
        titulo: 'Ajuste o calendário',
        progresso: 'calendario',
        texto: 'Fuso, horário de cada dia e feriados: é com eles que o SLA conta as horas úteis.',
        permissoes: ['configuracao.gerenciar'],
        para: '/configuracoes/calendario',
        secao: 'configuracoes',
    },
    {
        titulo: 'Convide a equipe',
        progresso: 'equipe',
        texto: 'Cada pessoa entra com um papel (Dono, Membro, Leitor ou um sob medida) e uma capacidade semanal.',
        permissoes: ['usuario.convidar'],
        para: '/configuracoes/equipe',
        secao: 'configuracoes',
    },
    {
        titulo: 'Cadastre os clientes',
        progresso: 'clientes',
        texto: 'Com os contatos de cada um: são eles que recebem os avisos e podem usar o portal.',
        permissoes: ['cliente.gerenciar'],
        para: '/clientes',
        secao: 'clientes',
    },
    {
        titulo: 'Crie os contratos',
        progresso: 'contratos',
        texto: 'Mensal fixo, banco de horas ou avulso, com os prazos de SLA combinados.',
        permissoes: ['contrato.gerenciar'],
        para: '/contratos',
        secao: 'contratos',
    },
    {
        titulo: 'Convide os contatos para o portal',
        progresso: 'portal',
        texto: 'Na ficha do cliente, aba Contatos e portal. O cliente passa a abrir e acompanhar solicitações sozinho.',
        permissoes: ['portal.gerenciar_acessos'],
        para: '/clientes',
        secao: 'clientes',
    },
    {
        titulo: 'Atenda as solicitações',
        progresso: 'solicitacoes',
        texto: 'A fila mostra o que chegou, com o prazo de cada uma. Assuma, converse com o cliente e resolva.',
        permissoes: ['solicitacao.ver'],
        para: '/solicitacoes',
        secao: 'solicitacoes',
    },
    {
        titulo: 'Organize o trabalho em tarefas',
        progresso: 'tarefas',
        texto: 'Tarefas soltas, de uma solicitação ou de um projeto; o cronômetro aponta as horas ao parar.',
        permissoes: ['tarefa.ver'],
        para: '/tarefas',
        secao: 'tarefas',
    },
    {
        titulo: 'Automatize o que se repete',
        progresso: 'recorrencias',
        texto: 'Recorrências criam sozinhas a tarefa do fechamento do mês, da revisão da semana…',
        permissoes: ['recorrencia.gerenciar'],
        para: '/recorrencias',
        secao: 'recorrencias',
    },
    {
        titulo: 'Acompanhe',
        texto: 'Agenda para planejar a semana, Panorama para os números do mês e Horas para conferir o apontado.',
        permissoes: [],
        para: '/agenda',
        secao: 'agenda',
    },
]

export const SECOES_AJUDA: SecaoAjuda[] = [
    {
        id: 'atalhos',
        titulo: 'Como o Acordu funciona',
        grupo: 'comecar',
        permissoes: [],
        resumo: 'Algumas coisas valem para todas as telas e economizam tempo.',
        dicas: [
            'A barra lateral segue a ordem do trabalho: Hoje e Agenda; Cadastros (clientes e contratos, feitos antes de atender); Atendimento (solicitações, tarefas, projetos e recorrências); Acompanhamento (horas e panorama); e, no fim, Configurações, Novidades e Ajuda. As seções das telas, aqui no manual, seguem a mesma ordem.',
            'Ctrl+K (ou Cmd+K no Mac) abre a busca: vá para qualquer tela, encontre um cliente ou uma solicitação pelo número e crie coisas novas sem sair de onde está.',
            'Os filtros ficam no endereço da página: copie o link para mostrar a alguém exatamente a mesma lista, ou volte a ela pelo histórico.',
            'Detalhes abrem num painel ao lado da lista. Esc fecha o painel e devolve você à linha de onde saiu.',
            'Várias mudanças mostram um aviso com "Desfazer" por alguns segundos: trocar responsável, prioridade, mover no quadro, inativar categoria.',
            'Tudo que se arrasta também tem o menu "⋯ Mover para", para usar pelo teclado ou no celular.',
            'As telas se atualizam sozinhas quando alguém da equipe muda algo: não é preciso recarregar.',
            'O tema claro ou escuro e o couro (Café, Conhaque ou Bordô) ficam no menu com o seu nome, no pé da barra lateral. A escolha vale só para você, neste navegador.',
            'O botão "?" no topo de cada tela abre a explicação dela aqui.',
        ],
    },
    {
        id: 'novidades',
        titulo: 'Novidades',
        grupo: 'comecar',
        permissoes: [],
        tela: '/novidades',
        resumo: 'O que muda no Acordu a cada atualização, explicado para quem usa.',
        dicas: [
            'Um ponto ao lado de "Novidades", na barra lateral, avisa quando há algo que você ainda não viu. Abrir a tela apaga o ponto.',
            'O "já vi" fica guardado neste navegador: em outro computador, o ponto aparece de novo.',
        ],
    },
    {
        id: 'hoje',
        titulo: 'Hoje',
        grupo: 'telas',
        permissoes: [],
        tela: '/',
        resumo: 'O seu início: o que está com você hoje, o que espera o cliente, a sua semana e as horas que já apontou.',
        passos: [
            '"Solicitações com você" vem pelo prazo de solução: a bolinha mostra o SLA (verde no prazo, âmbar em risco, vermelha estourada).',
            '"Tarefas de hoje" junta as que estão em andamento, as planejadas para hoje e as atrasadas.',
            '"Esperando resposta" lembra o que está parado com o cliente ou com terceiros.',
            '"Sua semana" compara as horas planejadas com a sua capacidade, dia a dia.',
            '"Avisos" repete os últimos avisos do sino.',
        ],
        dicas: [
            'Para quem configura a empresa, enquanto ela é nova, o cartão "Primeiros passos" mostra o que já foi feito e o que falta. "Dispensar" esconde o cartão neste navegador; ele também some quando tudo estiver feito.',
        ],
        imagens: [
            {
                captura: 'hoje',
                legenda: 'O Hoje de quem atende: solicitações com você, tarefas do dia, a semana e as horas apontadas.',
            },
        ],
    },
    {
        id: 'agenda',
        titulo: 'Agenda',
        grupo: 'telas',
        permissoes: ['solicitacao.ver', 'tarefa.ver', 'projeto.ver', 'recorrencia.ver', 'contrato.ver'],
        tela: '/agenda',
        resumo: 'A visão da equipe no tempo: o que vence, o que está planejado e quem está sobrecarregado.',
        passos: [
            'Linha do tempo: duas semanas úteis, com uma faixa por pessoa (ou por cliente). Projetos aparecem como barras; recorrências futuras e o fim de contratos, tracejados.',
            'A coluna "Atrasados" mostra o que ficou para trás. A coluna de hoje fica destacada.',
            'Plano da semana: uma coluna por pessoa e uma linha por dia. Arraste uma tarefa para outro dia ou para outra pessoa para replanejar; o aviso tem "Desfazer".',
            'No cabeçalho de cada pessoa, as horas planejadas contra a capacidade da semana.',
        ],
        dicas: [
            'Solicitações seguem o prazo do SLA e recorrências seguem a regra: aparecem, mas não se arrastam.',
            'O que cai no sábado ou domingo aparece na sexta.',
        ],
        imagens: [
            { captura: 'agenda', legenda: 'Linha do tempo com uma faixa por pessoa nas duas semanas úteis.' },
            { captura: 'agenda-plano', legenda: 'Plano da semana: uma coluna por pessoa, uma linha por dia.' },
        ],
    },
    {
        id: 'clientes',
        titulo: 'Clientes',
        grupo: 'telas',
        permissoes: ['cliente.ver'],
        tela: '/clientes',
        resumo: 'O cadastro dos clientes e, na ficha de cada um, tudo o que acontece com ele.',
        passos: [
            'Cadastre o cliente começando pelo CPF ou CNPJ: com o CNPJ, o Acordu busca os dados abertos da Receita e preenche o que estiver vazio (nome, razão social, endereço e telefone). Depois, responsável na equipe e etiquetas.',
            'Na ficha: Resumo (régua dos contratos), Em aberto, Contratos, Contatos e portal, Arquivos e Movimentos.',
            'Em "Contatos e portal", adicione os contatos e convide para o portal. "Só as próprias" mostra ao contato só as solicitações dele; "Todas do cliente", todas.',
        ],
        dicas: [
            'O telefone que vem da Receita costuma ser do escritório de contabilidade: confira antes de salvar. O e-mail não vem, e para empresário individual (MEI) a Receita só publica o CEP, que completa o endereço pelo ViaCEP. Se a empresa não estiver ativa na Receita, aparece um aviso, mas dá para salvar.',
            'Inativar um cliente tira o acesso de todos os contatos ao portal; reativar não devolve: é preciso convidar de novo.',
            '"Arquivos" junta os anexos de todas as conversas e comentários do cliente.',
        ],
        imagens: [
            { captura: 'cliente', legenda: 'A ficha de um cliente, com a régua do contrato e o que está em aberto.' },
        ],
    },
    {
        id: 'contratos',
        titulo: 'Contratos',
        grupo: 'telas',
        permissoes: ['contrato.ver'],
        tela: '/contratos',
        resumo: 'O que foi combinado com cada cliente: tipo, horas e prazos de SLA.',
        passos: [
            'Crie o contrato: mensal fixo (franquia por mês, opcional), banco de horas (horas compradas que acumulam) ou avulso.',
            'Os prazos de SLA já vêm com o padrão da empresa; ajuste ao combinado.',
            'Na página do contrato, acompanhe o consumo do mês na régua (marcas de 80% e 100%), as horas e os ajustes.',
            'Ao fim do mês, feche o mês (no contrato ou em lote, pelo botão "Fechar o mês" da lista).',
        ],
        dicas: [
            '"Ajustar saldo" registra horas extras compradas, cortesias ou correções, com motivo.',
            'Suspender pausa o contrato; encerrar é definitivo.',
        ],
        imagens: [{ captura: 'contrato', legenda: 'A página de um contrato de banco de horas, com o consumo do mês.' }],
    },
    {
        id: 'solicitacoes',
        titulo: 'Solicitações',
        grupo: 'telas',
        permissoes: ['solicitacao.ver'],
        tela: '/solicitacoes',
        resumo: 'A fila de atendimento: tudo que os clientes pedem, pelo portal, por e-mail ou aberto pela equipe.',
        passos: [
            'Abra uma solicitação pelo botão "Nova solicitação" ou pelo Ctrl+K. Escolha o cliente; o contrato ativo dele define o prazo de SLA.',
            'No painel da solicitação, assuma (ou passe para alguém), mude o status e a prioridade.',
            'Converse: "Nota interna" fica só na equipe (dá para mencionar alguém com @); "Resposta ao cliente" vai por e-mail e aparece no portal.',
            'Quando terminar, resolva. O cliente aprova a entrega pelo portal, ou a solicitação fecha sozinha depois dos dias configurados.',
            'Crie tarefas a partir da solicitação quando o trabalho tiver etapas.',
        ],
        dicas: [
            'Os filtros (cliente, responsável, prioridade, SLA) ficam no endereço: "Em risco" e "Sem responsável" são bons atalhos para o começo do dia.',
            '"Semelhantes" mostra solicitações já resolvidas parecidas, para reaproveitar a solução.',
            'Para quem não tem conta no portal, "Link do portal" manda um acesso sem senha só àquela solicitação.',
        ],
        imagens: [
            { captura: 'solicitacoes', legenda: 'A fila com o prazo de SLA de cada solicitação.' },
            { captura: 'solicitacao', legenda: 'O painel de uma solicitação, com a conversa e os dados ao lado.' },
        ],
    },
    {
        id: 'tarefas',
        titulo: 'Tarefas',
        grupo: 'telas',
        permissoes: ['tarefa.ver'],
        tela: '/tarefas',
        resumo: 'O trabalho do dia a dia, em lista ou em quadro (A fazer, Fazendo, Concluída).',
        passos: [
            'Crie uma tarefa solta, de uma solicitação ou de um projeto, com responsável, prazo, dia planejado e estimativa.',
            'No quadro, arraste o cartão entre as colunas; o aviso tem "Desfazer".',
            'No painel da tarefa: checklist, comentários com @menções, anexos e quem segue a tarefa.',
            'Use o cronômetro da tarefa: ele fica visível na barra lateral e, ao parar, abre o apontamento já preenchido.',
        ],
        dicas: [
            'Marque "O cliente vê esta tarefa" para ela aparecer como etapa no portal.',
            'Duração aceita "1h30", "1:30" ou "90" (minutos).',
        ],
        imagens: [{ captura: 'tarefas-quadro', legenda: 'O quadro de tarefas: arraste o cartão para mudar o status.' }],
    },
    {
        id: 'projetos',
        titulo: 'Projetos',
        grupo: 'telas',
        permissoes: ['projeto.ver'],
        tela: '/projetos',
        resumo: 'Entregas maiores, com prazo e estimativa, que agrupam várias tarefas.',
        passos: [
            'Crie o projeto com cliente, contrato, início, prazo e estimativa total.',
            'No painel, acompanhe o progresso (tarefas concluídas e horas apontadas contra a estimativa) e crie as tarefas do projeto.',
            'Conclua ou cancele quando acabar.',
        ],
        imagens: [{ captura: 'projetos', legenda: 'Os projetos com status, prazo e estimativa.' }],
    },
    {
        id: 'recorrencias',
        titulo: 'Recorrências',
        grupo: 'telas',
        permissoes: ['recorrencia.ver'],
        tela: '/recorrencias',
        resumo: 'Tarefas que se repetem e que o Acordu cria sozinho, já com responsável, checklist e prazo.',
        passos: [
            'Crie a recorrência: a tarefa (título, responsável, estimativa, checklist) e quando se repete (toda semana, todo mês, a cada 3 meses ou todo ano).',
            'Enquanto você preenche, "Próximas datas" mostra quando as tarefas vão nascer.',
            '"Criar a tarefa quantos dias antes" define a antecedência; o prazo é sempre a data.',
            'Pause para dar um tempo; encerre quando não fizer mais sentido.',
        ],
        dicas: ['Dia 31 em mês mais curto vira o último dia do mês.'],
        imagens: [{ captura: 'recorrencias', legenda: 'As recorrências com a regra e a próxima data.' }],
    },
    {
        id: 'horas',
        titulo: 'Horas',
        grupo: 'telas',
        permissoes: [],
        tela: '/horas',
        resumo: 'Os apontamentos da semana, agrupados por dia, com os totais.',
        passos: [
            'Aponte pelo cronômetro (parar = apontar) ou à mão, no painel da tarefa.',
            'Cada apontamento tem descrição interna e, se quiser, uma descrição para o cliente; marque se é faturável e se o cliente vê.',
            'Navegue de semana em semana. Quem vê as horas da equipe filtra por pessoa e por cliente.',
        ],
        dicas: [
            'As horas faturáveis consomem do contrato da solicitação (ou da tarefa) e aparecem no extrato do cliente.',
            'Mês fechado não aceita apontamentos novos nem edição.',
        ],
        imagens: [{ captura: 'horas', legenda: 'A semana de apontamentos, agrupada por dia.' }],
    },
    {
        id: 'panorama',
        titulo: 'Panorama',
        grupo: 'telas',
        permissoes: ['dashboard.ver'],
        tela: '/panorama',
        resumo: 'Os números da operação: fila, SLA, horas por cliente, consumo dos contratos e tarefas.',
        passos: [
            'Escolha o período: este mês, o mês passado ou um intervalo.',
            'Cada número leva à lista correspondente (por exemplo, "Sem responsável" abre a fila filtrada).',
        ],
        imagens: [
            {
                captura: 'panorama',
                legenda: 'O Panorama do mês: fila, SLA, horas por cliente e consumo dos contratos.',
            },
        ],
    },
    {
        id: 'configuracoes',
        titulo: 'Configurações',
        grupo: 'telas',
        permissoes: [
            'configuracao.gerenciar',
            'usuario.convidar',
            'usuario.remover',
            'papel.gerenciar',
            'auditoria.ver',
        ],
        tela: '/configuracoes',
        resumo: 'O que vale para a empresa toda. Cada seção aparece para quem tem a permissão dela.',
        passos: [
            'Empresa: nome, "Responder para" (onde chegam as respostas dos clientes) e SLA padrão dos contratos novos.',
            'Calendário: fuso, expediente de cada dia e feriados ("Sugerir feriados nacionais" preenche o ano).',
            'Portal: logotipo, nome que o cliente vê e dias para a solicitação resolvida fechar sozinha.',
            'Categorias: para organizar e filtrar as solicitações.',
            'Equipe: convites, papel e capacidade semanal de cada pessoa.',
            'Papéis: os padrões e os sob medida, com as permissões por área.',
            'Auditoria: quem fez o quê, com filtros e o histórico de cada item.',
        ],
        dicas: ['Mudar o calendário vale para as solicitações novas; as abertas mantêm o prazo que tinham.'],
        imagens: [{ captura: 'configuracoes', legenda: 'Configurações da empresa, com o menu das seções ao lado.' }],
    },
    {
        id: 'conta',
        titulo: 'Minha conta',
        grupo: 'telas',
        permissoes: [],
        tela: '/conta',
        resumo: 'O que é só seu: quais avisos chegam por e-mail e a sua agenda no calendário.',
        passos: [
            'Abra pelo menu com o seu nome, no pé da barra lateral.',
            'Marque quais avisos também chegam por e-mail; o sino continua mostrando tudo.',
            'Gere o link da agenda e cole no Google Agenda, Outlook ou Apple Calendário. O modo discreto esconde títulos e clientes.',
        ],
    },
    {
        id: 'sla',
        titulo: 'SLA em horas úteis',
        grupo: 'conceitos',
        permissoes: [],
        resumo: 'O prazo de cada solicitação conta só o expediente da empresa, sem fins de semana e feriados.',
        dicas: [
            'Os prazos vêm do contrato do cliente, por prioridade: primeira resposta e solução.',
            'Em risco: falta pouco para vencer. Violado: o prazo passou.',
            'Esperando o cliente, o relógio para; volta a contar quando ele responde.',
            'Cliente sem contrato ativo: a solicitação fica sem prazo.',
        ],
    },
    {
        id: 'cliente-ve',
        titulo: 'O que o cliente vê',
        grupo: 'conceitos',
        permissoes: [],
        resumo: 'O portal mostra uma versão simplificada do atendimento.',
        dicas: [
            'Status para o cliente: Recebida, Em andamento, Esperando você, Concluída ou Cancelada.',
            'Ele vê as respostas ao cliente, nunca as notas internas.',
            'Vê as tarefas marcadas como visíveis (como etapas) e o tempo dos apontamentos visíveis.',
            'No extrato do contrato, o total soma as horas faturáveis; o que não foi detalhado aparece como "Atendimento".',
        ],
    },
    {
        id: 'contratos-tipos',
        titulo: 'Tipos de contrato e fechamento do mês',
        grupo: 'conceitos',
        permissoes: [],
        resumo: 'Como as horas apontadas viram consumo.',
        dicas: [
            'Mensal fixo: franquia por mês; o que passa aparece como excedente.',
            'Banco de horas: horas compradas que vão sendo usadas; o saldo passa de um mês para o outro.',
            'Avulso: sem franquia, as horas são registradas e cobradas à parte.',
            'Fechar o mês trava os apontamentos daquele mês (ajustes de saldo continuam possíveis, com motivo): é o momento de faturar.',
        ],
    },
    {
        id: 'papeis',
        titulo: 'Papéis e permissões',
        grupo: 'conceitos',
        permissoes: [],
        resumo: 'O papel define o que cada pessoa vê e faz. Este manual também mostra só o que o seu papel alcança.',
        dicas: [
            'Dono: tudo. A empresa sempre tem ao menos um.',
            'Membro: o trabalho do dia a dia, sem gestão de contratos, configurações, equipe e auditoria.',
            'Leitor: só consulta.',
            'Papéis sob medida combinam as permissões que você escolher.',
            'Mudança de papel vale a partir do próximo acesso da pessoa.',
        ],
    },
]
