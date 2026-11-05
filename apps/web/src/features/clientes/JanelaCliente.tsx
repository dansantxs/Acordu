import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { Form } from 'react-aria-components'
import { errosDeCampo, mensagemDeErro } from '@/shared/api/mensagens'
import { buscarCnpj, formaDeTitulo, type EmpresaDoCnpj } from '@/shared/api/cnpj'
import { buscarCep } from '@/shared/api/viaCep'
import { consultaEquipe } from '@/shared/equipe/consultaEquipe'
import { cnpjCompleto, validarDocumento } from '@/shared/format/documento'
import {
    caracteresDoDocumento,
    digitos,
    formatarCep,
    formatarDocumento,
    formatarTelefone,
} from '@/shared/format/mascaras'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoEtiquetas } from '@/shared/ui/CampoEtiquetas'
import { CampoMascarado } from '@/shared/ui/CampoMascarado'
import { CampoSelecao } from '@/shared/ui/CampoSelecao'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { Janela } from '@/shared/ui/Janela'
import { LegendaObrigatorios } from '@/shared/ui/Rotulo'
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

/** "Na Receita, este CNPJ está BAIXADA desde 12/03/2024." (decisão 4a: só no formulário, não bloqueia). */
function avisoDeSituacao(empresa: EmpresaDoCnpj): string | null {
    if (empresa.situacao === 'ATIVA') return null
    const desde = empresa.situacaoDesde ? ` desde ${empresa.situacaoDesde.split('-').reverse().join('/')}` : ''
    return `Na Receita, este CNPJ está ${empresa.situacao}${desde}. Dá para salvar mesmo assim.`
}

const DESCRICAO_CNPJ = {
    parado: 'Com o CNPJ, buscamos os dados na Receita.',
    buscando: 'Buscando os dados na Receita…',
    preencheu:
        'Preenchido com os dados da Receita; confira antes de salvar. O telefone pode ser o do escritório de contabilidade.',
    'nao-encontrado': 'Não encontramos este CNPJ na Receita. Preencha à mão.',
    falhou: 'Não deu para buscar os dados agora. Preencha à mão.',
} as const

function Secao({ titulo, nota, children }: { titulo: string; nota?: string; children: ReactNode }) {
    return (
        <fieldset className="grid gap-4 border-t border-linha pt-4 sm:grid-cols-2">
            <legend className="pr-2 text-xs font-semibold tracking-wide text-suave uppercase">{titulo}</legend>
            {nota && <p className="-mt-2 text-xs text-suave sm:col-span-2">{nota}</p>}
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
    // Nome, razão social, documento, e-mail e telefone são controlados para o CNPJ preencher os vazios (F10a).
    const [nome, setNome] = useState(cliente?.nome ?? '')
    const [razaoSocial, setRazaoSocial] = useState(cliente?.razaoSocial ?? '')
    const [documento, setDocumento] = useState(cliente?.documento ?? '')
    const [email, setEmail] = useState(cliente?.email ?? '')
    const [telefone, setTelefone] = useState(cliente?.telefone ?? '')
    const [cnpj, setCnpj] = useState<keyof typeof DESCRICAO_CNPJ>('parado')
    const [situacao, setSituacao] = useState<string | null>(null)
    // Na edição, o CNPJ que o cliente já tinha não é consultado (decisão 3a, F10-10).
    const cnpjSalvo = useRef(cnpjCompleto(cliente?.documento ?? ''))
    const ultimoCnpj = useRef<string | null>(null)
    // Fechar a janela desmonta o formulário: a consulta em andamento é abandonada sem aplicar nada (F10-09).
    const desmontado = useRef<AbortController | null>(null)
    useEffect(() => {
        const controle = new AbortController()
        desmontado.current = controle
        return () => controle.abort()
    }, [])
    // A resposta chega depois de um `await`: o preenchimento olha os valores de agora, não os de quando a consulta saiu.
    const atuais = useRef({ nome, razaoSocial, email, telefone, endereco })
    useLayoutEffect(() => {
        atuais.current = { nome, razaoSocial, email, telefone, endereco }
    })

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

    /**
     * Empresário individual (F10-29): CEP, bairro, cidade e UF da Receita entram na hora; o ViaCEP só completa o que
     * ficou vazio (logradouro, bairro) e troca a cidade pela grafia com acento se ninguém mexeu nela. A resposta só vale
     * para o CNPJ e o CEP de agora e com a janela aberta (F10-08, F10-09); o foco fica onde está.
     */
    async function completarPeloCep(base: NonNullable<EmpresaDoCnpj['semLogradouro']>, cnpj: string) {
        const numeros = digitos(base.cep)
        ultimoCep.current = numeros
        setEndereco({ ...enderecoVazio, ...base })
        const sinal = desmontado.current?.signal
        const achado = await buscarCep(numeros, sinal)
        if (!achado || sinal?.aborted || ultimoCnpj.current !== cnpj || ultimoCep.current !== numeros) return
        setEndereco((atual) => ({
            ...atual,
            logradouro: atual.logradouro || achado.logradouro,
            bairro: atual.bairro || achado.bairro,
            cidade: atual.cidade === base.cidade || !atual.cidade ? achado.cidade : atual.cidade,
            uf: atual.uf || achado.uf,
        }))
    }

    async function mudarDocumento(valor: string) {
        setDocumento(valor)
        const completo = cnpjCompleto(valor)
        if (completo === ultimoCnpj.current) return
        // Trocou ou apagou o CNPJ: some o que era do anterior (F10-14), e uma resposta atrasada dele é ignorada (F10-08).
        ultimoCnpj.current = completo
        setSituacao(null)
        setCnpj('parado')
        if (!completo || completo === cnpjSalvo.current) return // CPF ou incompleto não consulta (F10-19)
        setCnpj('buscando')
        const sinal = desmontado.current?.signal
        const resultado = await buscarCnpj(completo, sinal)
        if (sinal?.aborted || ultimoCnpj.current !== completo) return
        if (resultado.tipo !== 'achou') {
            setCnpj(resultado.tipo)
            return
        }
        setSituacao(avisoDeSituacao(resultado.empresa))
        // A frase do F10-06 só aparece quando algum campo foi de fato preenchido.
        setCnpj(preencher(resultado.empresa, completo) ? 'preencheu' : 'parado')
    }

    /**
     * Só os campos vazios (F10-03); o endereço só como bloco, e só se estiver todo vazio (F10-04). Diz se preencheu algo.
     */
    function preencher(empresa: EmpresaDoCnpj, cnpj: string): boolean {
        const agora = atuais.current
        const vazio = (v: string) => v.trim() === ''
        let preencheu = false
        const seVazio = (atual: string, novo: string, mudar: (v: string) => void) => {
            if (!vazio(atual) || vazio(novo)) return
            mudar(novo)
            preencheu = true
        }
        seVazio(agora.nome, empresa.nomeFantasia || formaDeTitulo(empresa.razaoSocial), setNome)
        seVazio(agora.razaoSocial, empresa.razaoSocial, setRazaoSocial)
        seVazio(agora.email, empresa.email, setEmail)
        seVazio(agora.telefone, empresa.telefone, setTelefone)
        const veio = empresa.endereco
        if (Object.values(agora.endereco).every(vazio)) {
            if (veio) {
                // O CEP veio da Receita: o ViaCEP não é chamado para ele (F10-05).
                ultimoCep.current = digitos(veio.cep)
                setEndereco(veio)
                preencheu = true
            } else if (empresa.semLogradouro) {
                void completarPeloCep(empresa.semLogradouro, cnpj)
                preencheu = true
            }
        }
        return preencheu
    }

    // Endereço é opcional; começou a preencher, o mínimo (CEP, logradouro, cidade, UF) passa a ser obrigatório.
    const enderecoIniciado = Object.values(endereco).some((v) => v.trim() !== '')
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
                <LegendaObrigatorios />
                {mensagem && <Alerta>{mensagem}</Alerta>}

                <div className="grid gap-4 sm:grid-cols-2">
                    <CampoMascarado
                        name="documento"
                        autoFocus
                        rotulo="CPF ou CNPJ"
                        mascara={formatarDocumento}
                        caracteres={caracteresDoDocumento}
                        teclado="text"
                        validate={validarDocumento}
                        value={documento}
                        onChange={(v) => void mudarDocumento(v)}
                        // A dica só no cliente novo: na edição, o CNPJ salvo não é consultado (F10-10).
                        descricao={cnpj === 'parado' && cliente ? undefined : DESCRICAO_CNPJ[cnpj]}
                    />
                    <CampoTexto
                        name="inscricaoEstadual"
                        rotulo="Inscrição estadual"
                        descricao="Número ou ISENTO."
                        defaultValue={cliente?.inscricaoEstadual ?? ''}
                    />
                    {situacao && (
                        <div className="sm:col-span-2">
                            <Alerta tom="aviso">{situacao}</Alerta>
                        </div>
                    )}
                    <CampoTexto
                        name="nome"
                        rotulo="Nome"
                        descricao="Como a equipe chama o cliente."
                        value={nome}
                        onChange={setNome}
                        isRequired
                    />
                    <CampoTexto
                        name="razaoSocial"
                        rotulo="Razão social"
                        value={razaoSocial}
                        onChange={setRazaoSocial}
                    />
                </div>

                <Secao titulo="Contato da empresa">
                    <CampoTexto name="email" type="email" rotulo="E-mail" value={email} onChange={setEmail} />
                    <CampoMascarado
                        name="telefone"
                        type="tel"
                        rotulo="Telefone"
                        mascara={formatarTelefone}
                        value={telefone}
                        onChange={setTelefone}
                    />
                </Secao>

                <Secao titulo="Endereço" nota="Opcional. Se preencher, CEP, logradouro, cidade e UF são obrigatórios.">
                    <CampoMascarado
                        name="endereco.cep"
                        rotulo="CEP"
                        mascara={formatarCep}
                        isRequired={enderecoIniciado}
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
                        isRequired={enderecoIniciado}
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
                        isRequired={enderecoIniciado}
                        rotulo="Cidade"
                        value={endereco.cidade}
                        onChange={mudarEndereco('cidade')}
                    />
                    <CampoSelecao
                        name="endereco.uf"
                        rotulo="UF"
                        opcoes={UFS.map((uf) => ({ id: uf, rotulo: uf }))}
                        valor={endereco.uf || null}
                        isRequired={enderecoIniciado}
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
