/** Rótulo de cada permissão interna (segurança §3.1), agrupado por área para o editor de papéis. */
export const AREAS_PERMISSAO: { area: string; permissoes: [codigo: string, rotulo: string][] }[] = [
    {
        area: 'Clientes',
        permissoes: [
            ['cliente.ver', 'Ver clientes'],
            ['cliente.gerenciar', 'Cadastrar, editar e inativar clientes e contatos'],
        ],
    },
    {
        area: 'Solicitações',
        permissoes: [
            ['solicitacao.ver', 'Ver a fila'],
            ['solicitacao.criar', 'Abrir solicitação'],
            ['solicitacao.editar', 'Mudar status, responsável, prioridade e categoria'],
            ['solicitacao.comentar', 'Responder ao cliente e deixar nota interna'],
        ],
    },
    {
        area: 'Tarefas e projetos',
        permissoes: [
            ['tarefa.ver', 'Ver tarefas'],
            ['tarefa.criar', 'Criar tarefa'],
            ['tarefa.editar', 'Editar e mover tarefas'],
            ['projeto.ver', 'Ver projetos'],
            ['projeto.gerenciar', 'Criar, editar e encerrar projetos'],
            ['recorrencia.ver', 'Ver recorrências'],
            ['recorrencia.gerenciar', 'Criar, editar, pausar e encerrar recorrências'],
        ],
    },
    {
        area: 'Horas',
        permissoes: [
            ['apontamento.ver', 'Ver as horas da equipe'],
            ['apontamento.criar', 'Apontar horas'],
            ['apontamento.editar_todos', 'Editar e excluir horas de qualquer pessoa'],
        ],
    },
    {
        area: 'Contratos',
        permissoes: [
            ['contrato.ver', 'Ver contratos e consumo'],
            ['contrato.gerenciar', 'Criar, editar, suspender e encerrar contratos'],
            ['contrato.ajustar_saldo', 'Ajustar o saldo de horas'],
            ['contrato.fechar_periodo', 'Fechar o mês'],
        ],
    },
    {
        area: 'Gestão',
        permissoes: [
            ['dashboard.ver', 'Ver o panorama'],
            ['portal.gerenciar_acessos', 'Convidar contatos e mudar o acesso deles ao portal'],
            ['configuracao.gerenciar', 'Mudar as configurações da empresa'],
            ['usuario.convidar', 'Convidar pessoas para a equipe'],
            ['usuario.remover', 'Remover pessoas da equipe'],
            ['papel.gerenciar', 'Criar papéis, trocar o papel e a capacidade das pessoas'],
            ['auditoria.ver', 'Ver a auditoria'],
        ],
    },
]

const ROTULOS = new Map(AREAS_PERMISSAO.flatMap((a) => a.permissoes))

/** As áreas com as permissões que o catálogo da API traz; o que o front ainda não conhece vai em "Outras". */
export function areasDoCatalogo(catalogo: readonly string[]) {
    const conhecidas = AREAS_PERMISSAO.map((a) => ({
        area: a.area,
        permissoes: a.permissoes.filter(([codigo]) => catalogo.includes(codigo)),
    })).filter((a) => a.permissoes.length > 0)
    const outras = catalogo.filter((c) => !ROTULOS.has(c)).map((c): [string, string] => [c, c])
    return outras.length ? [...conhecidas, { area: 'Outras', permissoes: outras }] : conhecidas
}

export function rotuloPermissao(codigo: string): string {
    return ROTULOS.get(codigo) ?? codigo
}
