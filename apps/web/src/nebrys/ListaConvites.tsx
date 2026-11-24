import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { avisar } from '@/shared/avisos/avisos'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Janela } from '@/shared/ui/Janela'
import { Pagina } from '@/shared/ui/Pagina'
import { Paginacao } from '@/shared/ui/Paginacao'
import { apiNebrys, encerrarSeRecusada, type EnvioConvite } from './api'
import { consultaConvites, TAMANHO_PAGINA, type FiltroPagina } from './consultas'
import {
    dataHora,
    EMAIL_NAO_SAIU,
    ESTILO_SITUACAO_CONVITE,
    marcoDoConvite,
    recusaDaAcao,
    ROTULO_SITUACAO_CONVITE,
} from './convites'

/**
 * Convites de empresa (RF-AUT-08; F14-72 a F14-74): os emitidos por último primeiro, com a situação calculada pela API,
 * e "Novo convite" para um e-mail. Reenviar e revogar ficam no detalhe de cada convite. O link nunca aparece aqui: ele
 * vai só no e-mail (contrato §17).
 */
export function ListaConvites({ filtro }: { filtro: FiltroPagina }) {
    const lista = useQuery(consultaConvites(filtro))
    // A lista só recebe 404 da barreira (sessão vencida ou conta fora da lista): volta para a entrada.
    useEffect(() => encerrarSeRecusada(lista.error), [lista.error])
    const [novo, setNovo] = useState(false)
    const [aviso, setAviso] = useState<string | null>(null)

    return (
        <Pagina
            titulo="Convites"
            contexto="Área da Nebrys"
            acoes={
                <Botao
                    onPress={() => {
                        setAviso(null)
                        setNovo(true)
                    }}
                >
                    Novo convite
                </Botao>
            }
        >
            <p className="max-w-prose text-sm text-suave">
                Com o cadastro só por convite, quem recebe o link cria a empresa com o e-mail convidado. O convite vale
                por 7 dias a partir do último envio e para um cadastro só.
            </p>
            {aviso && <Alerta tom="aviso">{aviso}</Alerta>}
            {lista.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : lista.isError ? (
                <Alerta>{mensagemDeErro(lista.error)}</Alerta>
            ) : lista.data.itens.length === 0 ? (
                <EstadoVazio titulo="Nenhum convite ainda" />
            ) : (
                <>
                    <div className="overflow-x-auto rounded-couro border border-linha bg-superficie">
                        <table className="w-full text-sm">
                            <thead className="border-b border-linha text-left text-xs text-suave">
                                <tr>
                                    <th className="px-4 py-2.5 font-semibold">E-mail</th>
                                    <th className="px-4 py-2.5 font-semibold">Situação</th>
                                    <th className="px-4 py-2.5 font-semibold">Último envio</th>
                                </tr>
                            </thead>
                            <tbody>
                                {lista.data.itens.map((c) => (
                                    <tr key={c.id} className="border-b border-linha last:border-0">
                                        <td className="px-4 py-2.5">
                                            <Link
                                                to="/nebrys/convites/$id"
                                                params={{ id: c.id }}
                                                className="font-semibold hover:underline"
                                            >
                                                {c.email}
                                            </Link>
                                        </td>
                                        <td className="px-4 py-2.5">
                                            <span
                                                className={`rounded-couro px-2 py-0.5 text-xs whitespace-nowrap ${ESTILO_SITUACAO_CONVITE[c.situacao]}`}
                                            >
                                                {ROTULO_SITUACAO_CONVITE[c.situacao]}
                                            </span>
                                            <span className="mt-1 block text-xs text-suave">{marcoDoConvite(c)}</span>
                                        </td>
                                        <td className="px-4 py-2.5 whitespace-nowrap">{dataHora(c.enviadoEm)}</td>
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
            {novo && (
                <NovoConvite
                    aoFechar={() => setNovo(false)}
                    aoEmitir={(envio) => {
                        setNovo(false)
                        if (envio.emailEnviado) avisar(`Convite enviado para ${envio.convite.email}.`)
                        else setAviso(`${envio.convite.email}: ${EMAIL_NAO_SAIU}`)
                    }}
                />
            )}
        </Pagina>
    )
}

function NovoConvite({ aoFechar, aoEmitir }: { aoFechar: () => void; aoEmitir: (envio: EnvioConvite) => void }) {
    const consultas = useQueryClient()
    const emissao = useMutation({
        mutationFn: async (email: string) =>
            (await apiNebrys.POST('/api/v1/nebrys/convites', { body: { email } })).data!,
        onSuccess: (envio) => {
            void consultas.invalidateQueries({ queryKey: ['nebrys', 'convites'] })
            aoEmitir(envio)
        },
    })
    // `400` no e-mail fica no campo; `422` (convite em aberto para o e-mail) e as demais falhas, aqui.
    const recusa = recusaDaAcao(emissao.error)

    return (
        <Janela titulo="Novo convite" aberta aoMudar={(aberta) => !aberta && aoFechar()} ocupada={emissao.isPending}>
            <Form
                className="flex flex-col gap-4"
                validationErrors={errosDeCampo(emissao.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    emissao.mutate(String(new FormData(evento.currentTarget).get('email') ?? ''))
                }}
            >
                <p className="text-sm text-suave">
                    O link do cadastro vai para este e-mail, e o cadastro só passa com ele.
                </p>
                <CampoTexto name="email" type="email" rotulo="E-mail" autoComplete="off" isRequired autoFocus />
                {recusa && <Alerta>{recusa}</Alerta>}
                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={aoFechar} isDisabled={emissao.isPending}>
                        Voltar
                    </Botao>
                    <Botao type="submit" isPending={emissao.isPending}>
                        Enviar convite
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}
