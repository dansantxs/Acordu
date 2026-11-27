using Acordu.Application.Abstractions;
using Acordu.Application.Empresas;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Persistence.Cadastros;

/// <summary>
/// Passo <c>avisos-situacao</c> do job diário (RF-EMP-04, F14j; design do F14, D7): para cada empresa em TESTE, VENCIDA
/// ou CANCELADA, o aviso devido no instante do diário (<see cref="Tenant.AvisoDeSituacaoDevido"/>) vai por e-mail aos
/// Donos. Antes da liberação do envio (RF-AUT-06), só ao criador (F14-43), a regra do <c>EnvioATerceiros</c>.
/// <para>
/// Laço próprio (convenções §1): os avisos de vencimento e de expurgo são para empresas só leitura, que o
/// <c>ExecutorPorTenant</c> não percorre. Por isso a marca é gravada fora do change tracker (<c>ExecuteUpdate</c>, fora
/// da guarda de só leitura, ADR 0015 item 3): é rastro do aviso, não dado da empresa, e a condição pela versão
/// (<c>xmin</c>) garante que a decisão foi tomada sobre a linha que ainda está no banco. A marca entra antes do envio, num
/// commit só; o e-mail sai logo depois, fora da outbox (que descarta as mensagens de empresa só leitura), como os da conta.
/// A falha do envio é logada, conta em <c>falhas</c> e devolve as marcas de antes, para o próximo diário tentar.
/// </para>
/// </summary>
public sealed partial class AvisosDeSituacao
{
    private static readonly string[] Filtros = [FiltrosGlobais.Tenant, FiltrosGlobais.Cliente];

    private readonly IServiceScopeFactory _escopos;
    private readonly OpcoesEmpresas _opcoes;
    private readonly OpcoesFornecedor _fornecedor;
    private readonly TimeProvider _relogio;
    private readonly ILogger<AvisosDeSituacao> _logger;

    public AvisosDeSituacao(
        IServiceScopeFactory escopos,
        OpcoesEmpresas opcoes,
        OpcoesFornecedor fornecedor,
        TimeProvider relogio,
        ILogger<AvisosDeSituacao> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(fornecedor);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);
        _escopos = escopos;
        _opcoes = opcoes;
        _fornecedor = fornecedor;
        _relogio = relogio;
        _logger = logger;
    }

    /// <summary>Quantas empresas foram avisadas e em quantas o aviso falhou.</summary>
    public async Task<(int Avisadas, int Falhas)> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var agora = _relogio.GetUtcNow();
        var prazos = _opcoes.PrazosDosAvisos;
        List<Guid> candidatas;
        await using (var escopo = _escopos.CreateAsyncScope())
        {
            candidatas = await Candidatas(escopo.ServiceProvider.GetRequiredService<AcorduDbContext>(), agora, prazos)
                .OrderBy(t => t.Id)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
        }

        var avisadas = 0;
        var falhas = 0;
        foreach (var tenantId in candidatas)
        {
            try
            {
                switch (await AvisarAsync(tenantId, agora, prazos, cancellationToken))
                {
                    case Resultado.Avisada:
                        avisadas++;
                        break;
                    case Resultado.Falhou:
                        falhas++;
                        break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                falhas++;
                AvisoFalhou(_logger, ex, tenantId);
            }
        }

        return (avisadas, falhas);
    }

    private async Task<Resultado> AvisarAsync(
        Guid tenantId, DateTimeOffset agora, PrazosAvisosSituacao prazos, CancellationToken cancellationToken)
    {
        await using var escopo = _escopos.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AcorduDbContext>();
        var tenant = await db.Tenants.IgnoreQueryFilters(Filtros).SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
            return Resultado.Nada;

        // O momento do aviso é um dia civil no fuso da empresa, como a faixa da SPA.
        var fuso = await FusoAsync(db, tenantId, cancellationToken);
        if (tenant.AvisoDeSituacaoDevido(agora, fuso, prazos) is not { } aviso)
            return Resultado.Nada;

        // Sem destinatário (nenhum Dono com e-mail; antes da liberação, o criador sem papel Dono), nada é marcado: o aviso
        // fica devido e sai no próximo diário para quem estiver lá.
        var donos = await DestinatariosAsync(db, tenant, cancellationToken);
        if (donos.Count == 0)
        {
            SemDestinatario(_logger, tenantId, aviso.Tipo);
            return Resultado.Nada;
        }

        var versao = db.Entry(tenant).Property<uint>(ConfiguracaoRaiz.Versao).CurrentValue;
        var anteriores = tenant.AvisosSituacao.ToList();
        tenant.RegistrarAviso(aviso);
        var marcas = tenant.AvisosSituacao.ToList();
        // A entidade não vai ao SaveChanges (a guarda recusaria o Tenant só leitura): a marca sai pelo UPDATE abaixo.
        db.ChangeTracker.Clear();

        // Mesma linha que decidiu o aviso: uma ação concorrente (prorrogar, ativar) muda a versão, e o próximo diário decide
        // de novo sobre o estado novo.
        var gravadas = await db.Tenants.IgnoreQueryFilters(Filtros)
            .Where(t => t.Id == tenantId && EF.Property<uint>(t, ConfiguracaoRaiz.Versao) == versao)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.AvisosSituacao, marcas), cancellationToken);
        if (gravadas != 1)
            return Resultado.Nada;

        var email = escopo.ServiceProvider.GetRequiredService<IEnviadorEmail>();
        var falhou = false;
        foreach (var dono in donos)
        {
            try
            {
                await email.EnviarAsync(
                    MensagensDeSituacao.Montar(aviso, dono.Email, dono.Nome, tenant.Nome, fuso, agora, _fornecedor), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                falhou = true;
                EnvioFalhou(_logger, ex, tenantId, aviso.Tipo);
            }
        }

        if (falhou)
        {
            // Sem a marca, o próximo diário tenta de novo (design D7); quem já recebeu pode receber outra vez. Aqui não se
            // confere o xmin: o UPDATE da marca acabou de mudar a versão (a lida antes já não vale), e só este passo grava
            // avisos_situacao (um diário por vez). Se uma ação concorrente mudou a situação ou as datas entretanto, as marcas
            // de antes continuam corretas para o estado novo: o próximo diário decide sobre ele, e marca a menos só pode
            // repetir um aviso, nunca perdê-lo.
            await db.Tenants.IgnoreQueryFilters(Filtros)
                .Where(t => t.Id == tenantId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.AvisosSituacao, anteriores), CancellationToken.None);
            return Resultado.Falhou;
        }

        AvisoEnviado(_logger, tenantId, aviso.Tipo, donos.Count);
        return Resultado.Avisada;
    }

    /// <summary>
    /// Os Donos (papel padrão Dono) com e-mail; antes da liberação do envio, só o criador (F14-43; RF-AUT-06): a mesma regra
    /// do <c>EnvioATerceiros.PodeAvisarAsync</c>, que não serve aqui porque recusa tudo na empresa só leitura.
    /// </summary>
    private static async Task<List<Destinatario>> DestinatariosAsync(
        AcorduDbContext db, Tenant tenant, CancellationToken cancellationToken)
    {
        var donos = await (
            from v in db.Vinculos.IgnoreQueryFilters(Filtros)
            join p in db.Papeis.IgnoreQueryFilters(Filtros) on v.PapelId equals p.Id
            join u in db.Users on v.UsuarioId equals u.Id
            where v.TenantId == tenant.Id && p.Padrao && p.Nome == Papel.NomeDono && u.Email != null
            orderby u.Id
            select new Destinatario(u.Id, u.Email!, u.Nome))
            .ToListAsync(cancellationToken);
        return tenant.EnvioLiberado ? donos : donos.FindAll(d => d.UsuarioId == tenant.CriadoPorUsuarioId);
    }

    private static async Task<TimeZoneInfo> FusoAsync(AcorduDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var calendario = await db.Calendarios.IgnoreQueryFilters(Filtros)
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);
        return calendario?.Fuso ?? TimeZoneInfo.FindSystemTimeZoneById(CalendarioUtil.FusoPadrao);
    }

    /// <summary>
    /// Pré-filtro no banco (a decisão é do domínio): TESTE com o fim do teste à frente e dentro da maior antecedência (com
    /// folga para o fuso), ou só leitura com a data do expurgo ainda à frente (F14-113). ATIVA nunca, e a demonstração
    /// nunca (RF-NEB-02: ela é ATIVA, e a marca fica no filtro também).
    /// </summary>
    private static IQueryable<Tenant> Candidatas(AcorduDbContext db, DateTimeOffset agora, PrazosAvisosSituacao prazos)
    {
        // Folga de 2 dias: o momento é um dia civil no fuso da empresa, que pode ficar a mais de N × 24 h do fim.
        var limiteDoFim = agora + TimeSpan.FromDays(prazos.DiasAntesDoFim[^1] + 2);
        var soLeituraDepoisDe = agora - prazos.PrazoAteExpurgo;
        return db.Tenants.IgnoreQueryFilters(Filtros)
            .Where(t => !t.Demonstracao)
            .Where(t => (t.Situacao == SituacaoEmpresa.Teste && t.FimDoTeste > agora && t.FimDoTeste <= limiteDoFim)
                || (SituacaoEmpresaExtensions.SituacoesSomenteLeitura.Contains(t.Situacao)
                    && t.SomenteLeituraDesde != null
                    && t.SomenteLeituraDesde > soLeituraDepoisDe));
    }

    private enum Resultado
    {
        Nada = 1,
        Avisada = 2,
        Falhou = 3,
    }

    private sealed record Destinatario(Guid UsuarioId, string Email, string Nome);

    [LoggerMessage(EventId = 209, Level = LogLevel.Information, Message = "Aviso de situação {Tipo} da empresa {TenantId} enviado a {Donos} Dono(s).")]
    private static partial void AvisoEnviado(ILogger logger, Guid tenantId, TipoAvisoSituacao tipo, int donos);

    [LoggerMessage(EventId = 210, Level = LogLevel.Warning, Message = "Falha ao enviar o aviso de situação {Tipo} da empresa {TenantId}.")]
    private static partial void EnvioFalhou(ILogger logger, Exception erro, Guid tenantId, TipoAvisoSituacao tipo);

    [LoggerMessage(EventId = 211, Level = LogLevel.Error, Message = "Aviso de situação da empresa {TenantId} falhou.")]
    private static partial void AvisoFalhou(ILogger logger, Exception erro, Guid tenantId);

    [LoggerMessage(EventId = 212, Level = LogLevel.Warning, Message = "Aviso de situação {Tipo} da empresa {TenantId} sem destinatário; fica para o próximo diário.")]
    private static partial void SemDestinatario(ILogger logger, Guid tenantId, TipoAvisoSituacao tipo);
}
