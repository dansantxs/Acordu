/**
 * Empresa de demonstração das capturas do manual (M2). Tudo é fictício e criado pela API pública, como faria um
 * usuário: conta, equipe (convites aceitos pelo e-mail do Mailpit), clientes, contratos, solicitações, tarefas,
 * projeto, recorrências e horas. As datas saem do dia da execução (fuso de São Paulo), para a Agenda e o Hoje terem o
 * que mostrar em qualquer semana.
 */

const API = process.env.ACORDU_API ?? 'http://localhost:5080'
const MAILPIT = process.env.ACORDU_MAILPIT ?? 'http://localhost:8025'
const SENHA = 'demonstracao-do-manual'
const sufixo = Date.now().toString(36)

export interface Demonstracao {
    email: string
    senha: string
    solicitacaoId: string
    clienteId: string
    contratoId: string
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

// Datas no fuso da empresa (o padrão do calendário é America/Sao_Paulo).
const hojeIso = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date())
const somarDias = (iso: string, dias: number) => {
    const d = new Date(`${iso}T12:00:00Z`)
    d.setUTCDate(d.getUTCDate() + dias)
    return d.toISOString().slice(0, 10)
}
const diaDaSemana = (iso: string) => new Date(`${iso}T12:00:00Z`).getUTCDay()
/** Soma dias úteis (negativo volta), pulando sábado e domingo. */
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
    const email = `marina.${sufixo}@exemplo.com`
    const { accessToken: dono } = await chamar<{ accessToken: string }>(null, 'POST', '/auth/registro', {
        nome: 'Marina Duarte',
        email,
        senha: SENHA,
        nomeEmpresa: 'Estúdio Aurora',
    })
    await chamar(null, 'POST', '/auth/confirmacao-email', {
        token: await tokenDoEmail(email, 'conta/confirmacao'),
    })
    const marina: Pessoa = { token: dono, id: (await chamar<{ usuarioId: string }>(dono, 'GET', '/auth/eu')).usuarioId }

    const papeis = await chamar<{ id: string; nome: string }[]>(dono, 'GET', '/papeis')
    const membro = papeis.find((p) => p.nome === 'Membro')!.id
    const rafael = await convidar(dono, membro, 'Rafael Nunes', `rafael.${sufixo}@exemplo.com`)
    const clara = await convidar(dono, membro, 'Clara Moreira', `clara.${sufixo}@exemplo.com`)
    await chamar(dono, 'PUT', `/usuarios/${marina.id}/capacidade`, { capacidadeSemanalMinutos: 30 * 60 })
    await chamar(dono, 'PUT', `/usuarios/${rafael.id}/capacidade`, { capacidadeSemanalMinutos: 40 * 60 })
    await chamar(dono, 'PUT', `/usuarios/${clara.id}/capacidade`, { capacidadeSemanalMinutos: 30 * 60 })

    const [suporte, melhoria] = await Promise.all(
        ['Suporte', 'Melhoria', 'Infraestrutura'].map((nome) =>
            chamar(dono, 'POST', '/categorias', { nome, ativa: true }),
        ),
    ).then((cs) => cs.map(id))

    // Clientes, cada um com um contato e um contrato.
    const cliente = async (
        nome: string,
        contato: string,
        etiquetas: string[],
        cadastro: Record<string, unknown> = {},
    ) => {
        const c = id(
            await chamar(dono, 'POST', '/clientes', {
                nome,
                documento: null,
                observacoes: null,
                etiquetas,
                responsavelId: marina.id,
                email: `contato@${nome.toLowerCase().split(' ')[0]}.exemplo.com`,
                ...cadastro,
            }),
        )
        const ct = id(
            await chamar(dono, 'POST', `/clientes/${c}/contatos`, {
                nome: contato,
                email: `${contato.toLowerCase().split(' ')[0]}.${sufixo}@cliente.exemplo.com`,
                telefone: null,
                cargo: null,
            }),
        )
        return { id: c, contato: ct }
    }
    const padaria = await cliente('Padaria Grão Dourado', 'Sérgio Almeida', ['varejo', 'delivery'], {
        razaoSocial: 'Grão Dourado Panificadora Ltda.',
        telefone: '(14) 3222-0101',
        observacoes: 'Abre às 6h; chamados do caixa são sempre urgentes.',
        endereco: {
            cep: '17012-000',
            logradouro: 'Rua das Acácias',
            numero: '410',
            complemento: null,
            bairro: 'Centro',
            cidade: 'Bauru',
            uf: 'SP',
        },
    })
    const advocacia = await cliente('Lima & Prado Advocacia', 'Helena Prado', ['escritório'])
    const casaVerde = await cliente('Casa Verde Plantas', 'Joana Ribeiro', ['varejo', 'site'])
    const academia = await cliente('Academia Movimento', 'Paulo Teixeira', ['academia'])

    // Os prazos que a tela de contrato sugere (horas úteis).
    const SLA = [
        { prioridade: 'URGENTE', primeiraRespostaMinutos: 60, resolucaoMinutos: 4 * 60 },
        { prioridade: 'ALTA', primeiraRespostaMinutos: 2 * 60, resolucaoMinutos: 16 * 60 },
        { prioridade: 'MEDIA', primeiraRespostaMinutos: 4 * 60, resolucaoMinutos: 32 * 60 },
        { prioridade: 'BAIXA', primeiraRespostaMinutos: 8 * 60, resolucaoMinutos: 80 * 60 },
    ]
    const contrato = async (clienteId: string, tipo: string, franquiaMinutos: number | null) =>
        id(
            await chamar(dono, 'POST', '/contratos', {
                clienteId,
                tipo,
                inicio: inicioDoMes(-3),
                fim: null,
                franquiaMinutos,
                sla: SLA,
            }),
        )
    const cPadaria = await contrato(padaria.id, 'BANCO_HORAS', 20 * 60)
    const cAdvocacia = await contrato(advocacia.id, 'MENSAL_FIXO', 30 * 60)
    const cCasaVerde = await contrato(casaVerde.id, 'AVULSO', null)
    const cAcademia = await contrato(academia.id, 'BANCO_HORAS', 10 * 60)

    // Solicitações em vários estados.
    const solicitacao = async (
        c: { id: string; contato: string },
        contratoId: string,
        titulo: string,
        prioridade: string,
        responsavelId: string | null,
        categoriaId: string | null,
        descricao: string,
    ) =>
        id(
            await chamar(dono, 'POST', '/solicitacoes', {
                clienteId: c.id,
                contatoId: c.contato,
                contratoId,
                titulo,
                descricao,
                prioridade,
                categoriaId,
                responsavelId,
            }),
        )
    const sPdv = await solicitacao(
        padaria,
        cPadaria,
        'Caixa não imprime o cupom desde ontem',
        'URGENTE',
        rafael.id,
        suporte,
        'A impressora do caixa 2 liga, mas o cupom sai em branco. O caixa 1 está normal.',
    )
    await chamar(rafael.token, 'POST', `/solicitacoes/${sPdv}/inicio`)
    await chamar(rafael.token, 'POST', `/solicitacoes/${sPdv}/interacoes`, {
        texto: 'Oi, Sérgio! Já estou vendo. Pode me mandar uma foto do cupom em branco?',
        visibilidade: 'EXTERNA',
    })
    await chamar(rafael.token, 'POST', `/solicitacoes/${sPdv}/interacoes`, {
        texto: `Parece a cabeça térmica. @{${marina.id}}, temos uma impressora reserva para emprestar?`,
        visibilidade: 'INTERNA',
    })

    const sCardapio = await solicitacao(
        padaria,
        cPadaria,
        'Atualizar os preços do cardápio no site',
        'MEDIA',
        clara.id,
        melhoria,
        'Segue a planilha com os preços novos, valendo a partir de segunda.',
    )
    await chamar(clara.token, 'POST', `/solicitacoes/${sCardapio}/inicio`)
    await chamar(clara.token, 'POST', `/solicitacoes/${sCardapio}/aguardando-cliente`)

    const sEmail = await solicitacao(
        advocacia,
        cAdvocacia,
        'Criar e-mail para a nova advogada',
        'ALTA',
        marina.id,
        suporte,
        'A Dra. Beatriz começa na segunda e precisa de e-mail e acesso à pasta de processos.',
    )
    await chamar(dono, 'POST', `/solicitacoes/${sEmail}/inicio`)

    await solicitacao(
        advocacia,
        cAdvocacia,
        'Lentidão ao abrir a pasta compartilhada',
        'MEDIA',
        null,
        suporte,
        'Desde a semana passada demora quase um minuto para abrir a pasta de processos.',
    )
    const sWifi = await solicitacao(
        academia,
        cAcademia,
        'Wi-Fi da recepção caindo à tarde',
        'ALTA',
        null,
        null,
        'Os alunos reclamam que a rede cai entre 17h e 19h.',
    )
    await solicitacao(
        casaVerde,
        cCasaVerde,
        'Trocar as fotos da página inicial',
        'BAIXA',
        clara.id,
        melhoria,
        'Mandamos as fotos novas da coleção de primavera.',
    )
    const sBackup = await solicitacao(
        academia,
        cAcademia,
        'Recuperar planilha apagada sem querer',
        'ALTA',
        rafael.id,
        suporte,
        'Apagaram a planilha de matrículas de março da pasta da recepção.',
    )
    await chamar(rafael.token, 'POST', `/solicitacoes/${sBackup}/inicio`)
    await chamar(rafael.token, 'POST', `/solicitacoes/${sBackup}/resolucao`, {
        solucao: {
            problema: 'Planilha apagada da pasta compartilhada.',
            causa: 'Exclusão acidental.',
            texto: 'Restaurada a versão de ontem a partir do backup noturno.',
        },
    })

    // Projeto com tarefas, mais tarefas soltas e de solicitações.
    const projeto = id(
        await chamar(dono, 'POST', '/projetos', {
            clienteId: casaVerde.id,
            nome: 'Novo site da Casa Verde',
            contratoId: cCasaVerde,
            inicio: util(-6),
            prazo: util(15),
            estimativaMinutos: 40 * 60,
        }),
    )
    const tarefa = async (
        titulo: string,
        quem: Pessoa,
        extra: { solicitacaoId?: string; projetoId?: string; clienteId?: string },
        dia: string,
        estimativa: number,
    ) =>
        id(
            await chamar(dono, 'POST', '/tarefas', {
                titulo,
                descricao: null,
                solicitacaoId: extra.solicitacaoId ?? null,
                projetoId: extra.projetoId ?? null,
                clienteId: extra.clienteId ?? null,
                responsavelId: quem.id,
                prazo: dia,
                planejadaPara: dia,
                estimativaMinutos: estimativa,
                visivelCliente: Boolean(extra.projetoId),
            }),
        )
    const tLayout = await tarefa('Layout da página inicial', clara, { projetoId: projeto }, util(-2), 6 * 60)
    const tCatalogo = await tarefa('Catálogo de produtos', clara, { projetoId: projeto }, util(2), 8 * 60)
    const tCheckout = await tarefa('Integração com o pagamento', rafael, { projetoId: projeto }, util(6), 10 * 60)
    await tarefa('Textos das páginas institucionais', marina, { projetoId: projeto }, util(4), 3 * 60)
    const tImpressora = await tarefa('Trocar a impressora do caixa 2', rafael, { solicitacaoId: sPdv }, hojeIso, 90)
    const tConta = await tarefa('Criar a conta de e-mail e o acesso', marina, { solicitacaoId: sEmail }, hojeIso, 45)
    const tPlanilha = await tarefa('Restaurar a planilha do backup', rafael, { solicitacaoId: sBackup }, util(-1), 60)
    const tRoteador = await tarefa('Revisar o roteador da recepção', rafael, { solicitacaoId: sWifi }, util(1), 2 * 60)
    await tarefa('Orçamento do novo servidor', marina, { clienteId: advocacia.id }, util(3), 2 * 60)
    const tBanner = await tarefa('Ajustar o banner da promoção', clara, { solicitacaoId: sCardapio }, util(-3), 60)

    // Horas da semana (e da anterior), com cada pessoa apontando as próprias.
    const apontar = (quem: Pessoa, tarefaId: string, dia: number, minutos: number, descricao: string) =>
        chamar(quem.token, 'POST', '/apontamentos', {
            tarefaId,
            data: dia === 0 ? ultimoUtil() : util(dia),
            duracaoMinutos: minutos,
            descricao,
            descricaoExterna: null,
            faturavel: true,
            visivelCliente: null,
        })
    await apontar(clara, tLayout, -4, 180, 'Rascunho do layout')
    await apontar(clara, tLayout, -3, 150, 'Ajustes depois da reunião')
    await apontar(clara, tCatalogo, -1, 120, 'Estrutura do catálogo')
    await apontar(clara, tCatalogo, 0, 90, 'Fotos dos produtos')
    await apontar(rafael, tPlanilha, -1, 50, 'Restauração do backup')
    await apontar(rafael, tImpressora, 0, 40, 'Diagnóstico da impressora')
    await apontar(rafael, tCheckout, -2, 210, 'Conta de testes do pagamento')
    await apontar(rafael, tCheckout, -6, 120, 'Levantamento dos meios de pagamento')
    await apontar(marina, tConta, 0, 30, 'Caixa de e-mail criada')
    await apontar(marina, tLayout, -5, 60, 'Reunião de briefing com a Joana')
    // A Marina aponta todo dia da semana, para a tela Horas ter o que agrupar.
    const nestaSemana: number[] = []
    for (let d = -1; diaDaSemana(util(d)) < diaDaSemana(ultimoUtil()); d--) nestaSemana.push(d)
    for (const [i, d] of nestaSemana.entries()) {
        await apontar(marina, tConta, d, 30 + 15 * i, i % 2 ? 'Reunião com a Helena' : 'Atendimento por telefone')
    }
    // Consumo dos bancos de horas no mês: só conta tarefa com contrato pela origem (aqui, a solicitação) (a Academia passa dos 80%, para a régua mostrar o alerta).
    const noMes = (d: number) => (util(d) < inicioDoMes(0) ? 0 : d)
    for (const [d, min, texto] of [
        [-2, 240, 'Arte do banner e versões para as redes'],
        [-6, 300, 'Campanha de Dia das Crianças'],
        [-9, 180, 'Cardápio novo no site'],
    ] as const) {
        await apontar(clara, tBanner, noMes(d), min, texto)
    }
    for (const [d, min, texto] of [
        [-1, 180, 'Troca do ponto de acesso da recepção'],
        [-4, 150, 'Cabeamento da sala de spinning'],
        [-7, 120, 'Configuração da rede de convidados'],
    ] as const) {
        await apontar(rafael, tRoteador, noMes(d), min, texto)
    }

    // Status por último: primeiro as horas, depois o andamento.
    for (const [t, quem] of [
        [tLayout, clara],
        [tPlanilha, rafael],
    ] as const) {
        await chamar(quem.token, 'POST', `/tarefas/${t}/inicio`)
        await chamar(quem.token, 'POST', `/tarefas/${t}/conclusao`)
    }
    await chamar(clara.token, 'POST', `/tarefas/${tCatalogo}/inicio`)
    await chamar(rafael.token, 'POST', `/tarefas/${tImpressora}/inicio`)
    await chamar(rafael.token, 'POST', `/tarefas/${tCheckout}/inicio`)
    await chamar(dono, 'POST', `/tarefas/${tConta}/inicio`)
    for (const item of ['Criar a caixa no provedor', 'Liberar a pasta de processos', 'Mandar a senha provisória']) {
        await chamar(dono, 'POST', `/tarefas/${tConta}/itens`, {
            texto: item,
            feito: item === 'Criar a caixa no provedor',
        })
    }

    // Recorrências (a geração das tarefas fica com o job; aqui só a regra).
    const recorrencia = (clienteId: string, contratoId: string, titulo: string, quem: Pessoa, regra: object) =>
        chamar(dono, 'POST', '/recorrencias', {
            clienteId,
            contratoId,
            modelo: {
                titulo,
                descricao: null,
                responsavelId: quem.id,
                estimativaMinutos: 60,
                visivelCliente: true,
                checklist: null,
            },
            regra,
        })
    await recorrencia(advocacia.id, cAdvocacia, 'Conferir o backup dos processos', rafael, {
        periodicidade: 'SEMANAL',
        diaReferencia: 1,
        antecedenciaDias: 0,
        inicio: null,
        fim: null,
    })
    await recorrencia(padaria.id, cPadaria, 'Relatório mensal de atendimento', marina, {
        periodicidade: 'MENSAL',
        diaReferencia: 5,
        antecedenciaDias: 2,
        inicio: null,
        fim: null,
    })
    await recorrencia(academia.id, cAcademia, 'Revisão trimestral da rede', rafael, {
        periodicidade: 'TRIMESTRAL',
        diaReferencia: 10,
        antecedenciaDias: 5,
        inicio: null,
        fim: null,
    })

    // O cronômetro da Marina rodando, para aparecer na barra lateral e no Hoje.
    await chamar(dono, 'POST', '/cronometro/inicio', { tarefaId: tConta })

    return { email, senha: SENHA, solicitacaoId: sPdv, clienteId: padaria.id, contratoId: cPadaria }
}
