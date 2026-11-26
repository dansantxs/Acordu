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

    /// <summary>Data do cancelamento das empresas anteriores ao F14 (design do F14, D1).</summary>
    public static readonly DateTimeOffset CanceladasAntesDoF14Em = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// F14 (F14-08, decisão 13 do G2): as empresas anteriores ao F14 (só de teste) ficam CANCELADA, com o cancelamento e
    /// o início do só leitura numa data fixa antiga (<see cref="CanceladasAntesDoF14Em"/>), já fora do prazo do expurgo
    /// sem depender da configuração; o primeiro diário com o expurgo de empresas as apaga (F14-112). Sem fim de teste:
    /// nunca estiveram em teste. A condição pega só as linhas que a coluna nova acabou de receber vazias.
    /// </summary>
    public const string CancelarEmpresasAnterioresAoF14 = """
        UPDATE tenants
        SET situacao = 'CANCELADA',
            cancelada_em = TIMESTAMPTZ '2026-01-01 00:00:00+00',
            somente_leitura_desde = TIMESTAMPTZ '2026-01-01 00:00:00+00',
            fim_do_teste = NULL
        WHERE situacao = '';
        """;

    /// <summary>
    /// F15a (F15-62, design do F15, D8): as mensagens anteriores ao F15 ganham o canal pelo autor: as do contato,
    /// que até aqui só escrevia pelo portal (conta ou link), ficam PORTAL; as da equipe, ACORDU. A condição pega só as
    /// linhas que a coluna nova recebeu vazias.
    /// </summary>
    public const string PreencherCanalDasInteracoes = """
        UPDATE interacoes
        SET canal = CASE WHEN autor_eh_cliente THEN 'PORTAL' ELSE 'ACORDU' END
        WHERE canal = '';
        """;
}
