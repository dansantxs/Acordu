import type { Perfil } from '@/shared/sessao/perfil'

/** Destinos da capa lateral, na ordem do protótipo. As telas chegam no F2; até lá cada uma mostra o estado vazio. */
export interface ItemNavegacao {
    rotulo: string
    para: CaminhoNavegacao
    /** Basta uma: sem nenhuma, o item não aparece (a API responderia 403). Vazio = todos. */
    permissoes: string[]
    /** Linha antes do item, separando os grupos. */
    separar?: boolean
}

export type CaminhoNavegacao =
    | '/'
    | '/agenda'
    | '/solicitacoes'
    | '/tarefas'
    | '/clientes'
    | '/contratos'
    | '/projetos'
    | '/recorrencias'
    | '/panorama'
    | '/horas'
    | '/configuracoes'

export const NAVEGACAO: ItemNavegacao[] = [
    { rotulo: 'Hoje', para: '/', permissoes: [] },
    {
        rotulo: 'Agenda',
        para: '/agenda',
        permissoes: ['solicitacao.ver', 'tarefa.ver', 'projeto.ver', 'recorrencia.ver'],
    },
    { rotulo: 'Solicitações', para: '/solicitacoes', permissoes: ['solicitacao.ver'], separar: true },
    { rotulo: 'Tarefas', para: '/tarefas', permissoes: ['tarefa.ver'] },
    { rotulo: 'Clientes', para: '/clientes', permissoes: ['cliente.ver'] },
    { rotulo: 'Contratos', para: '/contratos', permissoes: ['contrato.ver'] },
    { rotulo: 'Projetos', para: '/projetos', permissoes: ['projeto.ver'] },
    { rotulo: 'Recorrências', para: '/recorrencias', permissoes: ['recorrencia.ver'] },
    { rotulo: 'Panorama', para: '/panorama', permissoes: ['dashboard.ver'], separar: true },
    { rotulo: 'Horas', para: '/horas', permissoes: [] },
    {
        rotulo: 'Configurações',
        para: '/configuracoes',
        permissoes: [
            'configuracao.gerenciar',
            'usuario.convidar',
            'papel.gerenciar',
            'portal.gerenciar_acessos',
            'auditoria.ver',
        ],
    },
]

export function pode(perfil: Perfil, ...permissoes: string[]): boolean {
    return permissoes.length === 0 || permissoes.some((p) => perfil.permissoes.includes(p))
}

export function navegacaoDe(perfil: Perfil): ItemNavegacao[] {
    return NAVEGACAO.filter((item) => pode(perfil, ...item.permissoes))
}
