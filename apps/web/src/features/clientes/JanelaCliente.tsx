import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useRef, useState, type ReactNode } from 'react'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { buscarCep } from '@/shared/api/viaCep'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { digitos, formatarCep, formatarDocumento, formatarTelefone } from '@/shared/format/mascaras'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoEtiquetas } from '@/shared/ui/CampoEtiquetas'
import { CampoMascarado } from '@/shared/ui/CampoMascarado'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import {
    consultaEtiquetas,
    criarCliente,
    editarCliente,
    MAXIMO_ETIQUETAS,
    opcional,
    type Cliente,
    type DadosCliente,
    type Endereco,
} from './consultas'

export const UFS = [
    'AC', 'AL', 'AP', 'AM', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MT', 'MS', 'MG', 'PA',
    'PB', 'PR', 'PE', 'PI', 'RJ', 'RN', 'RS', 'RO', 'RR', 'SC', 'SP', 'SE', 'TO',
] // prettier-ignore

type CamposEndereco = Record<keyof Endereco, string>

const enderecoVazio: CamposEndereco = {
    cep: '',
    logradouro: '',
    numero: '',
    complemento: '',
    bairro: '',
    cidade: '',
    uf: '',
}

function enderecoInicial(endereco: Endereco | null | undefined): CamposEndereco {
    if (!endereco) return enderecoVazio
    return {
        cep: formatarCep(endereco.cep),
        logradouro: endereco.logradouro,
        numero: endereco.numero ?? '',
        complemento: endereco.complemento ?? '',
        bairro: endereco.bairro ?? '',
        cidade: endereco.cidade,
        uf: endereco.uf,
    }
}

/** Endereço todo em branco não vai (nulo); qualquer campo preenchido manda o objeto, e a API diz o que falta. */
function enderecoParaEnvio(campos: CamposEndereco): Endereco | null {
    if (Object.values(campos).every((v) => v.trim() === '')) return null
    return {
        cep: campos.cep,
        logradouro: campos.logradouro,
        numero: opcional(campos.numero),
        complemento: opcional(campos.complemento),
        bairro: opcional(campos.bairro),
        cidade: campos.cidade,
        uf: campos.uf,
    }
}

function Secao({ titulo, children }: { titulo: string; children: ReactNode }) {
    return (
        <fieldset className="grid gap-4 border-t border-linha pt-4 sm:grid-cols-2">
            <legend className="pr-2 text-xs font-semibold tracking-wide text-suave uppercase">{titulo}</legend>
            {children}
        </fieldset>
    )
}

/** Criar (sem `cliente`) ou editar todo o cadastro (contrato §4.1; ampliado em 16/10). */
export function JanelaCliente({
    cliente,
    aberta,
    aoMudar,
    aoSalvar,
}: {
    cliente?: Cliente
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    aoSalvar?: (cliente: Cliente) => void
}) {
    return (
        <JanelaClienteAberta
            key={aberta ? 'aberta' : 'fechada'}
            cliente={cliente}
            aberta={aberta}
            aoMudar={aoMudar}
            aoSalvar={aoSalvar}
        />
    )
}

function JanelaClienteAberta({
    cliente,
    aberta,
    aoMudar,
    aoSalvar,
}: {
    cliente?: Cliente
    aberta: boolean
    aoMudar: (aberta: boolean) => void
    aoSalvar?: (cliente: Cliente) => void
}) {
    const consultas = useQueryClient()
    const equipe = useQuery({ ...consultaEquipe, enabled: aberta })
    const etiquetasEmUso = useQuery({ ...consultaEtiquetas, enabled: aberta })
    const [responsavel, setResponsavel] = useState<string | null>(cliente?.responsavel?.id ?? null)
    const [etiquetas, setEtiquetas] = useState<string[]>(cliente?.etiquetas ?? [])
    const [endereco, setEndereco] = useState<CamposEndereco>(() => enderecoInicial(cliente?.endereco))
    const [cep, setCep] = useState<'parado' | 'buscando' | 'achou' | 'nao-achou'>('parado')
    const ultimoCep = useRef(digitos(endereco.cep))
    const numero = useRef<HTMLInputElement>(null)

    const envio = useMutation({
        mutationFn: (dados: DadosCliente) => (cliente ? editarCliente(cliente, dados) : criarCliente(dados)),
        onSuccess: async (salvo) => {
            await consultas.invalidateQueries({ queryKey: ['clientes'] })
            aoMudar(false)
            aoSalvar?.(salvo)
        },
    })
    const mensagem = envio.isError ? mensagemDeErro(envio.error) : null

    const mudarEndereco = (campo: keyof CamposEndereco) => (valor: string) =>
        setEndereco((atual) => ({ ...atual, [campo]: valor }))

    async function mudarCep(valor: string) {
        setEndereco((atual) => ({ ...atual, cep: valor }))
        const numeros = digitos(valor)
        if (numeros.length !== 8 || numeros === ultimoCep.current) return
        ultimoCep.current = numeros
        setCep('buscando')
        const achado = await buscarCep(numeros)
        if (ultimoCep.current !== numeros) return // digitou outro CEP enquanto buscava
        if (!achado) {
            setCep('nao-achou')
            return
        }
        setCep('achou')
        setEndereco((atual) => ({
            ...atual,
            logradouro: achado.logradouro || atual.logradouro,
            bairro: achado.bairro || atual.bairro,
            cidade: achado.cidade,
            uf: achado.uf,
        }))
        numero.current?.focus()
    }

    const pessoas = (equipe.data ?? []).map((p) => ({ id: p.id, rotulo: p.nome }))
    // Quem saiu da equipe continua aparecendo como responsável até alguém trocar.
    if (cliente?.responsavel && !pessoas.some((p) => p.id === cliente.responsavel!.id)) {
        pessoas.push({ id: cliente.responsavel.id, rotulo: cliente.responsavel.nome || 'Pessoa que saiu da equipe' })
    }

    return (
        <Janela
            titulo={cliente ? 'Editar cliente' : 'Novo cliente'}
            aberta={aberta}
            aoMudar={(estado) => {
                if (!estado) envio.reset()
                aoMudar(estado)
            }}
            ocupada={envio.isPending}
            larga
        >
            <Form
                className="flex flex-col gap-5"
                validationErrors={errosDeCampo(envio.error)}
                onSubmit={(evento) => {
                    evento.preventDefault()
                    const dados = new FormData(evento.currentTarget)
                    envio.mutate({
                        nome: String(dados.get('nome')).trim(),
                        razaoSocial: opcional(dados.get('razaoSocial')),
                        documento: opcional(dados.get('documento')),
                        inscricaoEstadual: opcional(dados.get('inscricaoEstadual')),
                        email: opcional(dados.get('email')),
                        telefone: opcional(dados.get('telefone')),
                        endereco: enderecoParaEnvio(endereco),
                        responsavelId: responsavel,
                        etiquetas,
                        observacoes: opcional(dados.get('observacoes')),
                    })
                }}
            >
                {mensagem && <Alerta>{mensagem}</Alerta>}

                <div className="grid gap-4 sm:grid-cols-2">
                    <CampoTexto
                        name="nome"
                        rotulo="Nome"
                        descricao="Como a equipe chama o cliente."
                        defaultValue={cliente?.nome}
                        isRequired
                        autoFocus
                    />
                    <CampoTexto name="razaoSocial" rotulo="Razão social" defaultValue={cliente?.razaoSocial ?? ''} />
                    <CampoMascarado
                        name="documento"
                        rotulo="CPF ou CNPJ"
                        mascara={formatarDocumento}
                        defaultValue={cliente?.documento}
                    />
                    <CampoTexto
                        name="inscricaoEstadual"
                        rotulo="Inscrição estadual"
                        descricao="Número ou ISENTO."
                        defaultValue={cliente?.inscricaoEstadual ?? ''}
                    />
                </div>

                <Secao titulo="Contato da empresa">
                    <CampoTexto name="email" type="email" rotulo="E-mail" defaultValue={cliente?.email ?? ''} />
                    <CampoMascarado
                        name="telefone"
                        type="tel"
                        rotulo="Telefone"
                        mascara={formatarTelefone}
                        defaultValue={cliente?.telefone}
                    />
                </Secao>

                <Secao titulo="Endereço">
                    <CampoMascarado
                        name="endereco.cep"
                        rotulo="CEP"
                        mascara={formatarCep}
                        value={endereco.cep}
                        onChange={(v) => void mudarCep(v)}
                        descricao={
                            cep === 'buscando'
                                ? 'Buscando o endereço…'
                                : cep === 'nao-achou'
                                  ? 'CEP não encontrado: preencha o endereço.'
                                  : 'Com o CEP, o resto se preenche sozinho.'
                        }
                    />
                    <div className="hidden sm:block" />
                    <CampoTexto
                        name="endereco.logradouro"
                        rotulo="Logradouro"
                        value={endereco.logradouro}
                        onChange={mudarEndereco('logradouro')}
                        className="sm:col-span-2"
                    />
                    <div className="grid grid-cols-2 gap-4">
                        <CampoTexto
                            refEntrada={numero}
                            name="endereco.numero"
                            rotulo="Número"
                            value={endereco.numero}
                            onChange={mudarEndereco('numero')}
                        />
                        <CampoTexto
                            name="endereco.complemento"
                            rotulo="Complemento"
                            value={endereco.complemento}
                            onChange={mudarEndereco('complemento')}
                        />
                    </div>
                    <CampoTexto
                        name="endereco.bairro"
                        rotulo="Bairro"
                        value={endereco.bairro}
                        onChange={mudarEndereco('bairro')}
                    />
                    <CampoTexto
                        name="endereco.cidade"
                        rotulo="Cidade"
                        value={endereco.cidade}
                        onChange={mudarEndereco('cidade')}
                    />
                    <CampoSelecao
                        name="endereco.uf"
                        rotulo="UF"
                        opcoes={UFS.map((uf) => ({ id: uf, rotulo: uf }))}
                        valor={endereco.uf || null}
                        aoMudar={(uf) => mudarEndereco('uf')(uf ?? '')}
                        vazio="—"
                    />
                </Secao>

                <Secao titulo="Na equipe">
                    <CampoSelecao
                        name="responsavelId"
                        rotulo="Responsável"
                        descricao="Sugerido como responsável das solicitações novas deste cliente."
                        opcoes={pessoas}
                        valor={responsavel}
                        aoMudar={setResponsavel}
                        vazio="Ninguém"
                    />
                    <CampoEtiquetas
                        rotulo="Etiquetas"
                        descricao={`Até ${MAXIMO_ETIQUETAS}, para filtrar a lista.`}
                        valor={etiquetas}
                        aoMudar={setEtiquetas}
                        sugestoes={etiquetasEmUso.data ?? []}
                        maximo={MAXIMO_ETIQUETAS}
                        erro={errosDeCampo(envio.error).etiquetas?.[0]}
                    />
                    <CampoTexto
                        name="observacoes"
                        rotulo="Observações"
                        descricao="Só a equipe vê."
                        defaultValue={cliente?.observacoes ?? ''}
                        multilinha
                        className="sm:col-span-2"
                    />
                </Secao>

                <div className="flex justify-end gap-3">
                    <Botao variante="secundario" onPress={() => aoMudar(false)} isDisabled={envio.isPending}>
                        Cancelar
                    </Botao>
                    <Botao type="submit" isPending={envio.isPending}>
                        {cliente ? 'Salvar' : 'Criar cliente'}
                    </Botao>
                </div>
            </Form>
        </Janela>
    )
}
