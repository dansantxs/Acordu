/**
 * Empresa de demonstração das capturas do manual (M2). Tudo é fictício e vem do conjunto único de dados fictícios do
 * Acordu, `src/Acordu.Application/Exemplos/demonstracao.json` (design do F14, D13; convenções §4), o mesmo que a API
 * usa nos dados de exemplo: aqui ele é lido e criado pela API pública, como faria um usuário (conta, equipe com os
 * convites aceitos pelo e-mail do Mailpit, clientes, contratos, solicitações, tarefas, projeto, recorrências e horas).
 * As datas saem do dia da execução (fuso de São Paulo), com a mesma conta da API (`DiasRelativos`), para a Agenda e o
 * Hoje terem o que mostrar em qualquer semana.
 */

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { cnpjNovo } from '../e2e/documentoDeTeste'

const API = process.env.ACORDU_API ?? 'http://localhost:5080'
const MAILPIT = process.env.ACORDU_MAILPIT ?? 'http://localhost:8025'
const SENHA = 'demonstracao-do-manual'
const sufixo = Date.now().toString(36)

/** O formato do `demonstracao.json` (o mesmo dos records de `DadosFicticios.cs`). */
interface DadosFicticios {
    empresa: { nome: string }
    pessoas: { chave: string; nome: string; email: string; papel: string; capacidadeSemanalMinutos: number }[]
    categorias: { chave: string; nome: string }[]
    clientes: {
        chave: string
        nome: string
        etiquetas?: string[] | null
        responsavel?: string | null
        email?: string | null
        razaoSocial?: string | null
        telefone?: string | null
        observacoes?: string | null
        endereco?: Record<string, string | null> | null
        contato: { nome: string; email: string }
        contrato: { tipo: string; inicioMeses: number; franquiaMinutos: number | null }
    }[]
    solicitacoes: {
        chave: string
        cliente: string
        titulo: string
        descricao: string | null
        prioridade: string
        responsavel: string | null
        categoria: string | null
        passos?: {
            acao: 'inicio' | 'interacao' | 'aguardando-cliente' | 'resolucao'
            por: string
            visibilidade?: string
            texto?: string
            solucao?: { texto: string; problema: string | null; causa: string | null }
        }[]
    }[]
    projetos: {
        chave: string
        cliente: string
        nome: string
        inicio: number
        prazo: number
        estimativaMinutos: number | null
    }[]
    tarefas: {
        chave: string
        titulo: string
        responsavel: string
        solicitacao?: string
        projeto?: string
        cliente?: string
        dia: number
        estimativaMinutos: number | null
        status: 'A_FAZER' | 'FAZENDO' | 'CONCLUIDA'
        checklist?: { texto: string; feito: boolean }[]
    }[]
    apontamentos: {
        por: string
        tarefa: string
        dia: number
        minutos: number
        descricao: string
        limite?: 'semana' | 'mes'
    }[]
    recorrencias: {
        cliente: string
        titulo: string
        responsavel: string
        periodicidade: string
        diaReferencia: number
        antecedenciaDias: number
    }[]
    cronometro: { por: string; tarefa: string } | null
    portal: { cliente: string; escopo: string } | null
}

const ARQUIVO = fileURLToPath(new URL('../../../src/Acordu.Application/Exemplos/demonstracao.json', import.meta.url))
const DADOS = JSON.parse(readFileSync(ARQUIVO, 'utf8')) as DadosFicticios

export interface Demonstracao {
    email: string
    senha: string
    solicitacaoId: string
    clienteId: string
    contratoId: string
    /** Contato da padaria com acesso ao portal (vê todas as solicitações e os contratos). */
    contato: { email: string; senha: string }
    tenantId: string
    /** Solicitação resolvida esperando a aprovação do contato. */
    entregaId: string
}

interface Pessoa {
    token: string
    id: string
}

type Corpo = Record<string, unknown> | undefined

async function chamar<T = Record<string, unknown>>(
    token: string | null,
    metodo: string,
    rota: string,
    corpo?: Corpo,
): Promise<T> {
    const resposta = await fetch(`${API}/api/v1${rota}`, {
        method: metodo,
        headers: {
            ...(corpo ? { 'Content-Type': 'application/json' } : {}),
            ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: corpo ? JSON.stringify(corpo) : null,
    })
    const texto = await resposta.text()
    if (!resposta.ok) throw new Error(`${metodo} ${rota} → ${resposta.status}: ${texto}`)
    return (texto ? JSON.parse(texto) : {}) as T
}

const id = (r: Record<string, unknown>) => r.id as string
/** As contas são globais: cada execução usa e-mails novos (o banco local fica entre uma execução e outra). */
const comSufixo = (email: string) => email.replace('@', `.${sufixo}@`)
const daChave = <T>(mapa: Map<string, T>, chave: string): T => {
    const valor = mapa.get(chave)
    if (valor === undefined) throw new Error(`Chave ${chave} ausente dos dados fictícios.`)
    return valor
}

/** Espera o e-mail chegar ao Mailpit (a outbox envia depois do commit) e devolve o token do link. */
async function tokenDoEmail(para: string, caminho: string): Promise<string> {
    const padrao = new RegExp(`/${caminho}/([A-Za-z0-9_-]+)`)
    for (let tentativa = 0; tentativa < 60; tentativa++) {
        const busca = await fetch(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:"${para}"`)}`)
        const { messages } = (await busca.json()) as { messages: { ID: string }[] }
        for (const m of messages ?? []) {
            const msg = (await (await fetch(`${MAILPIT}/api/v1/message/${m.ID}`)).json()) as { Text: string }
            const achado = padrao.exec(msg.Text)
            if (achado?.[1]) return achado[1]
        }
        await new Promise((r) => setTimeout(r, 500))
    }
    throw new Error(`O e-mail com /${caminho}/ para ${para} não chegou ao Mailpit.`)
}

// Datas no fuso da empresa (o padrão do calendário é America/Sao_Paulo), com a conta de `DiasRelativos` da API.
const hojeIso = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date())
const somarDias = (iso: string, dias: number) => {
    const d = new Date(`${iso}T12:00:00Z`)
    d.setUTCDate(d.getUTCDate() + dias)
    return d.toISOString().slice(0, 10)
}
const diaDaSemana = (iso: string) => new Date(`${iso}T12:00:00Z`).getUTCDay()
/** Soma dias úteis (negativo volta), pulando sábado e domingo; 0 é hoje. */
const util = (n: number) => {
    let d = hojeIso
    let falta = Math.abs(n)
    while (falta > 0) {
        d = somarDias(d, Math.sign(n))
        if (diaDaSemana(d) % 6 !== 0) falta--
    }
    return d
}
/** Hoje, ou a sexta anterior no fim de semana: apontamento não vai para o futuro. */
const ultimoUtil = () => (diaDaSemana(hojeIso) % 6 === 0 ? util(-1) : hojeIso)
const inicioDoMes = (meses: number) => {
    const d = new Date(`${hojeIso.slice(0, 7)}-01T12:00:00Z`)
    d.setUTCMonth(d.getUTCMonth() + meses)
    return d.toISOString().slice(0, 10)
}
const segunda = (iso: string) => somarDias(iso, -((diaDaSemana(iso) + 6) % 7))
/** O dia do apontamento, ou nulo quando ele fica de fora (`limite: semana` fora da semana do último dia útil). */
const diaDoApontamento = (a: DadosFicticios['apontamentos'][number]) => {
    const dia = a.dia === 0 ? ultimoUtil() : util(a.dia)
    if (a.limite === 'mes') return dia < inicioDoMes(0) ? ultimoUtil() : dia
    if (a.limite === 'semana') return dia < ultimoUtil() && dia >= segunda(ultimoUtil()) ? dia : null
    return dia
}

async function convidar(dono: string, papelId: string, nome: string, email: string): Promise<Pessoa> {
    await chamar(dono, 'POST', '/convites', { email, papelId })
    const token = await tokenDoEmail(email, 'convite')
    const { accessToken } = await chamar<{ accessToken: string }>(null, 'POST', '/auth/convite', {
        token,
        nome,
        senha: SENHA,
    })
    const eu = await chamar<{ usuarioId: string }>(accessToken, 'GET', '/auth/eu')
    return { token: accessToken, id: eu.usuarioId }
}

export async function criarDemonstracao(): Promise<Demonstracao> {
    // A conta e a equipe.
    const quemRegistra = DADOS.pessoas.find((p) => p.papel === 'Dono')!
    const email = comSufixo(quemRegistra.email)
    const { accessToken: dono } = await chamar<{ accessToken: string }>(null, 'POST', '/auth/registro', {
        nome: quemRegistra.nome,
        email,
        senha: SENHA,
        nomeEmpresa: DADOS.empresa.nome,
        documento: cnpjNovo(),
    })
    await chamar(null, 'POST', '/auth/confirmacao-email', {
        token: await tokenDoEmail(email, 'conta/confirmacao'),
    })
    const pessoas = new Map<string, Pessoa>([
        [
            quemRegistra.chave,
            { token: dono, id: (await chamar<{ usuarioId: string }>(dono, 'GET', '/auth/eu')).usuarioId },
        ],
    ])
    const papeis = await chamar<{ id: string; nome: string }[]>(dono, 'GET', '/papeis')
    for (const p of DADOS.pessoas.filter((outra) => outra !== quemRegistra)) {
        const papel = papeis.find((x) => x.nome === p.papel)!.id
        pessoas.set(p.chave, await convidar(dono, papel, p.nome, comSufixo(p.email)))
    }
    for (const p of DADOS.pessoas) {
        await chamar(dono, 'PUT', `/usuarios/${daChave(pessoas, p.chave).id}/capacidade`, {
            capacidadeSemanalMinutos: p.capacidadeSemanalMinutos,
        })
    }
    const pessoa = (chave: string) => daChave(pessoas, chave)
    const mencoes = (texto: string) =>
        texto.replace(/@\{([a-zA-Z]+)\}/g, (_, chave: string) => `@{${pessoa(chave).id}}`)

    const categorias = new Map<string, string>()
    for (const c of DADOS.categorias)
        categorias.set(c.chave, id(await chamar(dono, 'POST', '/categorias', { nome: c.nome, ativa: true })))

    // Clientes, cada um com um contato e um contrato com o SLA sugerido da empresa.
    const { slaPadrao } = await chamar<{ slaPadrao: unknown[] }>(dono, 'GET', '/configuracoes/empresa')
    const clientes = new Map<string, { id: string; contato: string; email: string; contrato: string }>()
    for (const c of DADOS.clientes) {
        const clienteId = id(
            await chamar(dono, 'POST', '/clientes', {
                nome: c.nome,
                documento: null,
                observacoes: c.observacoes ?? null,
                etiquetas: c.etiquetas ?? [],
                responsavelId: c.responsavel ? pessoa(c.responsavel).id : null,
                email: c.email ?? null,
                razaoSocial: c.razaoSocial ?? null,
                telefone: c.telefone ?? null,
                endereco: c.endereco ?? null,
            }),
        )
        const emailContato = comSufixo(c.contato.email)
        const contato = id(
            await chamar(dono, 'POST', `/clientes/${clienteId}/contatos`, {
                nome: c.contato.nome,
                email: emailContato,
                telefone: null,
                cargo: null,
            }),
        )
        const contrato = id(
            await chamar(dono, 'POST', '/contratos', {
                clienteId,
                tipo: c.contrato.tipo,
                inicio: inicioDoMes(c.contrato.inicioMeses),
                fim: null,
                franquiaMinutos: c.contrato.franquiaMinutos,
                sla: slaPadrao,
            }),
        )
        clientes.set(c.chave, { id: clienteId, contato, email: emailContato, contrato })
    }

    // Solicitações em vários estados, cada passo feito por quem o arquivo diz.
    const solicitacoes = new Map<string, string>()
    for (const s of DADOS.solicitacoes) {
        const cliente = daChave(clientes, s.cliente)
        const solicitacao = id(
            await chamar(dono, 'POST', '/solicitacoes', {
                clienteId: cliente.id,
                contatoId: cliente.contato,
                contratoId: cliente.contrato,
                titulo: s.titulo,
                descricao: s.descricao,
                prioridade: s.prioridade,
                categoriaId: s.categoria ? daChave(categorias, s.categoria) : null,
                responsavelId: s.responsavel ? pessoa(s.responsavel).id : null,
            }),
        )
        solicitacoes.set(s.chave, solicitacao)
        for (const passo of s.passos ?? []) {
            const quem = pessoa(passo.por).token
            const rota = `/solicitacoes/${solicitacao}/${passo.acao === 'interacao' ? 'interacoes' : passo.acao}`
            if (passo.acao === 'interacao')
                await chamar(quem, 'POST', rota, { texto: mencoes(passo.texto!), visibilidade: passo.visibilidade })
            else if (passo.acao === 'resolucao') await chamar(quem, 'POST', rota, { solucao: passo.solucao })
            else await chamar(quem, 'POST', rota)
        }
    }

    // Projeto com tarefas, mais tarefas soltas e de solicitações.
    const projetos = new Map<string, string>()
    for (const p of DADOS.projetos) {
        const cliente = daChave(clientes, p.cliente)
        projetos.set(
            p.chave,
            id(
                await chamar(dono, 'POST', '/projetos', {
                    clienteId: cliente.id,
                    nome: p.nome,
                    contratoId: cliente.contrato,
                    inicio: util(p.inicio),
                    prazo: util(p.prazo),
                    estimativaMinutos: p.estimativaMinutos,
                }),
            ),
        )
    }
    const tarefas = new Map<string, string>()
    for (const t of DADOS.tarefas) {
        const dia = util(t.dia)
        tarefas.set(
            t.chave,
            id(
                await chamar(dono, 'POST', '/tarefas', {
                    titulo: t.titulo,
                    descricao: null,
                    solicitacaoId: t.solicitacao ? daChave(solicitacoes, t.solicitacao) : null,
                    projetoId: t.projeto ? daChave(projetos, t.projeto) : null,
                    clienteId: t.cliente ? daChave(clientes, t.cliente).id : null,
                    responsavelId: pessoa(t.responsavel).id,
                    prazo: dia,
                    planejadaPara: dia,
                    estimativaMinutos: t.estimativaMinutos,
                    visivelCliente: Boolean(t.projeto),
                }),
            ),
        )
    }

    // Horas, com cada pessoa apontando as próprias.
    for (const a of DADOS.apontamentos) {
        const data = diaDoApontamento(a)
        if (!data) continue
        await chamar(pessoa(a.por).token, 'POST', '/apontamentos', {
            tarefaId: daChave(tarefas, a.tarefa),
            data,
            duracaoMinutos: a.minutos,
            descricao: a.descricao,
            descricaoExterna: null,
            faturavel: true,
            visivelCliente: null,
        })
    }

    // Status por último: primeiro as horas, depois o andamento (cada tarefa pelo responsável dela).
    for (const t of DADOS.tarefas) {
        const tarefa = daChave(tarefas, t.chave)
        const quem = pessoa(t.responsavel).token
        if (t.status !== 'A_FAZER') await chamar(quem, 'POST', `/tarefas/${tarefa}/inicio`)
        if (t.status === 'CONCLUIDA') await chamar(quem, 'POST', `/tarefas/${tarefa}/conclusao`)
        for (const item of t.checklist ?? []) {
            await chamar(dono, 'POST', `/tarefas/${tarefa}/itens`, { texto: item.texto, feito: item.feito })
        }
    }

    // Recorrências (a geração das tarefas fica com o job; aqui só a regra).
    for (const r of DADOS.recorrencias) {
        const cliente = daChave(clientes, r.cliente)
        await chamar(dono, 'POST', '/recorrencias', {
            clienteId: cliente.id,
            contratoId: cliente.contrato,
            modelo: {
                titulo: r.titulo,
                descricao: null,
                responsavelId: pessoa(r.responsavel).id,
                estimativaMinutos: 60,
                visivelCliente: true,
                checklist: null,
            },
            regra: {
                periodicidade: r.periodicidade,
                diaReferencia: r.diaReferencia,
                antecedenciaDias: r.antecedenciaDias,
                inicio: null,
                fim: null,
            },
        })
    }

    // O cronômetro rodando, para aparecer na barra lateral e no Hoje.
    if (DADOS.cronometro) {
        await chamar(pessoa(DADOS.cronometro.por).token, 'POST', '/cronometro/inicio', {
            tarefaId: daChave(tarefas, DADOS.cronometro.tarefa),
        })
    }

    // O contato entra no portal pelo convite.
    const portal = DADOS.portal ? daChave(clientes, DADOS.portal.cliente) : null
    if (DADOS.portal && portal) {
        await chamar(dono, 'POST', `/clientes/${portal.id}/contatos/${portal.contato}/acesso-portal`, {
            escopoPortal: DADOS.portal.escopo,
        })
        await chamar(null, 'POST', '/auth/portal/convite', {
            token: await tokenDoEmail(portal.email, 'portal/convite'),
            nome: DADOS.clientes.find((c) => c.chave === DADOS.portal!.cliente)!.contato.nome,
            senha: SENHA,
        })
    }

    // O que as receitas das capturas abrem: a padaria, o chamado do caixa e a entrega esperando aprovação.
    const padaria = daChave(clientes, 'padaria')
    return {
        email,
        senha: SENHA,
        solicitacaoId: daChave(solicitacoes, 'pdv'),
        clienteId: padaria.id,
        contratoId: padaria.contrato,
        contato: { email: padaria.email, senha: SENHA },
        tenantId: (await chamar<{ tenant: { id: string } }>(dono, 'GET', '/auth/eu')).tenant.id,
        entregaId: daChave(solicitacoes, 'qrcode'),
    }
}
