import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { pode } from '@/features/casca/navegacao'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { dataHora } from '@/shared/format/prazo'
import {
    desligarNotificacoes,
    estadoNotificacoes,
    ligarNotificacoes,
    type EstadoNotificacoes,
} from '@/shared/notificacoes/navegador'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { Marcador } from '@/shared/ui/Marcador'
import { Pagina } from '@/shared/ui/Pagina'
import {
    consultaAssinatura,
    consultaPreferencias,
    gerarLinkAgenda,
    mudarModoAgenda,
    revogarLinkAgenda,
    salvarPreferencias,
    type CategoriaAviso,
    type Preferencias,
} from './consultas'

const CATEGORIAS: Record<CategoriaAviso, { rotulo: string; descricao: string }> = {
    NOVA_SOLICITACAO: {
        rotulo: 'Nova solicitação',
        descricao: 'Quando chega uma solicitação para você ou sem responsável.',
    },
    ATRIBUICAO: { rotulo: 'Passaram algo para você', descricao: 'Quando alguém faz de você o responsável.' },
    SLA: { rotulo: 'Prazo de SLA', descricao: 'Quando uma solicitação sua entra em risco ou estoura o prazo.' },
    CONSUMO: { rotulo: 'Consumo de contrato', descricao: 'Quando um contrato passa de 80% e de 100% das horas.' },
    MENCAO: { rotulo: 'Menções', descricao: 'Quando alguém cita você numa nota ou comentário.' },
    SEGUIDAS: { rotulo: 'O que você segue', descricao: 'Novas mensagens e mudanças de status no que você acompanha.' },
}

/** Permissões que dão acesso à agenda e, por isso, ao link ICS (contrato §11b e §11d). */
const LEITURA_AGENDA = ['solicitacao.ver', 'tarefa.ver', 'projeto.ver', 'recorrencia.ver', 'contrato.ver']

/** Minha conta (decisão 7 do F5): o que é de cada pessoa, não da empresa: e-mails que recebe e a agenda no calendário. */
export function TelaConta({ perfil }: { perfil: Perfil }) {
    return (
        <Pagina ajuda="conta" titulo="Minha conta" contexto={`${perfil.nome} · ${perfil.email}`}>
            <div className="flex max-w-2xl flex-col gap-10">
                <Emails />
                <NotificacoesNavegador />
                {pode(perfil, ...LEITURA_AGENDA) && <Agenda fuso={perfil.tenant.fusoHorario} />}
            </div>
        </Pagina>
    )
}

/**
 * Notificação do sistema pelo navegador (F12-20, F12-21, F12-25): por navegador, com o Acordu aberto. Sem suporte, a
 * seção não aparece.
 */
function NotificacoesNavegador() {
    const [estado, setEstado] = useState<EstadoNotificacoes>(estadoNotificacoes)
    const [pedindo, setPedindo] = useState(false)
    if (estado === 'sem-suporte') return null
    return (
        <section aria-labelledby="conta-navegador" className="flex flex-col gap-3">
            <h2 id="conta-navegador" className="font-titulo text-2xl font-semibold tracking-tight">
                Neste navegador
            </h2>
            <p className="text-sm text-suave">
                Os avisos do sino aparecem também como notificação do sistema, enquanto o Acordu estiver aberto, mesmo
                minimizado. Vale só para este navegador.
            </p>
            <Marcador
                valor={estado === 'ligada'}
                isDisabled={estado === 'negada' || pedindo}
                aoMudar={async (ligar) => {
                    if (!ligar) {
                        desligarNotificacoes()
                        setEstado('desligada')
                        return
                    }
                    setPedindo(true)
                    try {
                        setEstado(await ligarNotificacoes())
                    } finally {
                        setPedindo(false)
                    }
                }}
                {...(estado === 'negada' ? { descritoPor: 'conta-navegador-negada' } : {})}
            >
                Notificações neste navegador
            </Marcador>
            {estado === 'negada' && (
                <p id="conta-navegador-negada" className="text-xs text-suave">
                    O navegador bloqueou as notificações do Acordu. Para liberar, clique no cadeado ao lado do endereço,
                    em Notificações escolha Permitir, e volte aqui.
                </p>
            )}
        </section>
    )
}

function Emails() {
    const preferencias = useQuery(consultaPreferencias)
    return (
        <section aria-labelledby="conta-emails" className="flex flex-col gap-3">
            <h2 id="conta-emails" className="font-titulo text-2xl font-semibold tracking-tight">
                E-mails
            </h2>
            <p className="text-sm text-suave">
                O sino do Acordu mostra tudo; aqui você escolhe o que também chega por e-mail. Vale só para você e só
                nesta empresa. E-mails da conta (senha, confirmação) sempre chegam.
            </p>
            {preferencias.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : preferencias.isError ? (
                <Alerta>{mensagemDeErro(preferencias.error)}</Alerta>
            ) : (
                <FormularioEmails inicial={preferencias.data} />
            )}
        </section>
    )
}

function FormularioEmails({ inicial }: { inicial: Preferencias }) {
    const consultas = useQueryClient()
    const [ligadas, setLigadas] = useState(
        () => new Set(inicial.categorias.filter((c) => c.email).map((c) => c.categoria)),
    )
    const envio = useMutation({
        mutationFn: salvarPreferencias,
        onSuccess: (salvas) => {
            consultas.setQueryData(consultaPreferencias.queryKey, salvas)
            avisar('Preferências de e-mail salvas.')
        },
        onError: avisarErro,
    })
    return (
        <div className="flex flex-col gap-4">
            <ul className="flex flex-col gap-3">
                {inicial.categorias.map(({ categoria }) => (
                    <li key={categoria} className="flex flex-col gap-0.5">
                        <Marcador
                            valor={ligadas.has(categoria)}
                            aoMudar={(v) =>
                                setLigadas((atual) => {
                                    const nova = new Set(atual)
                                    if (v) nova.add(categoria)
                                    else nova.delete(categoria)
                                    return nova
                                })
                            }
                        >
                            {CATEGORIAS[categoria]?.rotulo ?? categoria}
                        </Marcador>
                        {CATEGORIAS[categoria] && (
                            <span className="pl-6 text-xs text-suave">{CATEGORIAS[categoria].descricao}</span>
                        )}
                    </li>
                ))}
            </ul>
            <div>
                <Botao
                    isPending={envio.isPending}
                    onPress={() =>
                        envio.mutate({
                            categorias: inicial.categorias.map(({ categoria }) => ({
                                categoria,
                                email: ligadas.has(categoria),
                            })),
                        })
                    }
                >
                    Salvar
                </Botao>
            </div>
        </div>
    )
}

function Agenda({ fuso }: { fuso: string }) {
    const consultas = useQueryClient()
    const assinatura = useQuery(consultaAssinatura)
    const [link, setLink] = useState<string | null>(null)
    const [copiado, setCopiado] = useState(false)
    const [confirmando, setConfirmando] = useState<'novo' | 'revogar' | null>(null)
    const atualizar = () => consultas.invalidateQueries({ queryKey: consultaAssinatura.queryKey })
    const geracao = useMutation({
        mutationFn: gerarLinkAgenda,
        onSuccess: async (gerado) => {
            setLink(gerado.url)
            setCopiado(false)
            setConfirmando(null)
            await atualizar()
        },
        onError: avisarErro,
    })
    const modo = useMutation({
        mutationFn: mudarModoAgenda,
        onSuccess: async (a) => {
            await atualizar()
            avisar(a.discreta ? 'Agenda em modo discreto.' : 'Agenda com os títulos completos.')
        },
        onError: avisarErro,
    })
    const revogacao = useMutation({
        mutationFn: revogarLinkAgenda,
        onSuccess: async () => {
            setLink(null)
            setConfirmando(null)
            await atualizar()
            avisar('Link da agenda revogado.')
        },
        onError: avisarErro,
    })

    return (
        <section aria-labelledby="conta-agenda" className="flex flex-col gap-3">
            <h2 id="conta-agenda" className="font-titulo text-2xl font-semibold tracking-tight">
                Agenda no seu calendário
            </h2>
            <p className="text-sm text-suave">
                Um link para assinar no Google Agenda, Outlook ou Apple Calendário. Traz as suas tarefas, prazos de
                solicitação e próximas recorrências, de 30 dias atrás a 90 à frente. Cada calendário busca as novidades
                no seu ritmo: o Google, por exemplo, pode levar algumas horas.
            </p>
            {assinatura.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : assinatura.isError ? (
                <Alerta>{mensagemDeErro(assinatura.error)}</Alerta>
            ) : !assinatura.data.ativa ? (
                <div className="flex flex-col gap-3">
                    <div className="flex flex-wrap gap-2">
                        <Botao
                            isPending={geracao.isPending && !geracao.variables}
                            onPress={() => geracao.mutate(false)}
                        >
                            Gerar link
                        </Botao>
                        <Botao
                            variante="secundario"
                            isPending={geracao.isPending && geracao.variables}
                            onPress={() => geracao.mutate(true)}
                        >
                            Gerar em modo discreto
                        </Botao>
                    </div>
                    <p className="text-xs text-suave">
                        Discreto: os eventos aparecem só como "Tarefa" ou "Prazo de solicitação", sem título nem
                        cliente. Bom para um calendário que outras pessoas veem.
                    </p>
                </div>
            ) : (
                <div className="flex flex-col gap-4">
                    <p className="text-sm">
                        Link ativo
                        {assinatura.data.criadaEm && <> desde {dataHora(assinatura.data.criadaEm, fuso)}</>}
                        {assinatura.data.discreta ? ', em modo discreto.' : ', com os títulos completos.'}
                    </p>
                    {link && (
                        <div className="flex flex-col gap-1">
                            <label htmlFor="link-agenda" className="text-sm font-semibold">
                                Seu link
                            </label>
                            <div className="flex gap-2">
                                <input
                                    id="link-agenda"
                                    readOnly
                                    value={link}
                                    onFocus={(e) => e.currentTarget.select()}
                                    className="min-w-0 flex-1 rounded-couro border border-linha bg-trilho px-3 py-2 text-sm"
                                />
                                <Botao
                                    variante="secundario"
                                    onPress={() =>
                                        void navigator.clipboard
                                            ?.writeText(link)
                                            .then(() => setCopiado(true))
                                            .catch(() => undefined)
                                    }
                                >
                                    {copiado ? 'Copiado' : 'Copiar'}
                                </Botao>
                            </div>
                            <p className="text-xs text-suave">
                                Ele só aparece agora. Cole no calendário em "Adicionar por URL" ou "Assinar calendário".
                                Quem tiver o link vê a sua agenda: não compartilhe.
                            </p>
                        </div>
                    )}
                    <Marcador
                        valor={assinatura.data.discreta}
                        isDisabled={modo.isPending}
                        aoMudar={(v) => modo.mutate(v)}
                    >
                        Modo discreto (sem títulos nem clientes)
                    </Marcador>
                    <div className="flex flex-wrap gap-2">
                        <Botao variante="secundario" onPress={() => setConfirmando('novo')}>
                            Gerar novo link
                        </Botao>
                        <Botao variante="discreto" onPress={() => setConfirmando('revogar')}>
                            Revogar
                        </Botao>
                    </div>
                </div>
            )}
            <Confirmacao
                titulo={confirmando === 'novo' ? 'Gerar um link novo?' : 'Revogar o link da agenda?'}
                acao={confirmando === 'novo' ? 'Gerar novo link' : 'Revogar'}
                aberta={confirmando !== null}
                aoMudar={(a) => !a && setConfirmando(null)}
                aoConfirmar={() =>
                    confirmando === 'novo' ? geracao.mutate(assinatura.data?.discreta ?? false) : revogacao.mutate()
                }
                ocupada={geracao.isPending || revogacao.isPending}
            >
                <p>
                    {confirmando === 'novo'
                        ? 'O link atual para de funcionar: o calendário onde ele está assinado deixa de atualizar até você colar o novo.'
                        : 'O calendário onde ele está assinado deixa de atualizar. Dá para gerar outro depois.'}
                </p>
            </Confirmacao>
        </section>
    )
}
