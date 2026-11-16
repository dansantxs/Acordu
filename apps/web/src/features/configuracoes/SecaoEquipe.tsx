import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useId, useState } from 'react'
import { Button, Form } from 'react-aria-components'
import { pode } from '@/features/casca/navegacao'
import { SEM_ENVIO } from '@/features/sessao/AvisoConfirmacao'
import {
    consultaConvites,
    consultaPapeis,
    consultaPessoas,
    convidar,
    definirCapacidade,
    NOME_DONO,
    NOME_MEMBRO,
    removerPessoa,
    revogarConvite,
    trocarPapel,
    type Papel,
    type Pessoa,
} from '@/features/equipe/consultas'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { formatarDuracao, lerDuracao } from '@/shared/format/duracao'
import { dataHora } from '@/shared/format/prazo'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Icone } from '@/shared/ui/Icone'
import { Janela } from '@/shared/ui/Janela'
import { CabecalhoSecao } from './TelaConfiguracoes'

/** Capacidade semanal: 0 a 168h (contrato §3). */
const MAXIMO_CAPACIDADE = 10080

type Acao = { tipo: 'papel' | 'capacidade' | 'remocao'; pessoa: Pessoa } | null

/**
 * Equipe (F5c): pessoas com papel e capacidade semanal (base da agenda), remoção e convites pendentes. Cada ação aparece
 * pela permissão dela: trocar papel e capacidade com `papel.gerenciar`, remover com `usuario.remover`, convidar com
 * `usuario.convidar`.
 */
export function SecaoEquipe({ perfil }: { perfil: Perfil }) {
    const consultas = useQueryClient()
    const verPessoas = pode(perfil, 'usuario.convidar', 'usuario.remover')
    const podeConvidar = pode(perfil, 'usuario.convidar')
    const motivoConvite = useId()
    const gerenciaPapeis = pode(perfil, 'papel.gerenciar')
    const podeRemover = pode(perfil, 'usuario.remover')
    const pessoas = useQuery({ ...consultaPessoas, enabled: verPessoas })
    const convites = useQuery({ ...consultaConvites, enabled: podeConvidar })
    const papeis = useQuery({ ...consultaPapeis, enabled: podeConvidar || gerenciaPapeis })
    const internos = (papeis.data ?? []).filter((p) => p.tipo === 'INTERNO')
    const [acao, setAcao] = useState<Acao>(null)
    const [convidando, setConvidando] = useState(false)

    const remocao = useMutation({
        mutationFn: removerPessoa,
        onSuccess: async (_, p) => {
            await consultas.invalidateQueries({ queryKey: ['equipe'] })
            setAcao(null)
            avisar(`${p.nome} saiu da equipe.`)
        },
    })
    const revogacao = useMutation({
        mutationFn: revogarConvite,
        onSuccess: async (_, c) => {
            // Toda a equipe: convites mudam também o passo "equipe" dos primeiros passos.
            await consultas.invalidateQueries({ queryKey: ['equipe'] })
            avisar(`Convite de ${c.email} cancelado.`)
        },
        onError: avisarErro,
    })

    if (!verPessoas)
        return (
            <>
                <CabecalhoSecao titulo="Equipe" />
                <EstadoVazio titulo="A lista da equipe pede outra permissão">
                    Para ver as pessoas, o papel precisa de "Convidar pessoas" ou "Remover pessoas". Os papéis ficam na
                    seção Papéis.
                </EstadoVazio>
            </>
        )

    return (
        <>
            <div className="flex flex-wrap items-start justify-between gap-3">
                <CabecalhoSecao titulo="Equipe">
                    Quem trabalha na empresa, com o papel e as horas por semana que entram no planejamento.
                </CabecalhoSecao>
                {podeConvidar && (
                    <div className="flex flex-col items-end gap-1">
                        <Botao
                            grava
                            onPress={() => setConvidando(true)}
                            isDisabled={!perfil.tenant.envioLiberado}
                            aria-describedby={perfil.tenant.envioLiberado ? undefined : motivoConvite}
                        >
                            <Icone nome="mais" />
                            Convidar pessoa
                        </Botao>
                        {!perfil.tenant.envioLiberado && (
                            <p id={motivoConvite} className="text-xs text-suave">
                                {SEM_ENVIO}
                            </p>
                        )}
                    </div>
                )}
            </div>
            <div className="flex flex-col gap-8">
                {pessoas.isPending ? (
                    <p className="text-sm text-suave">Carregando…</p>
                ) : pessoas.isError ? (
                    <Alerta>{mensagemDeErro(pessoas.error)}</Alerta>
                ) : (
                    <ul
                        aria-label="Pessoas"
                        className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm"
                    >
                        {pessoas.data.map((p) => {
                            const eu = p.usuarioId === perfil.usuarioId
                            return (
                                <li key={p.usuarioId} className="flex flex-wrap items-center gap-x-6 gap-y-1 px-4 py-3">
                                    <span className="mr-auto flex min-w-48 flex-col">
                                        <span className="font-semibold">
                                            {p.nome}
                                            {eu && <span className="font-normal text-suave"> (você)</span>}
                                        </span>
                                        <span className="text-xs text-suave">{p.email}</span>
                                    </span>
                                    <span className="rounded-couro bg-trilho px-2 py-0.5 text-xs">{p.papel.nome}</span>
                                    <span className="w-24 text-xs text-suave tabular-nums">
                                        {formatarDuracao(Number(p.capacidadeSemanalMinutos))}/semana
                                    </span>
                                    <span className="flex gap-3">
                                        {gerenciaPapeis && (
                                            <>
                                                <Button
                                                    onPress={() => setAcao({ tipo: 'papel', pessoa: p })}
                                                    aria-label={`Trocar o papel de ${p.nome}`}
                                                    className="text-acento-texto hover:underline"
                                                >
                                                    Papel
                                                </Button>
                                                <Button
                                                    onPress={() => setAcao({ tipo: 'capacidade', pessoa: p })}
                                                    aria-label={`Capacidade de ${p.nome}`}
                                                    className="text-acento-texto hover:underline"
                                                >
                                                    Capacidade
                                                </Button>
                                            </>
                                        )}
                                        {podeRemover && !eu && (
                                            <Button
                                                onPress={() => setAcao({ tipo: 'remocao', pessoa: p })}
                                                aria-label={`Remover ${p.nome}`}
                                                className="text-suave hover:text-vermelho hover:underline"
                                            >
                                                Remover
                                            </Button>
                                        )}
                                    </span>
                                </li>
                            )
                        })}
                    </ul>
                )}

                {podeConvidar && (
                    <section aria-labelledby="convites-pendentes" className="flex flex-col gap-2">
                        <h3 id="convites-pendentes" className="text-sm font-semibold">
                            Convites pendentes
                        </h3>
                        {convites.isPending ? (
                            <p className="text-sm text-suave">Carregando…</p>
                        ) : convites.isError ? (
                            <Alerta>{mensagemDeErro(convites.error)}</Alerta>
                        ) : convites.data.length === 0 ? (
                            <p className="text-sm text-suave">Nenhum convite esperando resposta.</p>
                        ) : (
                            <ul className="divide-y divide-linha rounded-couro border border-dashed border-linha text-sm">
                                {convites.data.map((c) => (
                                    <li key={c.id} className="flex flex-wrap items-center gap-x-6 gap-y-1 px-4 py-2.5">
                                        <span className="mr-auto">{c.email}</span>
                                        <span className="rounded-couro bg-trilho px-2 py-0.5 text-xs">
                                            {c.papel.nome}
                                        </span>
                                        <span className="text-xs text-suave">
                                            {c.expiraEm
                                                ? `vale até ${dataHora(c.expiraEm, perfil.tenant.fusoHorario)}`
                                                : 'e-mail saindo'}
                                        </span>
                                        <Button
                                            onPress={() => revogacao.mutate(c)}
                                            aria-label={`Cancelar o convite de ${c.email}`}
                                            className="text-suave hover:underline"
                                        >
                                            Cancelar
                                        </Button>
                                    </li>
                                ))}
                            </ul>
                        )}
                    </section>
                )}
            </div>

            {convidando && (
                <JanelaConvite
                    // Dono pelo convite exige papel.gerenciar (decidido em 23/10).
                    papeis={gerenciaPapeis ? internos : internos.filter((p) => p.nome !== NOME_DONO)}
                    aoFechar={() => setConvidando(false)}
                />
            )}
            {acao?.tipo === 'papel' && (
                <JanelaPapel pessoa={acao.pessoa} papeis={internos} aoFechar={() => setAcao(null)} />
            )}
            {acao?.tipo === 'capacidade' && <JanelaCapacidade pessoa={acao.pessoa} aoFechar={() => setAcao(null)} />}
            <Confirmacao
                titulo={`Remover ${acao?.tipo === 'remocao' ? acao.pessoa.nome : ''} da equipe?`}
                acao="Remover da equipe"
                aberta={acao?.tipo === 'remocao'}
                aoMudar={(a) => {
                    if (!a) {
                        remocao.reset()
                        setAcao(null)
                    }
                }}
                aoConfirmar={() => acao && remocao.mutate(acao.pessoa)}
                ocupada={remocao.isPending}
                erro={remocao.isError ? mensagemDeErro(remocao.error) : null}
            >
                <p>O acesso a esta empresa acaba na hora. A conta e o que foi feito aqui continuam.</p>
                <p>
                    O que estiver sob a responsabilidade dessa pessoa continua com ela, marcado como "saiu da equipe":
                    troque o responsável onde for preciso.
                </p>
            </Confirmacao>
        </>
    )
}

function JanelaConvite({ papeis, aoFechar }: { papeis: Papel[]; aoFechar: () => void }) {
    const consultas = useQueryClient()
    const [papel, setPapel] = useState<string | null>(papeis.find((p) => p.nome === NOME_MEMBRO)?.id ?? null)
    const envio = useMutation({
        mutationFn: ({ email }: { email: string }) => convidar(email, papel!),
        onSuccess: async (c) => {
            // Toda a equipe: convites mudam também o passo "equipe" dos primeiros passos.
            await consultas.invalidateQueries({ queryKey: ['equipe'] })
            avisar(`Convite enviado para ${c.email}.`)
            aoFechar()
        },
    })
    return (
        <Janela titulo="Convidar pessoa" aberta aoMudar={(a) => !a && aoFechar()} ocupada={envio.isPending}>
            <Form
                className="flex flex-col gap-4"
                validationErrors={errosDeCampo(envio.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    if (!papel) return
                    envio.mutate({ email: String(new FormData(evento.currentTarget).get('email')).trim() })
                }}
            >
                <p className="text-sm text-suave">
                    A pessoa recebe um link que vale por 7 dias para entrar na equipe, com conta nova ou a que já tem.
                </p>
                {envio.isError && mensagemDeErro(envio.error) && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
                <CampoTexto name="email" rotulo="E-mail" type="email" isRequired autoFocus />
                <CampoSelecao
                    name="papelId"
                    rotulo="Papel"
                    opcoes={papeis.map((p) => ({ id: p.id, rotulo: p.nome }))}
                    valor={papel}
                    aoMudar={setPapel}
                    isRequired
                />
                <div className="flex justify-end gap-2">
                    <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao grava type="submit" isPending={envio.isPending} isDisabled={!papel}>
                        Enviar convite
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}

function JanelaPapel({ pessoa, papeis, aoFechar }: { pessoa: Pessoa; papeis: Papel[]; aoFechar: () => void }) {
    const consultas = useQueryClient()
    const [papel, setPapel] = useState<string | null>(pessoa.papel.id)
    const envio = useMutation({
        mutationFn: () => trocarPapel(pessoa, papel!),
        onSuccess: async (p) => {
            await consultas.invalidateQueries({ queryKey: ['equipe'] })
            avisar(`${p.nome} agora é ${p.papel.nome}. Vale a partir do próximo acesso.`)
            aoFechar()
        },
    })
    return (
        <Janela titulo={`Papel de ${pessoa.nome}`} aberta aoMudar={(a) => !a && aoFechar()} ocupada={envio.isPending}>
            <Form
                className="flex flex-col gap-4"
                onSubmit={(evento) => {
                    evento.preventDefault()
                    if (papel && papel !== pessoa.papel.id) envio.mutate()
                    else aoFechar()
                }}
            >
                {envio.isError && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
                <CampoSelecao
                    rotulo="Papel"
                    opcoes={papeis.map((p) => ({
                        id: p.id,
                        rotulo: p.nome,
                        detalhe: `${p.permissoes.length} permissões`,
                    }))}
                    valor={papel}
                    aoMudar={setPapel}
                    isRequired
                />
                <p className="text-xs text-suave">A empresa sempre precisa de ao menos uma pessoa com o papel Dono.</p>
                <div className="flex justify-end gap-2">
                    <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao grava type="submit" isPending={envio.isPending}>
                        Salvar
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}

function JanelaCapacidade({ pessoa, aoFechar }: { pessoa: Pessoa; aoFechar: () => void }) {
    const consultas = useQueryClient()
    const [texto, setTexto] = useState(formatarDuracao(Number(pessoa.capacidadeSemanalMinutos)))
    const minutos = /^\s*0+\s*(h|min|m)?\s*$/i.test(texto) ? 0 : lerDuracao(texto)
    const invalida = minutos === null || minutos > MAXIMO_CAPACIDADE
    const envio = useMutation({
        mutationFn: (m: number) => definirCapacidade(pessoa, m),
        onSuccess: async (p) => {
            await consultas.invalidateQueries({ queryKey: ['equipe'] })
            avisar(`${p.nome}: ${formatarDuracao(Number(p.capacidadeSemanalMinutos))} por semana.`)
            aoFechar()
        },
    })
    return (
        <Janela
            titulo={`Capacidade de ${pessoa.nome}`}
            aberta
            aoMudar={(a) => !a && aoFechar()}
            ocupada={envio.isPending}
        >
            <Form
                className="flex flex-col gap-4"
                onSubmit={(evento) => {
                    evento.preventDefault()
                    if (!invalida) envio.mutate(minutos)
                }}
            >
                {envio.isError && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
                <div className="flex flex-col gap-1">
                    <label htmlFor="capacidade" className="text-sm font-semibold">
                        Horas por semana
                    </label>
                    <input
                        id="capacidade"
                        value={texto}
                        onChange={(e) => setTexto(e.target.value)}
                        aria-invalid={invalida || undefined}
                        aria-describedby="capacidade-d"
                        className="w-28 rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
                    />
                    <p id="capacidade-d" className={`text-xs ${invalida ? 'text-vermelho' : 'text-suave'}`}>
                        {invalida
                            ? 'De 0 a 168h. Use 40h, 30h ou 20h30.'
                            : // Número sozinho é minuto (convenção do campo de duração): mostrar o que vai ficar evita "40" virar 40min.
                              `Fica ${formatarDuracao(minutos)} por semana, comparadas com as tarefas planejadas na agenda. 0 tira a pessoa da conta de carga.`}
                    </p>
                </div>
                <div className="flex justify-end gap-2">
                    <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao grava type="submit" isPending={envio.isPending}>
                        Salvar
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}
