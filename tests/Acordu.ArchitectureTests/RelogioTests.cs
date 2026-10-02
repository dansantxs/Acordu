using Mono.Cecil;

namespace Acordu.ArchitectureTests;

/// <summary>
/// O Domain nunca lê o relógio: o instante entra por parâmetro (convencoes-codigo-acordu.md §2).
/// Isso mantém a CalculadoraSla e as máquinas de estado determinísticas e testáveis.
/// </summary>
public sealed class RelogioTests
{
    private static readonly HashSet<string> MembrosDeRelogio =
    [
        "System.DateTime.get_Now",
        "System.DateTime.get_UtcNow",
        "System.DateTime.get_Today",
        "System.DateTimeOffset.get_Now",
        "System.DateTimeOffset.get_UtcNow",
    ];

    [Fact]
    public void Domain_NaoLeRelogio()
    {
        var chamadas = Il.Chamadas(Camadas.Domain, LeRelogio);

        chamadas.Should().BeEmpty(
            "o Domain recebe 'agora'/'hoje' por parâmetro; a Application usa TimeProvider");
    }

    private static bool LeRelogio(MethodReference alvo) =>
        MembrosDeRelogio.Contains($"{alvo.DeclaringType.FullName}.{alvo.Name}")
        || alvo.DeclaringType.FullName == "System.TimeProvider";
}
