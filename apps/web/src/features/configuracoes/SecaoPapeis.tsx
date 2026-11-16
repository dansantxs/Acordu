import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button, Form } from 'react-aria-components'
import {
    consultaCatalogo,
    consultaPapeis,
    criarPapel,
    editarPapel,
    excluirPapel,
    type Papel,
} from '@/features/equipe/consultas'
import { areasDoCatalogo, rotuloPermissao } from '@/features/equipe/permissoes'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar } from '@/shared/avisos/avisos'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Confirmacao } from '@/shared/ui/Confirmacao'
import { Icone } from '@/shared/ui/Icone'
import { Janela } from '@/shared/ui/Janela'
import { Marcador } from '@/shared/ui/Marcador'
import { CabecalhoSecao } from './TelaConfiguracoes'

type Aberto = { modo: 'novo' } | { modo: 'editar' | 'ver'; papel: Papel } | null

/**
 * Papéis (F5c, contrato §3): os padrões (Dono, Membro, Leitor, Cliente) só para consulta e os sob medida com o editor
 * de permissões por área. Papel em uso não sai: troque o papel das pessoas antes.
 */
export function SecaoPapeis() {
    const consultas = useQueryClient()
    const papeis = useQuery(consultaPapeis)
    const [aberto, setAberto] = useState<Aberto>(null)
    const [excluindo, setExcluindo] = useState<Papel | null>(null)
    const exclusao = useMutation({
        mutationFn: excluirPapel,
        onSuccess: async (_, p) => {
            await consultas.invalidateQueries({ queryKey: consultaPapeis.queryKey })
            setExcluindo(null)
            avisar(`Papel "${p.nome}" excluído.`)
        },
    })

    return (
        <>
            <div className="flex flex-wrap items-start justify-between gap-3">
                <CabecalhoSecao titulo="Papéis">
                    O que cada pessoa pode fazer. Os papéis padrão não mudam; para outro recorte, crie um sob medida. A
                    mudança vale no próximo acesso de quem tem o papel.
                </CabecalhoSecao>
                <Botao grava onPress={() => setAberto({ modo: 'novo' })}>
                    <Icone nome="mais" />
                    Novo papel
                </Botao>
            </div>
            {papeis.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : papeis.isError ? (
                <Alerta>{mensagemDeErro(papeis.error)}</Alerta>
            ) : (
                <ul
                    aria-label="Papéis"
                    className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm"
                >
                    {papeis.data.map((p) => {
                        const pessoas = Number(p.pessoas)
                        return (
                            <li key={p.id} className="flex flex-wrap items-center gap-x-6 gap-y-1 px-4 py-3">
                                <span className="mr-auto flex min-w-48 flex-col">
                                    <span className="font-semibold">{p.nome}</span>
                                    <span className="text-xs text-suave">
                                        {p.padrao ? 'Padrão' : 'Sob medida'}
                                        {p.tipo === 'CLIENTE' && ' · contatos no portal'} · {p.permissoes.length}{' '}
                                        permissões
                                    </span>
                                </span>
                                <span className="text-xs text-suave">
                                    {pessoas === 0 ? 'ninguém' : pessoas === 1 ? '1 pessoa' : `${pessoas} pessoas`}
                                </span>
                                <span className="flex gap-3">
                                    {p.padrao ? (
                                        <Button
                                            onPress={() => setAberto({ modo: 'ver', papel: p })}
                                            aria-label={`Ver as permissões de ${p.nome}`}
                                            className="text-acento-texto hover:underline"
                                        >
                                            Ver
                                        </Button>
                                    ) : (
                                        <>
                                            <Button
                                                onPress={() => setAberto({ modo: 'editar', papel: p })}
                                                aria-label={`Editar ${p.nome}`}
                                                className="text-acento-texto hover:underline"
                                            >
                                                Editar
                                            </Button>
                                            <Button
                                                onPress={() => setExcluindo(p)}
                                                aria-label={`Excluir ${p.nome}`}
                                                className="text-suave hover:text-vermelho hover:underline"
                                            >
                                                Excluir
                                            </Button>
                                        </>
                                    )}
                                </span>
                            </li>
                        )
                    })}
                </ul>
            )}
            {aberto && (
                <JanelaPapel
                    aberto={aberto}
                    papeis={(papeis.data ?? []).filter((p) => p.tipo === 'INTERNO')}
                    aoFechar={() => setAberto(null)}
                />
            )}
            <Confirmacao
                titulo={`Excluir o papel "${excluindo?.nome ?? ''}"?`}
                acao="Excluir papel"
                aberta={excluindo !== null}
                aoMudar={(a) => {
                    if (!a) {
                        exclusao.reset()
                        setExcluindo(null)
                    }
                }}
                aoConfirmar={() => excluindo && exclusao.mutate(excluindo)}
                ocupada={exclusao.isPending}
                erro={
                    exclusao.error instanceof ErroApi && exclusao.error.status === 422
                        ? 'Ainda há pessoas com este papel. Troque o papel delas na seção Equipe e tente de novo.'
                        : exclusao.isError
                          ? mensagemDeErro(exclusao.error)
                          : null
                }
            >
                <p>Os convites pendentes com este papel são cancelados.</p>
            </Confirmacao>
        </>
    )
}

function JanelaPapel({
    aberto,
    papeis,
    aoFechar,
}: {
    aberto: NonNullable<Aberto>
    papeis: Papel[]
    aoFechar: () => void
}) {
    const consultas = useQueryClient()
    const catalogo = useQuery(consultaCatalogo)
    const papel = aberto.modo === 'novo' ? null : aberto.papel
    const somenteLeitura = aberto.modo === 'ver'
    const [marcadas, setMarcadas] = useState<string[]>(papel?.permissoes ?? [])
    const [vazio, setVazio] = useState(false)
    const [base, setBase] = useState<string | null>(null)
    const envio = useMutation({
        mutationFn: (nome: string) => (papel ? editarPapel(papel, nome, marcadas) : criarPapel(nome, marcadas)),
        onSuccess: async (salvo) => {
            await consultas.invalidateQueries({ queryKey: consultaPapeis.queryKey })
            avisar(papel ? `Papel "${salvo.nome}" salvo.` : `Papel "${salvo.nome}" criado.`)
            aoFechar()
        },
    })
    const marcar = (codigos: string[], ligar: boolean) =>
        setMarcadas((atual) =>
            ligar ? [...new Set([...atual, ...codigos])] : atual.filter((c) => !codigos.includes(c)),
        )
    const titulo = aberto.modo === 'novo' ? 'Novo papel' : somenteLeitura ? papel!.nome : `Editar "${papel!.nome}"`
    const erroPermissoes = envio.error instanceof ErroApi ? envio.error.doCampo('permissoes') : undefined

    return (
        <Janela titulo={titulo} aberta aoMudar={(a) => !a && aoFechar()} ocupada={envio.isPending} larga>
            {somenteLeitura ? (
                <div className="flex flex-col gap-4">
                    <p className="text-sm text-suave">Papel padrão: as permissões não mudam.</p>
                    <ul className="list-inside list-disc text-sm">
                        {papel!.permissoes.map((c) => (
                            <li key={c}>{rotuloPermissao(c)}</li>
                        ))}
                    </ul>
                    <div className="flex justify-end">
                        <Botao variante="secundario" onPress={aoFechar}>
                            Fechar
                        </Botao>
                    </div>
                </div>
            ) : (
                <Form
                    className="flex flex-col gap-5"
                    validationErrors={errosDeCampo(envio.error)}
                    onSubmit={(evento) => {
                        evento.preventDefault()
                        setVazio(marcadas.length === 0)
                        if (marcadas.length === 0) return
                        envio.mutate(String(new FormData(evento.currentTarget).get('nome')).trim())
                    }}
                >
                    {envio.isError && mensagemDeErro(envio.error) && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
                    <div className="grid gap-4 sm:grid-cols-2">
                        <CampoTexto
                            name="nome"
                            rotulo="Nome"
                            defaultValue={papel?.nome ?? ''}
                            maxLength={100}
                            isRequired
                            autoFocus
                        />
                        {!papel && (
                            <CampoSelecao
                                rotulo="Começar com as permissões de"
                                opcoes={papeis.map((p) => ({ id: p.id, rotulo: p.nome }))}
                                valor={base}
                                aoMudar={(id) => {
                                    setBase(id)
                                    setMarcadas(papeis.find((p) => p.id === id)?.permissoes ?? [])
                                }}
                                vazio="Nenhum (em branco)"
                            />
                        )}
                    </div>
                    {(vazio || erroPermissoes) && (
                        <p className="text-sm text-vermelho">{erroPermissoes ?? 'Marque ao menos uma permissão.'}</p>
                    )}
                    {catalogo.isPending ? (
                        <p className="text-sm text-suave">Carregando…</p>
                    ) : catalogo.isError ? (
                        <Alerta>{mensagemDeErro(catalogo.error)}</Alerta>
                    ) : (
                        <div className="grid gap-5 sm:grid-cols-2">
                            {areasDoCatalogo(catalogo.data).map(({ area, permissoes }) => {
                                const codigos = permissoes.map(([c]) => c)
                                const todas = codigos.every((c) => marcadas.includes(c))
                                return (
                                    <fieldset key={area} className="flex flex-col gap-2">
                                        <legend className="mb-1 flex w-full items-baseline justify-between gap-2 text-sm font-semibold">
                                            {area}
                                            <Button
                                                onPress={() => marcar(codigos, !todas)}
                                                aria-label={`${todas ? 'Desmarcar' : 'Marcar'} tudo em ${area}`}
                                                className="text-xs font-normal text-acento-texto hover:underline"
                                            >
                                                {todas ? 'Desmarcar tudo' : 'Marcar tudo'}
                                            </Button>
                                        </legend>
                                        {permissoes.map(([codigo, rotulo]) => (
                                            <Marcador
                                                key={codigo}
                                                valor={marcadas.includes(codigo)}
                                                aoMudar={(v) => marcar([codigo], v)}
                                            >
                                                {rotulo}
                                            </Marcador>
                                        ))}
                                    </fieldset>
                                )
                            })}
                        </div>
                    )}
                    <div className="flex justify-end gap-2">
                        <Botao variante="secundario" onPress={aoFechar} isDisabled={envio.isPending}>
                            Cancelar
                        </Botao>
                        <Botao grava type="submit" isPending={envio.isPending}>
                            {papel ? 'Salvar' : 'Criar papel'}
                        </Botao>
                    </div>
                </Form>
            )}
        </Janela>
    )
}
