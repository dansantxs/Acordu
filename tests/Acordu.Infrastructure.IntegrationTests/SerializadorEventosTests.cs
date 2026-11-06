using System.Text.Json.Nodes;
using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Infrastructure.Persistence.Outbox;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// Outbox gravada antes de um campo novo do evento (F11a: <c>AvisoNaMensagem</c>, <c>PedeRetorno</c>) continua
/// desserializando, com o padrão do parâmetro.
/// </summary>
public sealed class SerializadorEventosTests
{
    private static readonly DateTimeOffset Em = new(2026, 11, 6, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StatusAlteradoSemAvisoNaMensagem_DesserializaComFalse()
    {
        var evento = new SolicitacaoStatusAlterado(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), StatusSolicitacao.EmAndamento, StatusSolicitacao.AguardandoCliente,
            StatusExterno.EmAndamento, StatusExterno.AguardandoVoce, Em, AvisoNaMensagem: true);

        var lido = SerializadorEventos.Desserializar(SerializadorEventos.Tipo(evento), SemCampo(evento, "avisoNaMensagem"));

        lido.Should().Be(evento with { AvisoNaMensagem = false });
    }

    [Fact]
    public void InteracaoSemPedeRetorno_DesserializaComFalse()
    {
        var evento = new InteracaoRegistrada(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false, Visibilidade.Externa, Em,
            PedeRetorno: true);

        var lido = SerializadorEventos.Desserializar(SerializadorEventos.Tipo(evento), SemCampo(evento, "pedeRetorno"));

        lido.Should().Be(evento with { PedeRetorno = false });
    }

    /// <summary>O JSON como uma versão anterior gravaria: sem o campo (com qualquer caixa no nome).</summary>
    private static string SemCampo(IEventoDominio evento, string campo)
    {
        var json = JsonNode.Parse(SerializadorEventos.Serializar(evento))!.AsObject();
        var chave = json.Select(p => p.Key).Single(k => string.Equals(k, campo, StringComparison.OrdinalIgnoreCase));
        json.Remove(chave);
        return json.ToJsonString();
    }
}
