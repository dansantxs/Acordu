namespace Acordu.Application.Common;

/// <summary>Entrada inválida num campo do corpo → 400 <c>validacao</c> com o campo em <c>errors</c>.</summary>
public static class ErroDeCampo
{
    /// <param name="campo">Nome do campo como no JSON (camelCase).</param>
    public static ArgumentException Criar(string campo, string mensagem) => new(mensagem, campo);
}
