import { STATUS, TIPO_CONTRATO, type Status, type TipoContrato } from '@/features/solicitacoes/rotulos'
import type { components } from '@/shared/api/interno'
import { formatarDuracao } from '@/shared/format/duracao'

export type Registro = components['schemas']['RegistroAuditoriaDto']

/** As ações da trilha (contrato §11a) por área, com o rótulo do filtro. */
export const AREAS_ACAO: { area: string; acoes: [codigo: string, rotulo: string][] }[] = [
    {
        area: 'Sessão',
        acoes: [
            ['login.sucesso', 'Entrada no Acordu'],
            ['portal.link.troca', 'Entrada no portal por link'],
        ],
    },
    {
        area: 'Equipe',
        acoes: [
            ['equipe.convite', 'Convite para a equipe'],
            ['equipe.convite.revogacao', 'Convite cancelado'],
            ['equipe.papel.troca', 'Troca de papel'],
            ['equipe.capacidade', 'Capacidade alterada'],
            ['equipe.remocao', 'Remoção da equipe'],
            ['agenda.link.emissao', 'Link da agenda criado'],
            ['agenda.link.revogacao', 'Link da agenda revogado'],
        ],
    },
    {
        area: 'Portal',
        acoes: [
            ['portal.acesso.concessao', 'Convite de contato ao portal'],
            ['portal.acesso.escopo', 'Acesso do contato alterado'],
            ['portal.acesso.revogacao', 'Acesso do contato revogado'],
            ['portal.link.emissao', 'Link por solicitação enviado'],
            ['portal.link.revogacao', 'Link por solicitação revogado'],
        ],
    },
    {
        area: 'Contratos',
        acoes: [
            ['contrato.criacao', 'Contrato criado'],
            ['contrato.edicao', 'Contrato editado'],
            ['contrato.suspensao', 'Contrato suspenso'],
            ['contrato.reativacao', 'Contrato reativado'],
            ['contrato.encerramento', 'Contrato encerrado'],
            ['contrato.ajuste_saldo', 'Ajuste de saldo'],
            ['contrato.fechamento_periodo', 'Mês fechado'],
        ],
    },
    {
        area: 'Horas',
        acoes: [
            ['apontamento.edicao', 'Apontamento editado'],
            ['apontamento.exclusao', 'Apontamento excluído'],
        ],
    },
    {
        area: 'Solicitações',
        acoes: [
            ['solicitacao.status', 'Status da solicitação'],
            ['solicitacao.responsavel', 'Responsável da solicitação'],
            ['solicitacao.contrato', 'Contrato da solicitação'],
        ],
    },
    {
        area: 'Empresa',
        acoes: [
            ['exemplos.carga', 'Dados de exemplo carregados'],
            ['exemplos.remocao', 'Dados de exemplo apagados'],
        ],
    },
]

const ROTULOS = new Map(AREAS_ACAO.flatMap((a) => a.acoes))
export const rotuloAcao = (codigo: string) => ROTULOS.get(codigo) ?? codigo

type Dados = Record<string, unknown>
const texto = (v: unknown) => (typeof v === 'string' ? v : '')
const numero = (v: unknown) => (typeof v === 'number' ? v : Number(v ?? 0))
const status = (v: unknown) => STATUS[v as Status]?.toLowerCase() ?? texto(v).toLowerCase()
const escopo = (v: unknown) =>
    v === 'PROPRIAS' ? 'só as próprias' : v === 'TODAS_DO_CLIENTE' ? 'todas do cliente' : ''
const dataBr = (v: unknown) => texto(v).split('-').reverse().join('/')

/**
 * O que aconteceu, numa frase sem o autor (decisão 6 do F5): "trocou o papel de Membro para Leitor". Ação que o front
 * não conhece cai no rótulo do código; os detalhes ficam no chave/valor.
 */
export function fraseAuditoria(r: Registro, nomes: ReadonlyMap<string, string>): string {
    const d = (r.dados ?? {}) as Dados
    const pessoa = (id: unknown) =>
        typeof id === 'string' ? (nomes.get(id) ?? 'pessoa que saiu da equipe') : 'ninguém'
    switch (r.acao) {
        case 'login.sucesso':
            return 'entrou no Acordu'
        case 'portal.link.troca':
            return 'abriu o portal por um link de solicitação'
        case 'equipe.convite':
            return `convidou ${texto(d.email)} como ${texto(d.papel)}`
        case 'equipe.convite.revogacao':
            return `cancelou o convite de ${texto(d.email)}`
        case 'equipe.papel.troca':
            return `trocou o papel de ${pessoa(r.alvoId)} de ${texto(d.de) || '—'} para ${texto(d.para)}`
        case 'equipe.capacidade':
            return `mudou a capacidade de ${pessoa(r.alvoId)} de ${formatarDuracao(numero(d.de))} para ${formatarDuracao(numero(d.para))} por semana`
        case 'equipe.remocao':
            return `removeu ${pessoa(r.alvoId)} da equipe${d.papel ? ` (era ${texto(d.papel)})` : ''}`
        case 'agenda.link.emissao':
            return `criou o link da agenda${d.discreta ? ' em modo discreto' : ''}`
        case 'agenda.link.revogacao':
            return 'revogou o link da agenda'
        case 'portal.acesso.concessao':
            return `convidou um contato para o portal (${escopo(d.escopo)})`
        case 'portal.acesso.escopo':
            return `mudou o acesso de um contato para ${escopo(d.escopo)}`
        case 'portal.acesso.revogacao':
            return 'revogou o acesso de um contato ao portal'
        case 'portal.link.emissao':
            return `enviou um link da solicitação válido por ${numero(d.validadeDias)} dias`
        case 'portal.link.revogacao':
            return 'revogou um link da solicitação'
        case 'contrato.criacao':
            return `criou um contrato ${(TIPO_CONTRATO[d.tipo as TipoContrato] ?? '').toLowerCase()}`.trim()
        case 'contrato.edicao':
            return 'editou os termos do contrato'
        case 'contrato.suspensao':
            return 'suspendeu o contrato'
        case 'contrato.reativacao':
            return 'reativou o contrato'
        case 'contrato.encerramento':
            return `encerrou o contrato${d.fim ? ` em ${dataBr(d.fim)}` : ''}`
        case 'contrato.ajuste_saldo': {
            const minutos = numero(d.quantidadeMinutos)
            return `${minutos < 0 ? 'descontou' : 'adicionou'} ${formatarDuracao(Math.abs(minutos))} no saldo: "${texto(d.motivo)}"`
        }
        case 'contrato.fechamento_periodo':
            return `fechou o mês ${texto(d.periodo).split('-').reverse().join('/')}`
        case 'apontamento.edicao':
            return 'editou um apontamento'
        case 'apontamento.exclusao': {
            const antes = (d.antes ?? {}) as Dados
            return `excluiu um apontamento de ${formatarDuracao(numero(antes.duracaoMinutos))} em ${dataBr(antes.data)}`
        }
        case 'solicitacao.status':
            return `mudou a solicitação de ${status(d.de)} para ${status(d.para)}`
        case 'solicitacao.responsavel':
            return d.para ? `passou a solicitação para ${pessoa(d.para)}` : 'tirou o responsável da solicitação'
        case 'solicitacao.contrato':
            return d.para ? 'trocou o contrato da solicitação' : 'tirou a solicitação do contrato'
        case 'exemplos.carga':
            return `carregou os dados de exemplo (${numero(d.clientes)} clientes)`
        case 'exemplos.remocao':
            return `apagou os dados de exemplo (${numero(d.clientes)} clientes)`
        default:
            return rotuloAcao(r.acao)
    }
}

/** Campos que mudaram entre `antes` e `depois` (edição de contrato e de apontamento). */
export function diferencas(dados: unknown): { campo: string; antes: string; depois: string }[] {
    const d = (dados ?? {}) as Dados
    if (typeof d.antes !== 'object' || typeof d.depois !== 'object' || !d.antes || !d.depois) return []
    const antes = d.antes as Dados
    const depois = d.depois as Dados
    return [...new Set([...Object.keys(antes), ...Object.keys(depois)])]
        .filter((k) => JSON.stringify(antes[k]) !== JSON.stringify(depois[k]))
        .map((campo) => ({ campo, antes: valor(antes[campo]), depois: valor(depois[campo]) }))
}

export function valor(v: unknown): string {
    if (v === null || v === undefined || v === '') return '—'
    if (typeof v === 'boolean') return v ? 'sim' : 'não'
    if (typeof v === 'object') return JSON.stringify(v)
    return String(v)
}
