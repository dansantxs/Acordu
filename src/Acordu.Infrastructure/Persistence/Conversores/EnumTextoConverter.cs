using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acordu.Infrastructure.Persistence.Conversores;

/// <summary>Enum gravado como texto SCREAMING_SNAKE (convenções §1). Renomear valor de enum exige migration de dados.</summary>
public sealed class EnumTextoConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public EnumTextoConverter()
        : base(v => EnumTexto<TEnum>.Texto(v), v => EnumTexto<TEnum>.Valor(v))
    {
    }
}
