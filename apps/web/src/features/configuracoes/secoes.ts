import { pode } from '@/features/casca/navegacao'
import type { Perfil } from '@/shared/sessao/perfil'

export type CaminhoSecao =
    | '/configuracoes/empresa'
    | '/configuracoes/calendario'
    | '/configuracoes/portal'
    | '/configuracoes/categorias'
    | '/configuracoes/equipe'
    | '/configuracoes/papeis'
    | '/configuracoes/auditoria'

export interface Secao {
    rotulo: string
    para: CaminhoSecao
    /** Qualquer uma basta. */
    permissoes: string[]
}

/** Seções de Configurações (decisão 2 do F5): uma rota cada, só aparecem com a permissão. */
export const SECOES: Secao[] = [
    { rotulo: 'Empresa', para: '/configuracoes/empresa', permissoes: ['configuracao.gerenciar'] },
    { rotulo: 'Calendário', para: '/configuracoes/calendario', permissoes: ['configuracao.gerenciar'] },
    { rotulo: 'Portal', para: '/configuracoes/portal', permissoes: ['configuracao.gerenciar'] },
    { rotulo: 'Categorias', para: '/configuracoes/categorias', permissoes: ['configuracao.gerenciar'] },
    {
        rotulo: 'Equipe',
        para: '/configuracoes/equipe',
        permissoes: ['usuario.convidar', 'usuario.remover', 'papel.gerenciar'],
    },
    { rotulo: 'Papéis', para: '/configuracoes/papeis', permissoes: ['papel.gerenciar'] },
    { rotulo: 'Auditoria', para: '/configuracoes/auditoria', permissoes: ['auditoria.ver'] },
]

export function secoesDe(perfil: Perfil): Secao[] {
    return SECOES.filter((s) => pode(perfil, ...s.permissoes))
}

export function podeVer(perfil: Perfil, para: CaminhoSecao): boolean {
    return secoesDe(perfil).some((s) => s.para === para)
}
