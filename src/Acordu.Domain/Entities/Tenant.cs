using System.Globalization;
using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// O prestador de serviço (segurança §2). É a própria fronteira do isolamento: o <see cref="Id"/> é o <c>TenantId</c>
/// de todo o resto. Não herda <see cref="EntidadeBase"/>, que exigiria um TenantId separado e redundante.
/// </summary>
public sealed class Tenant
{
    public const int DiasFechamentoPadrao = 5;
    public const int DiasFechamentoMinimo = 1;
    public const int DiasFechamentoMaximo = 60;

    private Tenant(Guid id, string nome, DateTimeOffset criadoEm, Guid criadoPorUsuarioId, DateTimeOffset fimDoTeste)
    {
        Id = id;
        Nome = nome;
        CriadoEm = criadoEm;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        DiasFechamentoAutomatico = DiasFechamentoPadrao;
        Situacao = SituacaoEmpresa.Teste;
        FimDoTeste = fimDoTeste;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Tenant()
    {
        Nome = null!;
    }

    public Guid Id { get; private set; }

    /// <summary>Nome da empresa do prestador, exibido para a equipe e no portal.</summary>
    public string Nome { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>
    /// Quem registrou a empresa (RF-AUT-06, F9): só a confirmação do e-mail dele libera o envio a terceiros. Nulo só em
    /// empresa anterior ao F9 sem Dono (a migration preenche com o Dono mais antigo).
    /// </summary>
    public Guid? CriadoPorUsuarioId { get; private set; }

    /// <summary>
    /// Quando a empresa passou a poder enviar e-mail a terceiros (convites, links, avisos): a primeira vez que o e-mail
    /// do criador ficou confirmado. Nunca volta a nulo (spec do F9, decisão 2). Gravada pela Infrastructure na
    /// confirmação, por atualização direta: a rota é pública e não tem o contexto da empresa (convenções §1).
    /// </summary>
    public DateTimeOffset? EnvioLiberadoEm { get; private set; }

    public bool EnvioLiberado => EnvioLiberadoEm is not null;

    /// <summary>Nome mostrado aos clientes no portal (RF-POR-09); nulo usa o <see cref="Nome"/>.</summary>
    public string? NomePortal { get; private set; }

    /// <summary>Dias em RESOLVIDA sem manifestação do cliente até o fechamento automático (RF-SOL-09).</summary>
    public int DiasFechamentoAutomatico { get; private set; }

    /// <summary>Chave do logotipo do portal no object storage (RF-POR-09); nulo = sem logotipo.</summary>
    public string? LogoChave { get; private set; }

    public const int TamanhoLogoChave = 512;

    /// <summary>
    /// Endereço que recebe as respostas dos e-mails ao contato (Reply-To; pedido de 16/10). O "De" continua sendo o
    /// endereço do Acordu; nulo = responder vai para o endereço sem caixa.
    /// </summary>
    public string? ResponderPara { get; private set; }

    /// <summary>Prazos sugeridos no cadastro de contrato; nulo = <see cref="PoliticaSla.Sugerida"/>.</summary>
    public PoliticaSla? SlaPadrao { get; private set; }

    /// <summary>A política que o cadastro de contrato sugere: a da empresa ou a do sistema.</summary>
    public PoliticaSla SlaSugerido => SlaPadrao ?? PoliticaSla.Sugerida;

    /// <summary>Situação comercial (RF-EMP-01, modelo §4.11.3). Muda só pelas transições de domínio.</summary>
    public SituacaoEmpresa Situacao { get; private set; }

    /// <summary>Instante em que o teste acaba; nulo em empresa que nunca esteve em teste (as anteriores ao F14).</summary>
    public DateTimeOffset? FimDoTeste { get; private set; }

    /// <summary>
    /// Início do só leitura atual (vencimento ou cancelamento); nulo enquanto a empresa grava. A data prevista do expurgo
    /// é calculada a partir dele (<see cref="ExpurgoPrevistoEm"/>), não gravada.
    /// </summary>
    public DateTimeOffset? SomenteLeituraDesde { get; private set; }

    /// <summary>Quando a empresa foi cancelada (pela Nebrys, ou pela migration do F14 nas anteriores a ele).</summary>
    public DateTimeOffset? CanceladaEm { get; private set; }

    /// <summary>
    /// Instante da última saída do só leitura (<see cref="Ativar"/> ou <see cref="Prorrogar"/> de uma VENCIDA, ou
    /// <see cref="Reativar"/>); nulo se a empresa nunca voltou do só leitura. As recorrências não geram as datas anteriores
    /// ao dia dele (F14-24): o que passou enquanto ela estava só leitura não é gerado de forma retroativa.
    /// </summary>
    public DateTimeOffset? GravavelDesde { get; private set; }

    /// <summary>
    /// Empresa de demonstração (RF-NEB-02; design do F14, D1 a1): ATIVA e fora do ciclo de situação, então nenhuma transição
    /// vale para ela (F14-69). Quem a cria e a marca é a área da Nebrys (F14s).
    /// </summary>
    public bool Demonstracao { get; private set; }

    /// <summary>
    /// CPF ou CNPJ de quem contratou o teste (RF-AUT-09, F14g), normalizado (<see cref="Common.Documento"/>). Fica só aqui,
    /// enquanto a empresa existe; o registro global de "um teste por documento" guarda só o HMAC dele (design do F14, D8).
    /// Nulo nas empresas anteriores ao F14g: a Application o exige no cadastro.
    /// </summary>
    public string? Documento { get; private set; }

    /// <summary>Tamanho máximo do documento normalizado (CNPJ, 14 caracteres).</summary>
    public const int TamanhoDocumento = 14;

    private List<string> _avisosSituacao = [];

    /// <summary>
    /// Marcas dos avisos de situação já enviados aos Donos (F14j, design do F14, D7), cada uma <c>tipo:referência</c>: o
    /// tipo com a antecedência (<c>fim-do-teste-7</c>, <c>vencimento</c>, <c>expurgo-7</c>) e o instante UTC de referência
    /// (o fim do teste, ou a data prevista do expurgo). A referência na marca é o que faz o aviso sair de novo depois de
    /// uma prorrogação (F14-101). Vazia nas empresas anteriores ao F14j.
    /// </summary>
    public IReadOnlyCollection<string> AvisosSituacao => _avisosSituacao;

    /// <summary>
    /// Código do endereço de entrada por e-mail (RF-SOL-11, F15-01; design do F15, D2 e D9): sorteado na criação
    /// (<see cref="CodigoDeEntrada"/>), único entre todas as empresas (índice único) e não derivado do nome. O endereço é
    /// <c>{código}@{domínio de entrada}</c>, com o domínio da configuração (a Application monta). Nulo só na empresa de
    /// demonstração (F15-96); as anteriores ao F15 ganham o código na migration.
    /// </summary>
    public string? CodigoEntrada { get; private set; }

    /// <summary>
    /// Recebimento por e-mail ligado (F15-03, F15-04): só com ele o e-mail ao endereço de entrada vira solicitação ou
    /// mensagem. Nasce desligado (pergunta 1 do F15); muda por <see cref="LigarRecebimentoEmail"/> e
    /// <see cref="DesligarRecebimentoEmail"/>.
    /// </summary>
    public bool RecebimentoEmail { get; private set; }

    public const string MensagemRecebimentoIndisponivel = "Recebimento por e-mail indisponível.";

    public const string MensagemDemonstracaoSemRecebimento = "A empresa de demonstração não recebe e-mails.";

    /// <summary>VENCIDA ou CANCELADA: a empresa lê e não grava (RF-EMP-02).</summary>
    public bool SomenteLeitura => Situacao.SomenteLeitura();

    /// <summary>
    /// A empresa nasce em TESTE, com o fim do teste no instante do cadastro mais a duração (RF-EMP-01; a duração vem da
    /// configuração, padrão de 30 dias pela decisão 1 do G2 do F14). O <paramref name="documento"/> (CPF ou CNPJ) é
    /// normalizado e conferido aqui; quem o exige é o cadastro (F14-56).
    /// </summary>
    public static Tenant Criar(
        Guid id, string nome, DateTimeOffset agora, Guid criadoPorUsuarioId, TimeSpan duracaoDoTeste, string? documento = null)
    {
        if (duracaoDoTeste <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duracaoDoTeste), duracaoDoTeste, "A duração do teste precisa ser positiva.");

        var criadoEm = agora.ToUniversalTime();
        return new(
            Ids.Obrigatorio(id, nameof(id)),
            Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome)),
            criadoEm,
            Ids.Obrigatorio(criadoPorUsuarioId, nameof(criadoPorUsuarioId)),
            criadoEm + duracaoDoTeste)
        {
            Documento = Common.Documento.Normalizar(documento, nameof(documento)),
            CodigoEntrada = CodigoDeEntrada.Sortear(),
        };
    }

    /// <summary>
    /// A empresa de demonstração (RF-NEB-02, F14-87; design do F14, D1 a1 e D13), criada pela área da Nebrys: ATIVA e fora
    /// do ciclo de situação (nenhuma transição vale, F14-69), sem fim de teste nem documento. O envio nasce liberado só para
    /// a tela não pedir a confirmação do e-mail de quem a criou (uma conta fictícia): nada sai dela, porque o bloqueio do
    /// envio olha a marca <see cref="Demonstracao"/> antes da liberação (F14-89).
    /// </summary>
    public static Tenant CriarDemonstracao(Guid id, string nome, DateTimeOffset agora, Guid criadoPorUsuarioId)
    {
        var criadoEm = agora.ToUniversalTime();
        return new(
            Ids.Obrigatorio(id, nameof(id)),
            Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome)),
            criadoEm,
            Ids.Obrigatorio(criadoPorUsuarioId, nameof(criadoPorUsuarioId)),
            criadoEm)
        {
            Situacao = SituacaoEmpresa.Ativa,
            FimDoTeste = null,
            Demonstracao = true,
            EnvioLiberadoEm = criadoEm,
        };
    }

    /// <summary>
    /// Volta a configuração da empresa de demonstração ao estado de quando foi criada (F14-88): o nome dos dados fictícios,
    /// sem nome de portal, endereço de resposta, logotipo nem SLA próprio, e o fechamento automático padrão. A situação, o
    /// criador e a data de criação não mudam. Fora da demonstração → <see cref="TransicaoInvalidaException"/>.
    /// </summary>
    public void RestaurarDemonstracao(string nome)
    {
        if (!Demonstracao)
            throw new TransicaoInvalidaException("Só a empresa de demonstração é restaurada.");

        Nome = Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome));
        NomePortal = null;
        DiasFechamentoAutomatico = DiasFechamentoPadrao;
        LogoChave = null;
        ResponderPara = null;
        SlaPadrao = null;
        RecebimentoEmail = false;
    }

    /// <summary>
    /// TESTE → VENCIDA (job diário, F14-03), só com o fim do teste já alcançado em <paramref name="agora"/>. O só leitura
    /// começa no instante do vencimento, que fica gravado como a data do vencimento. O fim do teste fica como estava.
    /// </summary>
    public void Vencer(DateTimeOffset agora)
    {
        if (Situacao != SituacaoEmpresa.Teste)
            throw new TransicaoInvalidaException("Só uma empresa em teste vence.");
        if (FimDoTeste is not { } fim || agora < fim)
            throw new TransicaoInvalidaException("O teste desta empresa ainda não acabou.");

        Situacao = SituacaoEmpresa.Vencida;
        SomenteLeituraDesde = agora.ToUniversalTime();
    }

    /// <summary>
    /// TESTE ou VENCIDA → ATIVA (área da Nebrys, F14-65): sem fim de teste e fora do só leitura. Vinda da VENCIDA, grava
    /// <see cref="GravavelDesde"/> com <paramref name="agora"/> (as recorrências não geram as datas do só leitura, F14-24).
    /// </summary>
    public void Ativar(DateTimeOffset agora)
    {
        ExigirForaDaDemonstracao();
        if (Situacao is not (SituacaoEmpresa.Teste or SituacaoEmpresa.Vencida))
        {
            throw new TransicaoInvalidaException(Situacao == SituacaoEmpresa.Ativa
                ? "A empresa já está ativa."
                : "Uma empresa cancelada volta pela reativação, não pela ativação.");
        }

        SairDoSomenteLeitura(agora);
        Situacao = SituacaoEmpresa.Ativa;
        FimDoTeste = null;
    }

    /// <summary>
    /// TESTE ou VENCIDA → TESTE com o novo fim (F14-66). Prorrogar até o dia <paramref name="ultimoDia"/> grava o início do
    /// dia seguinte no <paramref name="fuso"/> da empresa: o teste vale o último dia inteiro (design do F14, §1.2). O novo
    /// fim precisa estar à frente de <paramref name="agora"/>: um dia já passado é recusado (F14-69). Vinda da VENCIDA, sai
    /// do só leitura e grava <see cref="GravavelDesde"/> (F14-24).
    /// </summary>
    public void Prorrogar(DateOnly ultimoDia, TimeZoneInfo fuso, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(fuso);
        ExigirForaDaDemonstracao();
        if (Situacao is not (SituacaoEmpresa.Teste or SituacaoEmpresa.Vencida))
            throw new TransicaoInvalidaException("Só se prorroga o teste de uma empresa em teste ou vencida.");

        var novoFim = InicioDoDia(ultimoDia.AddDays(1), fuso);
        if (novoFim <= agora)
            throw new TransicaoInvalidaException("O teste só pode ser prorrogado até hoje ou uma data futura.");

        SairDoSomenteLeitura(agora);
        Situacao = SituacaoEmpresa.Teste;
        FimDoTeste = novoFim;
    }

    /// <summary>
    /// TESTE, ATIVA ou VENCIDA → CANCELADA (F14-67): só leitura, com <see cref="CanceladaEm"/> = <paramref name="agora"/>.
    /// Vinda da VENCIDA, o só leitura continua contando do vencimento (<see cref="SomenteLeituraDesde"/> fica, e com ele a
    /// data do expurgo); das demais, começa agora.
    /// </summary>
    public void Cancelar(DateTimeOffset agora)
    {
        ExigirForaDaDemonstracao();
        if (Situacao == SituacaoEmpresa.Cancelada)
            throw new TransicaoInvalidaException("A empresa já está cancelada.");

        var instante = agora.ToUniversalTime();
        if (Situacao != SituacaoEmpresa.Vencida)
            SomenteLeituraDesde = instante;
        Situacao = SituacaoEmpresa.Cancelada;
        CanceladaEm = instante;
    }

    /// <summary>
    /// CANCELADA → ATIVA (F14-68): sai do só leitura, sem as datas do cancelamento nem fim de teste, e grava
    /// <see cref="GravavelDesde"/>. A empresa já expurgada não existe mais: quem chama responde 404 (F14-106).
    /// </summary>
    public void Reativar(DateTimeOffset agora)
    {
        ExigirForaDaDemonstracao();
        if (Situacao != SituacaoEmpresa.Cancelada)
            throw new TransicaoInvalidaException("Só uma empresa cancelada é reativada.");

        SairDoSomenteLeitura(agora);
        Situacao = SituacaoEmpresa.Ativa;
        FimDoTeste = null;
        CanceladaEm = null;
    }

    /// <summary>Volta a gravar: o só leitura atual acaba e, se havia um, a volta fica em <see cref="GravavelDesde"/>.</summary>
    private void SairDoSomenteLeitura(DateTimeOffset agora)
    {
        if (!SomenteLeitura)
            return;
        SomenteLeituraDesde = null;
        GravavelDesde = agora.ToUniversalTime();
    }

    private void ExigirForaDaDemonstracao()
    {
        if (Demonstracao)
            throw new TransicaoInvalidaException("A empresa de demonstração fica fora do ciclo de situação.");
    }

    /// <summary>
    /// Primeiro instante do <paramref name="dia"/> no fuso, em UTC. Num fuso em que a meia-noite não existe (início do
    /// horário de verão), o primeiro instante válido do dia.
    /// </summary>
    private static DateTimeOffset InicioDoDia(DateOnly dia, TimeZoneInfo fuso)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (fuso.IsInvalidTime(local))
            local = local.AddMinutes(30);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, fuso), TimeSpan.Zero);
    }

    /// <summary>
    /// Data prevista do expurgo (RF-EMP-03): início do só leitura mais o prazo da configuração; nulo quando a empresa grava.
    /// </summary>
    public DateTimeOffset? ExpurgoPrevistoEm(TimeSpan prazoAteExpurgo) =>
        SomenteLeitura && SomenteLeituraDesde is { } desde ? desde + prazoAteExpurgo : null;

    /// <summary>
    /// A data do expurgo já chegou em <paramref name="agora"/> (RF-EMP-03, F14-29, F14-30): só leitura há o prazo ou mais.
    /// TESTE e ATIVA (inclusive a demonstração, que é ATIVA) nunca (F14-31). O passo <c>expurgo-empresas</c> do diário
    /// filtra pela mesma regra no banco.
    /// </summary>
    public bool ExpurgoDevido(DateTimeOffset agora, TimeSpan prazoAteExpurgo) =>
        ExpurgoPrevistoEm(prazoAteExpurgo) is { } data && data <= agora;

    /// <summary>
    /// O aviso de situação devido em <paramref name="agora"/> (RF-EMP-04, F14-39 a F14-42, F14-101 a F14-103, F14-113), ou
    /// nulo. O momento de cada aviso é um <b>dia civil no fuso da empresa</b> (<paramref name="fuso"/>), a mesma conta da
    /// faixa da SPA: o diário roda uma vez por dia, e o aviso de "N dias antes" é devido no primeiro diário do dia que fica
    /// a N dias do prazo, qualquer que seja a hora do cadastro. Um aviso por empresa e por diário, o mais próximo do prazo, e
    /// só com o instante de referência ainda à frente:
    /// <list type="bullet">
    /// <item>TESTE com o fim do teste à frente: devido o aviso de N dias quando hoje ≥ último dia do teste − N
    /// (<see cref="UltimoDiaDoTeste"/>). Dos devidos sai o mais próximo do fim (o menor N), com as marcas dos maiores junto,
    /// para eles não saírem depois (F14-103); o atrasado sai uma vez só (F14-102). Se o mais próximo já tem marca, nada sai.</item>
    /// <item>VENCIDA ou CANCELADA com a data do expurgo à frente (F14-113: já alcançada, nenhum aviso): na VENCIDA sem a marca
    /// do vencimento, sai o do vencimento (F14-41), com a marca do de antes do expurgo junto se ele também já for devido,
    /// porque o e-mail do vencimento já traz a data; senão, o de antes do expurgo, quando hoje ≥ dia do expurgo − N (F14-42).</item>
    /// </list>
    /// ATIVA (e a demonstração, que é ATIVA) nunca. Não muda a empresa: quem envia grava as marcas
    /// (<see cref="RegistrarAviso"/>).
    /// </summary>
    public AvisoSituacao? AvisoDeSituacaoDevido(DateTimeOffset agora, TimeZoneInfo fuso, PrazosAvisosSituacao prazos)
    {
        ArgumentNullException.ThrowIfNull(fuso);
        ArgumentNullException.ThrowIfNull(prazos);
        var hoje = DiaCivil(agora, fuso);

        if (Situacao == SituacaoEmpresa.Teste)
        {
            if (FimDoTeste is not { } fim || agora >= fim)
                return null;

            var ultimoDia = UltimoDiaDoTeste(fim, fuso);
            var devidos = prazos.DiasAntesDoFim.Where(dias => hoje >= ultimoDia.AddDays(-dias)).ToList();
            if (devidos.Count == 0)
                return null;

            var marcas = devidos.Select(dias => MarcaAviso(TipoFimDoTeste + Dias(dias), fim)).ToList();
            return _avisosSituacao.Contains(marcas[0])
                ? null
                : new AvisoSituacao(TipoAvisoSituacao.AntesDoFimDoTeste, fim, devidos[0], marcas);
        }

        if (ExpurgoPrevistoEm(prazos.PrazoAteExpurgo) is not { } expurgo || agora >= expurgo)
            return null;

        var antesDoExpurgo = prazos.DiasAntesDoExpurgo;
        var marcaExpurgo = MarcaAviso(TipoExpurgo + Dias(antesDoExpurgo), expurgo);
        var expurgoDevido = hoje >= DiaCivil(expurgo, fuso).AddDays(-antesDoExpurgo);
        var marcaVencimento = MarcaAviso(TipoVencimento, expurgo);
        if (Situacao == SituacaoEmpresa.Vencida && !_avisosSituacao.Contains(marcaVencimento))
        {
            return new AvisoSituacao(
                TipoAvisoSituacao.Vencimento, expurgo, null, expurgoDevido ? [marcaVencimento, marcaExpurgo] : [marcaVencimento]);
        }

        return expurgoDevido && !_avisosSituacao.Contains(marcaExpurgo)
            ? new AvisoSituacao(TipoAvisoSituacao.AntesDoExpurgo, expurgo, antesDoExpurgo, [marcaExpurgo])
            : null;
    }

    /// <summary>
    /// Grava as marcas do aviso (design D7: no mesmo commit em que se decide enviar). As marcas de outra referência (um fim
    /// do teste ou uma data de expurgo que já não vale) saem: só as do ciclo atual importam.
    /// </summary>
    public void RegistrarAviso(AvisoSituacao aviso)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        var referencia = SufixoDaReferencia(aviso.Referencia);
        _avisosSituacao =
        [
            .. _avisosSituacao
                .Where(m => m.EndsWith(referencia, StringComparison.Ordinal))
                .Union(aviso.Marcas, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Último dia do teste no fuso: o dia do instante logo antes do fim (a prorrogação até D grava o início de D+1, design
    /// do F14 §1.2). É a conta da faixa da SPA (<c>ultimoDiaDoTeste</c>) e do texto dos avisos.
    /// </summary>
    public static DateOnly UltimoDiaDoTeste(DateTimeOffset fimDoTeste, TimeZoneInfo fuso) =>
        DiaCivil(fimDoTeste - TimeSpan.FromTicks(1), fuso);

    /// <summary>Dia civil de um instante no fuso (o relógio é de quem chama).</summary>
    public static DateOnly DiaCivil(DateTimeOffset instante, TimeZoneInfo fuso)
    {
        ArgumentNullException.ThrowIfNull(fuso);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, fuso).DateTime);
    }

    private const string TipoFimDoTeste = "fim-do-teste-";
    private const string TipoVencimento = "vencimento";
    private const string TipoExpurgo = "expurgo-";

    private static string Dias(int dias) => dias.ToString(CultureInfo.InvariantCulture);

    private static string MarcaAviso(string tipo, DateTimeOffset referencia) => tipo + SufixoDaReferencia(referencia);

    private static string SufixoDaReferencia(DateTimeOffset referencia) =>
        ":" + referencia.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public void Renomear(string nome) => Nome = Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome));

    public void ConfigurarPortal(string? nomePortal, int diasFechamentoAutomatico)
    {
        if (diasFechamentoAutomatico is < DiasFechamentoMinimo or > DiasFechamentoMaximo)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diasFechamentoAutomatico),
                diasFechamentoAutomatico,
                $"O fechamento automático vai de {DiasFechamentoMinimo} a {DiasFechamentoMaximo} dias.");
        }

        NomePortal = Texto.Opcional(nomePortal, LimitesTexto.Nome, nameof(nomePortal));
        DiasFechamentoAutomatico = diasFechamentoAutomatico;
    }

    /// <summary>Nome da empresa, endereço de resposta (vazio = nenhum) e SLA sugerido nos contratos novos.</summary>
    public void ConfigurarEmpresa(string nome, string? responderPara, PoliticaSla slaPadrao)
    {
        ArgumentNullException.ThrowIfNull(slaPadrao);
        var novoNome = Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome));
        var endereco = string.IsNullOrWhiteSpace(responderPara) ? null : Email.Normalizar(responderPara, nameof(responderPara));
        Nome = novoNome;
        ResponderPara = endereco;
        SlaPadrao = slaPadrao;
    }

    /// <summary>
    /// Liga o recebimento por e-mail (F15-03). Sem endereço de entrada (a demonstração, F15-96) →
    /// <see cref="TransicaoInvalidaException"/>. Ligar o que já está ligado não muda nada. A falta do domínio de entrada na
    /// configuração (F15-94) é conferida antes, pela Application.
    /// </summary>
    public void LigarRecebimentoEmail()
    {
        ExigirEnderecoDeEntrada();
        RecebimentoEmail = true;
    }

    /// <summary>Desliga o recebimento por e-mail (F15-04); na demonstração → <see cref="TransicaoInvalidaException"/>.</summary>
    public void DesligarRecebimentoEmail()
    {
        if (Demonstracao)
            throw new TransicaoInvalidaException(MensagemDemonstracaoSemRecebimento);
        RecebimentoEmail = false;
    }

    /// <summary>
    /// Troca o código de entrada por outro sorteado: só na criação da empresa, quando o índice único recusa o primeiro
    /// (colisão, design do F15, D2). Sem código (a demonstração) → <see cref="TransicaoInvalidaException"/>.
    /// </summary>
    public void SortearOutroCodigoEntrada()
    {
        ExigirEnderecoDeEntrada();
        string novo;
        do
        {
            novo = CodigoDeEntrada.Sortear();
        }
        while (string.Equals(novo, CodigoEntrada, StringComparison.Ordinal));

        CodigoEntrada = novo;
    }

    private void ExigirEnderecoDeEntrada()
    {
        if (Demonstracao)
            throw new TransicaoInvalidaException(MensagemDemonstracaoSemRecebimento);
        if (CodigoEntrada is null)
            throw new TransicaoInvalidaException(MensagemRecebimentoIndisponivel);
    }

    /// <summary>Troca (ou remove, com nulo) o logotipo; o arquivo já está no storage.</summary>
    public void DefinirLogo(string? chave) => LogoChave = Texto.Opcional(chave, TamanhoLogoChave, nameof(chave));
}
