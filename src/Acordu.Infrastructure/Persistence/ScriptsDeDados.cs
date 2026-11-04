namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// SQL de dados usado por migrations, aqui para o teste de integração rodar o mesmo texto (as migrations só têm o
/// esquema conferido).
/// </summary>
public static class ScriptsDeDados
{
    /// <summary>
    /// F9 (RF-AUT-06): as empresas anteriores ao F9 ganham o criador (o Dono mais antigo; os Ids de vínculo são UUIDv7,
    /// então a ordem do Id é a de criação) e ficam liberadas para enviar, como já faziam (spec do F9, F9-27).
    /// </summary>
    public const string PreencherCriadorELiberacao = """
        UPDATE tenants t
        SET criado_por_usuario_id = (
            SELECT v.usuario_id
            FROM vinculos v
            JOIN papeis p ON p.id = v.papel_id
            WHERE v.tenant_id = t.id AND p.padrao AND p.nome = 'Dono'
            ORDER BY v.id
            LIMIT 1)
        WHERE t.criado_por_usuario_id IS NULL;
        UPDATE tenants SET envio_liberado_em = now() WHERE envio_liberado_em IS NULL;
        """;
}
