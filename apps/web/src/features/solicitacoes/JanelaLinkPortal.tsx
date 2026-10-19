import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button, Checkbox, Form } from 'react-aria-components'
import { consultaCliente } from '@/features/clientes/consultas'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { dataHora } from '@/shared/format/prazo'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { Janela } from '@/shared/ui/Janela'
import {
    consultaLinksPortal,
    emitirLinkPortal,
    revogarLinkPortal,
    type LinkEmitido,
    type Solicitacao,
} from './consultas'

const VALIDADES = [
    { id: '7', rotulo: '7 dias' },
    { id: '30', rotulo: '30 dias' },
    { id: '90', rotulo: '90 dias' },
]

/**
 * Link do portal sem senha para um contato acompanhar esta solicitação (contrato §7, 6d3). O link só aparece uma vez,
 * logo depois de emitido; emitir de novo para o mesmo contato revoga o anterior.
 */
export function JanelaLinkPortal({
    solicitacao,
    fuso,
    podeVerCliente,
    aberta,
    aoMudar,
}: {
    solicitacao: Solicitacao
    fuso: string
    podeVerCliente: boolean
    aberta: boolean
    aoMudar: (aberta: boolean) => void
}) {
    return (
        <Janela titulo="Link do portal" aberta={aberta} aoMudar={aoMudar}>
            {aberta && (
                <Conteudo
                    solicitacao={solicitacao}
                    fuso={fuso}
                    podeVerCliente={podeVerCliente}
                    aoFechar={() => aoMudar(false)}
                />
            )}
        </Janela>
    )
}

function Conteudo({
    solicitacao: s,
    fuso,
    podeVerCliente,
    aoFechar,
}: {
    solicitacao: Solicitacao
    fuso: string
    podeVerCliente: boolean
    aoFechar: () => void
}) {
    const consultas = useQueryClient()
    const links = useQuery(consultaLinksPortal(s.id))
    const cliente = useQuery({ ...consultaCliente(s.cliente.id), enabled: podeVerCliente })
    const contatos = podeVerCliente
        ? (cliente.data?.contatos ?? []).filter((c) => c.ativo).map((c) => ({ id: c.id, rotulo: c.nome }))
        : s.contato
          ? [{ id: s.contato.id, rotulo: s.contato.nome }]
          : []
    const [contato, setContato] = useState<string | null>(s.contato?.id ?? null)
    const [validade, setValidade] = useState('30')
    const [enviarEmail, setEnviarEmail] = useState(true)
    const [emitido, setEmitido] = useState<LinkEmitido | null>(null)

    const emissao = useMutation({
        mutationFn: () => emitirLinkPortal(s.id, { contatoId: contato!, validadeDias: Number(validade), enviarEmail }),
        onSuccess: (novo) => {
            setEmitido(novo)
            void consultas.invalidateQueries({ queryKey: consultaLinksPortal(s.id).queryKey })
        },
    })
    const revogacao = useMutation({
        mutationFn: (acessoId: string) => revogarLinkPortal(s.id, acessoId),
        onSuccess: (_, acessoId) => {
            const nome = links.data?.find((l) => l.acessoId === acessoId)?.contatoNome
            avisar(`Link de ${nome ?? 'acesso'} revogado.`)
            void consultas.invalidateQueries({ queryKey: consultaLinksPortal(s.id).queryKey })
        },
        onError: avisarErro,
    })

    if (emitido) return <LinkPronto emitido={emitido} fuso={fuso} aoFechar={aoFechar} />

    return (
        <div className="flex flex-col gap-5">
            <p className="text-sm text-suave">
                O contato abre o link sem senha e vê só esta solicitação: andamento, mensagens ao cliente e anexos
                delas. Emitir de novo para a mesma pessoa cancela o link anterior.
            </p>

            {(links.data?.length ?? 0) > 0 && (
                <section aria-labelledby="links-validos" className="flex flex-col gap-2">
                    <h3 id="links-validos" className="text-sm font-semibold">
                        Links válidos
                    </h3>
                    <ul className="divide-y divide-linha rounded-couro border border-linha text-sm">
                        {links.data!.map((l) => (
                            <li key={l.acessoId} className="flex items-center justify-between gap-3 px-3 py-2">
                                <span>
                                    {l.contatoNome}{' '}
                                    <span className="text-suave">· até {dataHora(l.expiraEm, fuso)}</span>
                                </span>
                                <Button
                                    onPress={() => revogacao.mutate(l.acessoId)}
                                    isDisabled={revogacao.isPending}
                                    className="text-sm text-suave hover:text-vermelho hover:underline"
                                >
                                    Revogar
                                </Button>
                            </li>
                        ))}
                    </ul>
                </section>
            )}

            {contatos.length === 0 ? (
                <Alerta tom="aviso">
                    {podeVerCliente && cliente.isPending
                        ? 'Carregando os contatos…'
                        : 'O cliente não tem contato ativo. Cadastre um na ficha do cliente para mandar o link.'}
                </Alerta>
            ) : (
                <Form
                    className="flex flex-col gap-4"
                    validationErrors={errosDeCampo(emissao.error)}
                    onSubmit={(evento) => {
                        evento.preventDefault()
                        if (contato) emissao.mutate()
                    }}
                >
                    {emissao.isError && mensagemDeErro(emissao.error) && (
                        <Alerta>{mensagemDeErro(emissao.error)}</Alerta>
                    )}
                    <div className="grid gap-4 sm:grid-cols-2">
                        <CampoSelecao
                            name="contatoId"
                            rotulo="Para quem"
                            opcoes={contatos}
                            valor={contato}
                            aoMudar={setContato}
                            isRequired
                        />
                        <CampoSelecao
                            name="validadeDias"
                            rotulo="Vale por"
                            opcoes={VALIDADES}
                            valor={validade}
                            aoMudar={(v) => v && setValidade(v)}
                            isRequired
                        />
                    </div>
                    <Checkbox
                        isSelected={enviarEmail}
                        onChange={setEnviarEmail}
                        className="group flex items-center gap-2 text-sm"
                    >
                        <span
                            aria-hidden="true"
                            className="grid size-4 place-items-center rounded border border-linha bg-superficie group-data-[selected]:border-acento group-data-[selected]:bg-acento group-data-[focus-visible]:ring-2 group-data-[focus-visible]:ring-acento"
                        >
                            <svg
                                viewBox="0 0 12 12"
                                className="size-3 text-botao-tinta opacity-0 group-data-[selected]:opacity-100"
                            >
                                <path d="M2.5 6.5 5 9l4.5-6" fill="none" stroke="currentColor" strokeWidth="2" />
                            </svg>
                        </span>
                        Mandar o link por e-mail ao contato
                    </Checkbox>
                    <div className="flex justify-end gap-3">
                        <Botao variante="secundario" onPress={aoFechar} isDisabled={emissao.isPending}>
                            Fechar
                        </Botao>
                        <Botao type="submit" isPending={emissao.isPending}>
                            Gerar link
                        </Botao>
                    </div>
                </Form>
            )}
        </div>
    )
}

function LinkPronto({ emitido, fuso, aoFechar }: { emitido: LinkEmitido; fuso: string; aoFechar: () => void }) {
    const [copiado, setCopiado] = useState(false)
    return (
        <div className="flex flex-col gap-4">
            <Alerta tom={emitido.emailEnviado ? 'sucesso' : 'aviso'}>
                {emitido.emailEnviado
                    ? 'Link gerado e enviado por e-mail ao contato.'
                    : 'Link gerado. O e-mail não foi enviado: copie o link e mande por outro canal.'}
            </Alerta>
            <div className="flex flex-col gap-1">
                <label htmlFor="link-portal" className="text-sm font-semibold">
                    Link (vale até {dataHora(emitido.expiraEm, fuso)})
                </label>
                <div className="flex gap-2">
                    <input
                        id="link-portal"
                        readOnly
                        value={emitido.link}
                        onFocus={(e) => e.currentTarget.select()}
                        className="min-w-0 flex-1 rounded-couro border border-linha bg-trilho px-3 py-2 text-sm"
                    />
                    <Botao
                        variante="secundario"
                        onPress={() =>
                            void navigator.clipboard
                                ?.writeText(emitido.link)
                                .then(() => setCopiado(true))
                                .catch(() => undefined)
                        }
                    >
                        {copiado ? 'Copiado' : 'Copiar'}
                    </Botao>
                </div>
                <p className="text-xs text-suave">Ele só aparece agora. Depois, só dá para gerar outro.</p>
            </div>
            <div className="flex justify-end">
                <Botao onPress={aoFechar}>Concluir</Botao>
            </div>
        </div>
    )
}
