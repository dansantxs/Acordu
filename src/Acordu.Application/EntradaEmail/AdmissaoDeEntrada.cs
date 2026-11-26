namespace Acordu.Application.EntradaEmail;

/// <summary>
/// A primeira conferência depois do destino (design do F15, D12: token → tamanho → destino → <b>empresa</b> → resto): o
/// e-mail só segue para a empresa que existe, grava e está com o recebimento ligado. Os três descartes não gravam nada
/// nem respondem ao remetente (F15-05, F15-19, F15-22; ADR 0016, itens 2 e 8), e a rota responde igual para os três.
/// </summary>
public static class AdmissaoDeEntrada
{
    /// <summary>O descarte, ou nulo quando o e-mail segue para a empresa.</summary>
    public static DesfechoEntrada? Avaliar(EmpresaDeEntrada? empresa) => empresa switch
    {
        null => DesfechoEntrada.Desconhecida,
        { SomenteLeitura: true } => DesfechoEntrada.SomenteLeitura,
        { RecebimentoLigado: false } => DesfechoEntrada.Desligada,
        _ => null,
    };
}
