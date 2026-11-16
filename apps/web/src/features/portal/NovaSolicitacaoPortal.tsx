import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { Button, FileTrigger, Form, Radio, RadioGroup } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { MAXIMO_ANEXOS, TAMANHO_MAXIMO, tamanhoLegivel } from '@/shared/anexos/anexos'
import { avisar } from '@/shared/avisos/avisos'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Icone } from '@/shared/ui/Icone'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
import { abrirNoPortal, type Prioridade } from './consultas'
import { TituloPortal } from './CascaPortal'

const URGENCIAS: { valor: Prioridade; rotulo: string; dica: string }[] = [
    { valor: 'BAIXA', rotulo: 'Baixa', dica: 'Quando der' },
    { valor: 'MEDIA', rotulo: 'Média', dica: 'No ritmo normal' },
    { valor: 'ALTA', rotulo: 'Alta', dica: 'Atrapalha o trabalho' },
    { valor: 'URGENTE', rotulo: 'Urgente', dica: 'Parou tudo' },
]

/**
 * Abrir solicitação pelo portal (F7c): título, o que está acontecendo, urgência sugerida (decisão 4: aparece, já em
 * Média, e a equipe confirma) e anexos (até 5, de 20 MB, conferidos antes). Depois de abrir, vai para o detalhe.
 */
export function NovaSolicitacaoPortal() {
    const consultas = useQueryClient()
    const navegar = useNavigate()
    const [urgencia, setUrgencia] = useState<Prioridade>('MEDIA')
    const [anexos, setAnexos] = useState<File[]>([])
    const [problema, setProblema] = useState<string | null>(null)
    const envio = useMutation({
        mutationFn: (dados: { titulo: string; descricao: string | null }) =>
            abrirNoPortal({ ...dados, prioridade: urgencia }, anexos),
        onSuccess: async (s) => {
            await consultas.invalidateQueries({ queryKey: ['portal'] })
            avisar(`Solicitação #${s.numero} aberta. A equipe já foi avisada.`)
            void navegar({ to: '/portal/solicitacoes/$id', params: { id: s.id } })
        },
    })
    const adicionar = (arquivos: File[]) => {
        setProblema(null)
        const grandes = arquivos.filter((a) => a.size > TAMANHO_MAXIMO)
        const juntos = [...anexos, ...arquivos.filter((a) => a.size <= TAMANHO_MAXIMO)]
        if (grandes.length) setProblema(`${grandes.map((a) => a.name).join(', ')}: acima de 20 MB, não dá para anexar.`)
        else if (juntos.length > MAXIMO_ANEXOS) setProblema(`No máximo ${MAXIMO_ANEXOS} arquivos.`)
        setAnexos(juntos.slice(0, MAXIMO_ANEXOS))
    }

    return (
        <>
            <Link to="/portal" className="text-sm text-acento-texto hover:underline">
                ← Solicitações
            </Link>
            <TituloPortal titulo="Nova solicitação" />
            <Form
                className="flex flex-col gap-5"
                validationErrors={errosDeCampo(envio.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    const dados = new FormData(evento.currentTarget)
                    envio.mutate({
                        titulo: String(dados.get('titulo')).trim(),
                        descricao: String(dados.get('descricao') ?? '').trim() || null,
                    })
                }}
            >
                <LegendaObrigatorios />
                {envio.isError && mensagemDeErro(envio.error) && <Alerta>{mensagemDeErro(envio.error)}</Alerta>}
                <CampoTexto
                    name="titulo"
                    rotulo="Assunto"
                    descricao="Em poucas palavras, ex.: Site fora do ar, Trocar a logo do cardápio."
                    maxLength={200}
                    isRequired
                    autoFocus
                />
                <CampoTexto
                    name="descricao"
                    rotulo="O que está acontecendo?"
                    multilinha
                    descricao="Quanto mais detalhe (onde, desde quando, o que já tentou), mais rápido a equipe resolve."
                />
                <RadioGroup
                    value={urgencia}
                    onChange={(v) => setUrgencia(v as Prioridade)}
                    className="flex flex-col gap-2"
                >
                    <span className="text-sm font-semibold">Urgência</span>
                    <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                        {URGENCIAS.map((u) => (
                            <Radio
                                key={u.valor}
                                value={u.valor}
                                className="flex cursor-default flex-col rounded-couro border border-linha bg-superficie px-3 py-2 text-sm outline-none data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento data-[selected]:border-acento data-[selected]:bg-acento-suave"
                            >
                                <span className="font-semibold">{u.rotulo}</span>
                                <span className="text-xs text-suave">{u.dica}</span>
                            </Radio>
                        ))}
                    </div>
                    <span className="text-xs text-suave">
                        É uma sugestão: a equipe confirma a prioridade ao receber.
                    </span>
                </RadioGroup>
                <div className="flex flex-col gap-2">
                    <span className="text-sm font-semibold">Arquivos</span>
                    {anexos.length > 0 && (
                        <ul aria-label="Arquivos para enviar" className="flex flex-wrap gap-2">
                            {anexos.map((a, n) => (
                                <li
                                    key={`${a.name}-${n}`}
                                    className="flex items-center gap-2 rounded-couro border border-linha px-2 py-1 text-xs"
                                >
                                    <span className="max-w-48 truncate">{a.name}</span>
                                    <span className="text-suave">{tamanhoLegivel(a.size)}</span>
                                    <Button
                                        aria-label={`Tirar ${a.name}`}
                                        onPress={() => setAnexos((atuais) => atuais.filter((_, i) => i !== n))}
                                        className="grid size-5 place-items-center rounded-couro text-suave hover:bg-trilho"
                                    >
                                        <Icone nome="fechar" className="size-3" />
                                    </Button>
                                </li>
                            ))}
                        </ul>
                    )}
                    {problema && <Alerta tom="aviso">{problema}</Alerta>}
                    <div className="flex items-center gap-3">
                        <FileTrigger allowsMultiple onSelect={(lista) => lista && adicionar([...lista])}>
                            <Botao grava variante="secundario">
                                <Icone nome="clipe" />
                                Anexar
                            </Botao>
                        </FileTrigger>
                        <span className="text-xs text-suave">Print, foto ou documento: até 5, de 20 MB.</span>
                    </div>
                </div>
                <div className="flex justify-end gap-2">
                    <Botao grava type="submit" isPending={envio.isPending}>
                        Abrir solicitação
                    </Botao>
                </div>
            </Form>
        </>
    )
}
