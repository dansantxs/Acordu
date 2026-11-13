using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.Common;
using Acordu.Domain.Entities;

namespace Acordu.Application.UnitTests.Common;

/// <summary>
/// RF-AUT-06 (spec do F9, decisões 6 e 8): aviso automático sai de empresa liberada no instante do evento e, antes
/// disso, só para o criador; o bloqueado é registrado.
/// </summary>
public sealed class EnvioATerceirosTests
{
    private static readonly DateTimeOffset Criacao = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Criador = Guid.NewGuid();

    [Fact]
    public async Task PodeAvisar_EmpresaLiberada_Permite()
    {
        var (envio, bloqueados) = Montar(Liberada(Criacao), ocorridoEm: Criacao.AddHours(1));

        (await envio.PodeAvisarAsync("aviso-recebida", null)).Should().BeTrue();
        bloqueados.Categorias.Should().BeEmpty();
    }

    [Fact]
    public async Task PodeAvisar_NaoLiberadaParaOCriador_Permite()
    {
        var (envio, _) = Montar(Tenant.Criar(Guid.NewGuid(), "Estúdio", Criacao, Criador, TimeSpan.FromDays(30)), ocorridoEm: null);

        (await envio.PodeAvisarAsync("aviso-consumo", Criador)).Should().BeTrue();
    }

    [Fact]
    public async Task PodeAvisar_NaoLiberadaParaOutraPessoaOuContato_DescartaERegistra()
    {
        var (envio, bloqueados) = Montar(Tenant.Criar(Guid.NewGuid(), "Estúdio", Criacao, Criador, TimeSpan.FromDays(30)), ocorridoEm: null);

        (await envio.PodeAvisarAsync("aviso-consumo", Guid.NewGuid())).Should().BeFalse();
        (await envio.PodeAvisarAsync("aviso-recebida", null)).Should().BeFalse();
        bloqueados.Categorias.Should().Equal("aviso-consumo", "aviso-recebida");
    }

    [Fact]
    public async Task PodeAvisar_EventoAnteriorALiberacao_Descarta()
    {
        // Nova tentativa da outbox entregue depois da confirmação: o aviso é de antes dela.
        var (envio, bloqueados) = Montar(Liberada(Criacao.AddHours(2)), ocorridoEm: Criacao.AddHours(1));

        (await envio.PodeAvisarAsync("aviso-recebida", null)).Should().BeFalse();
        bloqueados.Categorias.Should().ContainSingle();
    }

    [Fact]
    public async Task PodeAvisar_EmpresaAnteriorAoF9SemCriador_SoComLiberacao()
    {
        var antiga = Tenant.Criar(Guid.NewGuid(), "Antiga", Criacao, Criador, TimeSpan.FromDays(30));
        typeof(Tenant).GetProperty(nameof(Tenant.CriadoPorUsuarioId))!.SetValue(antiga, null);
        var (envio, _) = Montar(antiga, ocorridoEm: null);

        (await envio.PodeAvisarAsync("aviso-consumo", Criador)).Should().BeFalse();
    }

    [Fact]
    public async Task GarantirLiberado_NaoLiberada_Recusa()
    {
        var (envio, _) = Montar(Tenant.Criar(Guid.NewGuid(), "Estúdio", Criacao, Criador, TimeSpan.FromDays(30)), ocorridoEm: null);

        await envio.Invoking(e => e.GarantirLiberadoAsync()).Should().ThrowAsync<EmailNaoConfirmadoException>();
    }

    /// <summary>A liberação é gravada pela Infrastructure (não há método no domínio); aqui, direto na propriedade.</summary>
    private static Tenant Liberada(DateTimeOffset em)
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Criacao, Criador, TimeSpan.FromDays(30));
        typeof(Tenant).GetProperty(nameof(Tenant.EnvioLiberadoEm))!.SetValue(tenant, (DateTimeOffset?)em);
        return tenant;
    }

    private static (EnvioATerceiros Envio, Bloqueados Bloqueados) Montar(Tenant tenant, DateTimeOffset? ocorridoEm)
    {
        var bloqueados = new Bloqueados();
        return (new EnvioATerceiros(new TenantFixo(tenant), bloqueados, new Origem(ocorridoEm)), bloqueados);
    }

    private sealed class TenantFixo(Tenant tenant) : ITenantAtual
    {
        public Task<Tenant> ObterAsync(CancellationToken cancellationToken = default) => Task.FromResult(tenant);
    }

    private sealed class Bloqueados : IEnviosBloqueados
    {
        public List<string> Categorias { get; } = [];

        public void Registrar(string categoria) => Categorias.Add(categoria);
    }

    private sealed class Origem(DateTimeOffset? ocorridoEm) : IOrigemDoEvento
    {
        public Guid? AutorId => null;

        public Guid? MensagemId => null;

        public DateTimeOffset? OcorridoEm => ocorridoEm;
    }
}
