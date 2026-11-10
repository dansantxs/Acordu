using System.Diagnostics;
using System.Reflection;

namespace Acordu.Api.Operacao;

/// <summary>
/// O primeiro quadro da pilha que é código do Acordu (<c>Acordu.*</c>), como <c>Tipo.Metodo</c>: parte da impressão
/// digital do erro (F13-05). Desfaz os tipos que o compilador gera para <c>async</c> e lambdas
/// (<c>&lt;Metodo&gt;d__3.MoveNext</c> vira <c>Tipo.Metodo</c>), para o mesmo erro dar a mesma impressão em toda versão.
/// </summary>
internal static class QuadroDoAcordu
{
    private const string Prefixo = "Acordu.";

    public static string? De(Exception excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        foreach (var quadro in new StackTrace(excecao, fNeedFileInfo: false).GetFrames())
        {
            if (quadro.GetMethod() is not { } metodo || metodo.DeclaringType is not { } tipo)
                continue;
            var (dono, nome) = Desfazer(tipo, metodo);
            if (dono.FullName is { } completo && completo.StartsWith(Prefixo, StringComparison.Ordinal))
                return $"{completo}.{nome}";
        }

        return null;
    }

    private static (Type Dono, string Nome) Desfazer(Type tipo, MethodBase metodo)
    {
        var nome = metodo.Name;
        // Máquina de estado (async/iterador) ou classe de closure: o nome real está entre < > e o dono é o tipo de fora.
        while (tipo.Name.StartsWith('<') && tipo.DeclaringType is { } externo)
        {
            if (Interno(tipo.Name) is { Length: > 0 } doTipo)
                nome = doTipo;
            tipo = externo;
        }

        if (Interno(nome) is { Length: > 0 } doMetodo)
            nome = doMetodo;
        return (tipo, nome);
    }

    /// <summary><c>&lt;Mover&gt;d__3</c> → <c>Mover</c>; <c>&lt;&gt;c</c> → vazio.</summary>
    private static string? Interno(string nome)
    {
        if (!nome.StartsWith('<'))
            return null;
        var fim = nome.IndexOf('>', StringComparison.Ordinal);
        return fim > 1 ? nome[1..fim] : "";
    }
}
