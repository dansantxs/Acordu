using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Segurança §5.5 (F14k, achado do revisor): toda recusa da sessão de operação confere uma senha pelo hasher do
/// Identity, para o tempo de resposta não separar e-mail sem conta, senha vazia e conta bloqueada da senha errada.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class SessaoOperacaoTempoTests(ApiFixture api)
{
    [Fact]
    public async Task AbrirSessao_TodaRecusa_ConfereUmHashDeSenha()
    {
        var bloqueado = $"operacao-{Guid.NewGuid():N}@nebrys.test";
        var contador = new HasherQueConta();
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Nebrys:Operadores"] = $"{OperadorNebrys.Email},{bloqueado}" },
            servicos: s => s.AddSingleton<IPasswordHasher<Usuario>>(contador));
        await OperadorNebrys.ContaAsync(app, OperadorNebrys.Email);
        await OperadorNebrys.ContaAsync(app, bloqueado);
        for (var i = 0; i < 5; i++)
        {
            using var errada = await OperadorNebrys.SessaoAsync(app.GetTestClient(), bloqueado, "senha-errada-123");
        }

        foreach (var (nome, email, senha) in new[]
                 {
                     ("e-mail sem conta", $"ninguem-{Guid.NewGuid():N}@nebrys.test", OperadorNebrys.Senha),
                     ("senha vazia", OperadorNebrys.Email, string.Empty),
                     ("conta bloqueada", bloqueado, OperadorNebrys.Senha),
                     ("senha errada", OperadorNebrys.Email, "senha-errada-456"),
                 })
        {
            var antes = contador.Conferencias;

            using var resposta = await OperadorNebrys.SessaoAsync(app.GetTestClient(), email, senha);

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound, nome);
            (contador.Conferencias - antes).Should().Be(1, "a recusa por {0} custa um hash, como a senha errada", nome);
        }
    }

    /// <summary>O hasher padrão do Identity, contando as conferências.</summary>
    private sealed class HasherQueConta : IPasswordHasher<Usuario>
    {
        private readonly PasswordHasher<Usuario> _real = new();
        private int _conferencias;

        public int Conferencias => Volatile.Read(ref _conferencias);

        public string HashPassword(Usuario user, string password) => _real.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(Usuario user, string hashedPassword, string providedPassword)
        {
            Interlocked.Increment(ref _conferencias);
            return _real.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}
