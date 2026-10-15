using Acordu.Application.Abstractions.Eventos;

namespace Acordu.Infrastructure.Persistence.Outbox;

/// <summary>Autor do evento em entrega, fixado pelo <see cref="ProcessadorOutbox"/> no escopo de cada mensagem.</summary>
public sealed class OrigemDoEvento : IOrigemDoEvento
{
    public Guid? AutorId { get; private set; }

    public Guid? MensagemId { get; private set; }

    public DateTimeOffset? OcorridoEm { get; private set; }

    public void Definir(Guid? autorId, Guid mensagemId, DateTimeOffset ocorridoEm)
    {
        AutorId = autorId;
        MensagemId = mensagemId;
        OcorridoEm = ocorridoEm;
    }
}
