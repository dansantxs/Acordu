using System.Text.RegularExpressions;
using Acordu.Application.Abstractions;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>E-mails "enviados" durante os testes, em memória. Cada teste usa e-mails únicos e filtra pelo destinatário.</summary>
internal sealed partial class CaixaDeEmails : IEnviadorEmail
{
    private readonly Lock _trava = new();
    private readonly List<MensagemEmail> _mensagens = [];

    public Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancellationToken = default)
    {
        lock (_trava)
            _mensagens.Add(mensagem);
        return Task.CompletedTask;
    }

    public IReadOnlyList<MensagemEmail> Para(string email)
    {
        lock (_trava)
            return [.. _mensagens.Where(m => m.Para == email)];
    }

    /// <summary>Token do último convite do portal enviado ao e-mail.</summary>
    public string TokenDoConvite(string email)
    {
        var mensagem = Para(email).Last(m => m.Categoria == "convite-portal");
        return LinkDoConvite().Match(mensagem.Texto).Groups[1].Value;
    }

    /// <summary>Token do último convite para a equipe enviado ao e-mail.</summary>
    public string TokenDoConviteEquipe(string email)
    {
        var mensagem = Para(email).Last(m => m.Categoria == "convite-equipe");
        return LinkDoConviteEquipe().Match(mensagem.Texto).Groups[1].Value;
    }

    [GeneratedRegex(@"/portal/convite/([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkDoConvite();

    [GeneratedRegex(@"/convite/([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkDoConviteEquipe();
}
