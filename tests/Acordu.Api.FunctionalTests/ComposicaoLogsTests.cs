using Acordu.Api.Operacao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F13, F13a: o formato do log escolhido por variável de ambiente (F13-01, F13-03).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ComposicaoLogsTests(ApiFixture api)
{
    [Fact]
    public async Task AddLogsAcordu_ComJson_ConsoleUsaOFormatadorComAVersao() // F13-01
    {
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?>
        {
            ["Logs:Formato"] = "json",
            ["App:Versao"] = "abc1234",
        });

        app.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue.FormatterName
            .Should().Be(FormatadorLogJson.Nome);
        app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Should().ContainSingle();
    }

    [Fact]
    public async Task AddLogsAcordu_SemFormato_ConsoleDeTexto() // F13-03
    {
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?>());

        app.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue.FormatterName
            .Should().NotBe(FormatadorLogJson.Nome);
        app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Should().BeEmpty();
    }

    [Fact]
    public async Task AddLogsAcordu_FormatoDesconhecido_NaoSobe()
    {
        var subir = () => api.NovaApiAsync(new Dictionary<string, string?> { ["Logs:Formato"] = "xml" });

        (await subir.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*Logs__Formato*");
    }
}
