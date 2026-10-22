import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { FileTrigger, Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { avisar, avisarErro } from '@/shared/avisos/avisos'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { consultaPortal, enviarLogo, removerLogo, salvarPortal, type ConfiguracaoPortal } from './consultas'
import { CabecalhoSecao } from './TelaConfiguracoes'

const TIPOS_LOGO = ['image/png', 'image/jpeg', 'image/webp']
const TAMANHO_LOGO = 512 * 1024

/** Portal (F5b, RF-POR-09): como o cliente vê o prestador e quando a solicitação resolvida fecha sozinha. */
export function SecaoPortal() {
    const portal = useQuery(consultaPortal)
    return (
        <>
            <CabecalhoSecao titulo="Portal">
                Como o cliente vê a empresa no portal e nos e-mails. Os acessos de cada contato ficam na ficha do
                cliente.
            </CabecalhoSecao>
            {portal.isPending ? (
                <p className="text-sm text-suave">Carregando…</p>
            ) : portal.isError ? (
                <Alerta>{mensagemDeErro(portal.error)}</Alerta>
            ) : (
                <div className="flex max-w-2xl flex-col gap-8">
                    <Logotipo portal={portal.data} />
                    <Formulario portal={portal.data} />
                </div>
            )}
        </>
    )
}

function Logotipo({ portal }: { portal: ConfiguracaoPortal }) {
    const consultas = useQueryClient()
    const [erro, setErro] = useState<string | null>(null)
    const aoSalvar = (salvo: ConfiguracaoPortal) => consultas.setQueryData(consultaPortal.queryKey, salvo)
    const envio = useMutation({
        mutationFn: enviarLogo,
        onSuccess: (salvo) => {
            aoSalvar(salvo)
            avisar('Logotipo atualizado.')
        },
        onError: (e) => setErro(e instanceof ErroApi ? (e.doCampo('logo') ?? mensagemDeErro(e)) : mensagemDeErro(e)),
    })
    const remocao = useMutation({
        mutationFn: removerLogo,
        onSuccess: (salvo) => {
            aoSalvar(salvo)
            avisar('Logotipo removido.')
        },
        onError: avisarErro,
    })
    return (
        <section aria-labelledby="logo-portal" className="flex flex-col gap-3">
            <h3 id="logo-portal" className="text-sm font-semibold">
                Logotipo
            </h3>
            <div className="flex flex-wrap items-center gap-4">
                <div className="grid h-20 w-40 place-items-center overflow-hidden rounded-couro border border-linha bg-superficie">
                    {portal.logoUrl ? (
                        <img
                            src={portal.logoUrl}
                            alt="Logotipo atual"
                            className="max-h-full max-w-full object-contain"
                        />
                    ) : (
                        <span className="text-xs text-suave">Sem logotipo</span>
                    )}
                </div>
                <div className="flex flex-col gap-2">
                    <div className="flex gap-2">
                        <FileTrigger
                            acceptedFileTypes={TIPOS_LOGO}
                            onSelect={(lista) => {
                                const arquivo = lista?.[0]
                                if (!arquivo) return
                                setErro(null)
                                if (!TIPOS_LOGO.includes(arquivo.type)) return setErro('Use PNG, JPEG ou WebP.')
                                if (arquivo.size > TAMANHO_LOGO) return setErro('O logotipo vai até 512 KB.')
                                envio.mutate(arquivo)
                            }}
                        >
                            <Botao variante="secundario" isPending={envio.isPending}>
                                {portal.logoUrl ? 'Trocar logotipo' : 'Enviar logotipo'}
                            </Botao>
                        </FileTrigger>
                        {portal.logoUrl && (
                            <Botao variante="discreto" isPending={remocao.isPending} onPress={() => remocao.mutate()}>
                                Remover
                            </Botao>
                        )}
                    </div>
                    <p className={`text-xs ${erro ? 'text-vermelho' : 'text-suave'}`} role={erro ? 'alert' : undefined}>
                        {erro ?? 'PNG, JPEG ou WebP, até 512 KB. Fundo transparente fica melhor.'}
                    </p>
                </div>
            </div>
        </section>
    )
}

function Formulario({ portal }: { portal: ConfiguracaoPortal }) {
    const consultas = useQueryClient()
    const [dias, setDias] = useState(String(portal.diasFechamentoAutomatico))
    const envio = useMutation({
        mutationFn: salvarPortal,
        onSuccess: (salvo) => {
            consultas.setQueryData(consultaPortal.queryKey, salvo)
            avisar('Portal salvo.')
        },
    })
    const numero = Number(dias)
    const diasValidos = Number.isInteger(numero) && numero >= 1 && numero <= 60
    const erroDias = !diasValidos
        ? 'De 1 a 60 dias.'
        : envio.error instanceof ErroApi
          ? envio.error.doCampo('diasFechamentoAutomatico')
          : undefined
    return (
        <Form
            className="flex flex-col gap-5"
            validationErrors={errosDeCampo(envio.error)}
            onSubmit={(evento) => {
                evento.preventDefault()
                if (!diasValidos) return
                const nome = String(new FormData(evento.currentTarget).get('nomeExibido') ?? '').trim()
                envio.mutate({ nomeExibido: nome || null, diasFechamentoAutomatico: numero })
            }}
        >
            {envio.isError && mensagemDeErro(envio.error) && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
            <CampoTexto
                name="nomeExibido"
                rotulo="Nome no portal"
                defaultValue={portal.nomeExibido ?? ''}
                maxLength={200}
                descricao={`Vazio: usa o nome da empresa (${portal.nomeTenant}). Também é o nome no "De" dos e-mails ao cliente.`}
            />
            <div className="flex flex-col gap-1">
                <label htmlFor="portal-dias" className="text-sm font-semibold">
                    Fechar a solicitação resolvida depois de
                </label>
                <span className="flex items-center gap-2 text-sm">
                    <input
                        id="portal-dias"
                        type="number"
                        inputMode="numeric"
                        min={1}
                        max={60}
                        value={dias}
                        onChange={(e) => setDias(e.target.value)}
                        aria-invalid={erroDias ? true : undefined}
                        aria-describedby="portal-dias-d"
                        className="w-20 rounded-couro border border-linha bg-superficie px-3 py-2 text-sm text-tinta aria-[invalid]:border-vermelho"
                    />
                    dias sem resposta do cliente
                </span>
                <p id="portal-dias-d" className={`text-xs ${erroDias ? 'text-vermelho' : 'text-suave'}`}>
                    {erroDias ?? 'O cliente pode reabrir antes disso, pelo portal ou respondendo o e-mail.'}
                </p>
            </div>
            <div>
                <Botao type="submit" isPending={envio.isPending}>
                    Salvar
                </Botao>
            </div>
        </Form>
    )
}
