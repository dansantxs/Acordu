import type { Perfil } from '@/shared/sessao/perfil'

/**
 * Destinos da capa lateral, na ordem de uso (pedido de 05/11): o dia (Hoje e Agenda), o que se cadastra antes de
 * trabalhar (clientes e contratos), o trabalho, o acompanhamento e, por último, depois de uma linha, o que é do sistema.
 */
export type GrupoNavegacao = 'dia' | 'cadastros' | 'atendimento' | 'acompanhamento' | 'sistema'

/** Título de cada grupo na capa; sem título, o grupo aparece só separado por uma linha. */
export const TITULOS_GRUPO: Record<GrupoNavegacao, string | null> = {
    dia: null,
    cadastros: 'Cadastros',
    atendimento: 'Atendimento',
    acompanhamento: 'Acompanhamento',
    sistema: null,
}

export interface ItemNavegacao {
    rotulo: string
    para: CaminhoNavegacao
    /** Basta uma: sem nenhuma, o item não aparece (a API responderia 403). Vazio = todos. */
    permissoes: string[]
    grupo: GrupoNavegacao
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
    | '/novidades'
    | '/ajuda'

export const NAVEGACAO: ItemNavegacao[] = [
    { rotulo: 'Hoje', para: '/', permissoes: [], grupo: 'dia' },
    {
        rotulo: 'Agenda',
        para: '/agenda',
        permissoes: ['solicitacao.ver', 'tarefa.ver', 'projeto.ver', 'recorrencia.ver'],
        grupo: 'dia',
    },
    { rotulo: 'Clientes', para: '/clientes', permissoes: ['cliente.ver'], grupo: 'cadastros' },
    { rotulo: 'Contratos', para: '/contratos', permissoes: ['contrato.ver'], grupo: 'cadastros' },
    { rotulo: 'Solicitações', para: '/solicitacoes', permissoes: ['solicitacao.ver'], grupo: 'atendimento' },
    { rotulo: 'Tarefas', para: '/tarefas', permissoes: ['tarefa.ver'], grupo: 'atendimento' },
    { rotulo: 'Projetos', para: '/projetos', permissoes: ['projeto.ver'], grupo: 'atendimento' },
    { rotulo: 'Recorrências', para: '/recorrencias', permissoes: ['recorrencia.ver'], grupo: 'atendimento' },
    { rotulo: 'Horas trabalhadas', para: '/horas', permissoes: [], grupo: 'acompanhamento' },
    { rotulo: 'Panorama', para: '/panorama', permissoes: ['dashboard.ver'], grupo: 'acompanhamento' },
    {
        rotulo: 'Configurações',
        para: '/configuracoes',
        permissoes: [
            // As mesmas das seções (features/configuracoes/secoes.ts); o portal dos contatos fica na ficha do cliente.
            'configuracao.gerenciar',
            'usuario.convidar',
            'usuario.remover',
            'papel.gerenciar',
            'auditoria.ver',
        ],
        grupo: 'sistema',
    },
    { rotulo: 'Novidades', para: '/novidades', permissoes: [], grupo: 'sistema' },
    { rotulo: 'Ajuda', para: '/ajuda', permissoes: [], grupo: 'sistema' },
]

export function pode(perfil: Perfil, ...permissoes: string[]): boolean {
    return permissoes.length === 0 || permissoes.some((p) => perfil.permissoes.includes(p))
}

export function navegacaoDe(perfil: Perfil): ItemNavegacao[] {
    return NAVEGACAO.filter((item) => pode(perfil, ...item.permissoes))
}

/** Os destinos do papel em grupos, na ordem; grupo sem nenhum item visível não aparece. */
export function gruposDe(perfil: Perfil): { grupo: GrupoNavegacao; titulo: string | null; itens: ItemNavegacao[] }[] {
    // Na ordem de TITULOS_GRUPO: cada grupo aparece uma vez, mesmo com um item fora do bloco em NAVEGACAO.
    const itens = navegacaoDe(perfil)
    return (Object.keys(TITULOS_GRUPO) as GrupoNavegacao[])
        .map((grupo) => ({ grupo, titulo: TITULOS_GRUPO[grupo], itens: itens.filter((i) => i.grupo === grupo) }))
        .filter((g) => g.itens.length > 0)
}
