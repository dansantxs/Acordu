namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Como terminou um e-mail que chegou à rota de entrada (design do F15, D12; ADR 0016, item 2). Vai ao log só como
/// contagem (convenções §5); a rota responde <c>202</c> para todos, sem dizer qual, menos <see cref="GrandeDemais"/>
/// (<c>413</c>).
/// </summary>
public enum DesfechoEntrada
{
    /// <summary>O destino não é de empresa nenhuma (F15-22). Nada gravado.</summary>
    Desconhecida = 1,

    /// <summary>A empresa está só leitura (F15-19). Nada gravado.</summary>
    SomenteLeitura = 2,

    /// <summary>A empresa está com o recebimento desligado (F15-05). Nada gravado.</summary>
    Desligada = 3,

    /// <summary>Acima do tamanho máximo (F15-89): <c>413</c>, nada gravado.</summary>
    GrandeDemais = 4,

    /// <summary>O corpo não é um e-mail legível. Nada gravado; <c>202</c>, para o servidor de origem não repetir.</summary>
    Malformada = 5,

    /// <summary>A mesma entrega já foi registrada na empresa (idempotência, D6). Nada gravado.</summary>
    Repetida = 6,

    /// <summary>Registrado na empresa (até o F15f, como <c>IGNORADO</c>; design §3).</summary>
    Registrada = 7,

    /// <summary>E-mail automático (F15-85, F15-86; D12): registrado como <c>IGNORADO</c>, nada criado nem respondido.</summary>
    Automatica = 8,

    /// <summary>Remetente do próprio Acordu (laço, F15-88; D12): registrado como <c>IGNORADO</c>, nada respondido.</summary>
    Laco = 9,

    /// <summary>Spam ou vírus pelo filtro do provedor (F15-87; D11): registrado como <c>IGNORADO</c>, sem triagem.</summary>
    Filtrada = 10,
}
