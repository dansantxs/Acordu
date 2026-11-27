import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { ErroApi } from '@/shared/api/problema'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Janela } from '@/shared/ui/Janela'
import { Pagina } from '@/shared/ui/Pagina'
import { Paginacao } from '@/shared/ui/Paginacao'
import { apiNebrys, encerrarSeRecusada, type EntradaListaEspera, type EnvioConvite } from './api'
import { consultaListaEspera, TAMANHO_PAGINA, type FiltroPagina } from './consultas'
import { dataHora, EMAIL_NAO_SAIU, recusaDaAcao } from './convites'
import { data } from './situacao'
import { textoDoFornecedor } from '@/shared/config/fornecedor'

type AcaoEntrada = 'convite' | 'descarte'

const MUDOU =
    'A entrada mudou desde que você abriu a lista (outra ação ao mesmo tempo). A lista de agora está abaixo: confira e decida de novo.'
const SUMIU = 'Esta entrada não existe mais: pode ter sido descartada.'

async function agir(entrada: EntradaListaEspera, acao: AcaoEntrada): Promise<EnvioConvite | null> {
    const params = { path: { id: entrada.id } }
    if (acao === 'convite') return (await apiNebrys.POST('/api/v1/nebrys/lista-espera/{id}/convite', { params })).data!
    await apiNebrys.DELETE('/api/v1/nebrys/lista-espera/{id}', { params })
    return null
}

/**
 * Lista de espera (RF-AUT-08; F14-75 a F14-77): quem pediu acesso pela tela de cadastro, por ordem de chegada. Convidar
 * manda o convite de empresa para o e-mail da entrada, que fica convidada; descartar apaga a entrada (o convite que saiu
 * dela continua). `409` (outra ação na mesma entrada) e `404` (já descartada) releem a lista e avisam no topo; `422`
 * (já convidada, ou o e-mail tem convite em aberto) fica na janela.
 */
export function ListaEspera({ filtro }: { filtro: FiltroPagina }) {
    const consultas = useQueryClient()
    const lista = useQuery(consultaListaEspera(filtro))
    // A lista só recebe 404 da barreira (sessão vencida ou conta fora da lista): volta para a entrada.
    useEffect(() => encerrarSeRecusada(lista.error), [lista.error])
    const [aviso, setAviso] = useState<string | null>(null)
    const [aberta, setAberta] = useState<{ entrada: EntradaListaEspera; acao: AcaoEntrada } | null>(null)
    const acao = useMutation({
        mutationFn: ({ entrada, qual }: { entrada: EntradaListaEspera; qual: AcaoEntrada }) => agir(entrada, qual),
        onSuccess: (envio, { entrada }) => {
            setAberta(null)
            void consultas.invalidateQueries({ queryKey: ['nebrys', 'lista-espera'] })
            if (!envio) {
                setAviso(null)
                avisar(`Entrada de ${entrada.nome} descartada.`)
                return
            }
            void consultas.invalidateQueries({ queryKey: ['nebrys', 'convites'], refetchType: 'none' })
            if (envio.emailEnviado) {
                setAviso(null)
                avisar(`Convite enviado para ${envio.convite.email}.`)
            } else setAviso(`${envio.convite.email}: ${EMAIL_NAO_SAIU}`)
        },
        onError: (erro) => {
            if (erro instanceof ErroApi && (erro.status === 409 || erro.status === 404)) {
                setAberta(null)
                setAviso(erro.status === 409 ? MUDOU : SUMIU)
                void consultas.invalidateQueries({ queryKey: ['nebrys', 'lista-espera'] })
            }
        },
    })
    const abrir = (entrada: EntradaListaEspera, qual: AcaoEntrada) => {
        acao.reset()
        setAberta({ entrada, acao: qual })
    }
    const erroNaJanela =
        acao.error instanceof ErroApi && (acao.error.status === 409 || acao.error.status === 404)
            ? null
            : recusaDaAcao(acao.error)

    return (
        <Pagina titulo="Lista de espera" contexto="Área da Nebrys">
            <p className="max-w-prose text-sm text-suave">
                {textoDoFornecedor(
                    'Quem abre o cadastro enquanto ele é só por convite pode deixar os dados aqui. {{A fornecedor}} escolhe a quem convidar; ninguém recebe e-mail só por entrar na lista.',
                )}
            </p>
            {aviso && <Alerta tom="aviso">{aviso}</Alerta>}
            {lista.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : lista.isError ? (
                <Alerta>{mensagemDeErro(lista.error)}</Alerta>
            ) : lista.data.itens.length === 0 ? (
                <EstadoVazio titulo="Ninguém na lista de espera" />
            ) : (
                <>
                    <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
                        <table className="w-full text-sm">
                            <thead className="border-b border-linha text-left text-xs text-suave">
                                <tr>
                                    <th className="px-4 py-2.5 font-semibold">Pessoa</th>
                                    <th className="px-4 py-2.5 font-semibold">Empresa</th>
                                    <th className="px-4 py-2.5 font-semibold">Chegou em</th>
                                    <th className="px-4 py-2.5 font-semibold">Situação</th>
                                    <th className="px-4 py-2.5 text-right font-semibold">Ações</th>
                                </tr>
                            </thead>
                            <tbody>
                                {lista.data.itens.map((e) => (
                                    <tr key={e.id} className="border-b border-linha align-top last:border-0">
                                        <td className="px-4 py-2.5">
                                            <span className="block font-semibold">{e.nome}</span>
                                            <span className="text-xs text-suave">{e.email}</span>
                                        </td>
                                        <td className="px-4 py-2.5">
                                            <span className="block">{e.nomeEmpresa}</span>
                                            {e.mensagem && (
                                                <span className="block max-w-md text-xs whitespace-pre-line text-suave">
                                                    {e.mensagem}
                                                </span>
                                            )}
                                        </td>
                                        <td className="px-4 py-2.5 whitespace-nowrap">{data(e.criadaEm)}</td>
                                        <td className="px-4 py-2.5">
                                            {e.situacao === 'CONVIDADA' ? (
                                                <>
                                                    <span className="rounded-couro bg-verde-suave px-2 py-0.5 text-xs whitespace-nowrap text-verde">
                                                        Convidada
                                                    </span>
                                                    {e.convidadaEm && (
                                                        <span className="mt-1 block text-xs text-suave">
                                                            em {dataHora(e.convidadaEm)}
                                                        </span>
                                                    )}
                                                    {e.conviteId && (
                                                        <Link
                                                            to="/nebrys/convites/$id"
                                                            params={{ id: e.conviteId }}
                                                            className="mt-1 block text-xs text-acento-texto underline-offset-4 hover:underline"
                                                        >
                                                            Ver o convite
                                                        </Link>
                                                    )}
                                                </>
                                            ) : (
                                                <span className="rounded-couro bg-ambar-suave px-2 py-0.5 text-xs whitespace-nowrap text-ambar-texto">
                                                    Aguardando
                                                </span>
                                            )}
                                        </td>
                                        <td className="px-4 py-2.5">
                                            <div className="flex justify-end gap-2">
                                                {e.situacao === 'AGUARDANDO' && (
                                                    <Botao
                                                        onPress={() => abrir(e, 'convite')}
                                                        aria-label={`Convidar ${e.nome}`}
                                                    >
                                                        Convidar
                                                    </Botao>
                                                )}
                                                <Botao
                                                    variante="secundario"
                                                    onPress={() => abrir(e, 'descarte')}
                                                    aria-label={`Descartar ${e.nome}`}
                                                >
                                                    Descartar
                                                </Botao>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                    <Paginacao
                        pagina={Number(lista.data.pagina)}
                        tamanhoPagina={Number(lista.data.tamanhoPagina) || TAMANHO_PAGINA}
                        total={Number(lista.data.total)}
                    />
                </>
            )}
            {aberta && (
                <Janela
                    titulo={
                        aberta.acao === 'convite'
                            ? `Convidar ${aberta.entrada.nome}?`
                            : `Descartar a entrada de ${aberta.entrada.nome}?`
                    }
                    aberta
                    aoMudar={(aberto) => !aberto && setAberta(null)}
                    ocupada={acao.isPending}
                >
                    <p className="text-sm text-suave">
                        {aberta.acao === 'convite'
                            ? `Sai um convite de empresa para ${aberta.entrada.email}, que vale por 7 dias. A entrada fica marcada como convidada.`
                            : 'A entrada é apagada. Um convite que já saiu dela continua valendo, e a pessoa pode entrar na lista de novo.'}
                    </p>
                    {erroNaJanela && <Alerta>{erroNaJanela}</Alerta>}
                    <div className="flex justify-end gap-3">
                        <Botao variante="secundario" onPress={() => setAberta(null)} isDisabled={acao.isPending}>
                            Voltar
                        </Botao>
                        <Botao
                            onPress={() => acao.mutate({ entrada: aberta.entrada, qual: aberta.acao })}
                            isPending={acao.isPending}
                            className={aberta.acao === 'descarte' ? '!bg-vermelho !text-botao-tinta' : undefined}
                        >
                            {aberta.acao === 'convite' ? 'Enviar convite' : 'Descartar a entrada'}
                        </Botao>
                    </div>
                </Janela>
            )}
        </Pagina>
    )
}
