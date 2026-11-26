import type { CapturaAjuda } from '@/features/ajuda/capturas'

/**
 * Guia do portal (marco do manual, M3): curto, em linguagem de cliente, versionado com as telas do portal. PR que muda
 * uma tela do portal atualiza a seção dela no mesmo PR (convenções §4). `soComContratos`: só para quem vê contratos.
 */
export interface SecaoAjudaPortal {
    id: string
    titulo: string
    resumo: string
    passos?: string[]
    dicas?: string[]
    soComContratos?: boolean
    imagens?: { captura: CapturaAjuda; legenda: string }[]
}

export const SECOES_AJUDA_PORTAL: SecaoAjudaPortal[] = [
    {
        id: 'acompanhar',
        titulo: 'Acompanhar as solicitações',
        resumo: 'A tela Solicitações mostra tudo o que você pediu à equipe, separado em três abas.',
        passos: [
            '"Esperando você" é o que depende de uma resposta sua ou de uma entrega para aprovar. O número ao lado mostra quantas são.',
            '"Em andamento" é o que a equipe está cuidando agora.',
            '"Concluídas" guarda o que já foi encerrado ou cancelado.',
        ],
        imagens: [{ captura: 'portal-lista', legenda: 'As solicitações, com a aba "Esperando você" em destaque.' }],
    },
    {
        id: 'nova',
        titulo: 'Abrir uma solicitação',
        resumo: 'Pelo botão "Nova solicitação", na tela Solicitações.',
        passos: [
            'Escreva um assunto curto, como "Site fora do ar".',
            'Conte o que está acontecendo: onde, desde quando e o que já tentou. Quanto mais detalhe, mais rápido a equipe resolve.',
            'Escolha a urgência que você sugere, de "Quando der" até "Parou tudo". A equipe confirma a prioridade.',
            'Se ajudar, anexe prints ou arquivos (até 5, de até 20 MB cada).',
        ],
        imagens: [{ captura: 'portal-nova', legenda: 'O formulário de nova solicitação, com a urgência sugerida.' }],
    },
    {
        id: 'conversar',
        titulo: 'Conversar com a equipe',
        resumo: 'Abra uma solicitação para ver o andamento e a conversa.',
        passos: [
            'No alto, o andamento: quem está cuidando, a previsão e as etapas que a equipe deixou visíveis.',
            'Embaixo, a conversa. Escreva e envie; dá para anexar arquivos.',
            'Quando a equipe está esperando por você, responder já retoma o atendimento.',
        ],
        dicas: [
            'As respostas da equipe também chegam por e-mail.',
            'Cada mensagem mostra por onde chegou: "Acordu", quando é da equipe, ou "Portal", quando você escreveu por aqui.',
            'Se aparecer "No momento não é possível enviar por aqui", fale direto com a empresa que atende você. O que já foi enviado continua aparecendo aqui.',
        ],
    },
    {
        id: 'aprovar',
        titulo: 'Aprovar uma entrega',
        resumo: 'Quando a equipe termina, a solicitação vai para "Esperando você" com a entrega para conferir.',
        passos: [
            'Se ficou como combinado, use "Aprovar entrega": a solicitação é encerrada.',
            'Se faltou algo, use "Pedir ajuste" e conte o que precisa mudar. A equipe retoma o atendimento.',
        ],
        dicas: ['Sem resposta, a solicitação é encerrada sozinha depois de alguns dias.'],
        imagens: [
            {
                captura: 'portal-detalhe',
                legenda: 'Uma solicitação resolvida, com os botões para aprovar ou pedir ajuste.',
            },
        ],
    },
    {
        id: 'contratos',
        titulo: 'Conferir o contrato',
        resumo: 'A tela Contratos mostra o que foi contratado e quanto já foi usado no mês.',
        passos: [
            'Escolha o contrato (se houver mais de um) e o mês.',
            'A régua mostra as horas usadas; no banco de horas, também o saldo.',
            'Embaixo, as horas do mês linha a linha. Atendimentos que a equipe não detalhou aparecem como "Atendimento".',
        ],
        soComContratos: true,
        imagens: [{ captura: 'portal-contratos', legenda: 'O extrato do mês de um banco de horas.' }],
    },
    {
        id: 'privacidade',
        titulo: 'Seus dados',
        resumo: 'Quem cuida dos dados que você manda por aqui é a empresa que atende você.',
        dicas: [
            'O link "Política de privacidade", no pé do portal, explica como o Acordu trata os dados.',
            'Para ver, corrigir ou pedir a exclusão dos seus dados, fale com a empresa que atende você.',
        ],
    },
    {
        id: 'link',
        titulo: 'Acesso sem senha',
        resumo: 'A equipe pode mandar um link para você acompanhar uma solicitação sem entrar com conta.',
        dicas: [
            'O link abre só aquela solicitação e vale por um tempo limitado.',
            'Se recarregar a página, abra o link do e-mail de novo.',
        ],
    },
]
