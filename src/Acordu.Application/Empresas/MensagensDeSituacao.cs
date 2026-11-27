using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Empresas;

/// <summary>
/// Textos dos e-mails de situação aos Donos (RF-EMP-04, F14-39, F14-41, F14-42; design do F14, D7). Remetente: o Acordu.
/// As datas saem no fuso da empresa (convenções §2: apresentação), e o "como ativar" cita o canal da Nebrys (D15). Os dias
/// que faltam são os de fato (F14-102), contados como a faixa da SPA: dias civis até o último dia do teste.
/// </summary>
public static class MensagensDeSituacao
{
    public const string CategoriaFimDoTeste = "situacao-fim-do-teste";
    public const string CategoriaVencimento = "situacao-vencimento";
    public const string CategoriaExpurgo = "situacao-expurgo";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <param name="aviso">O aviso devido (<c>Tenant.AvisoDeSituacaoDevido</c>).</param>
    /// <param name="email">Destinatário: um Dono da empresa.</param>
    /// <param name="nome">Nome do Dono.</param>
    /// <param name="empresa">Nome da empresa.</param>
    /// <param name="fuso">Fuso da empresa (calendário).</param>
    /// <param name="agora">Instante do diário: de onde se contam os dias que faltam.</param>
    /// <param name="fornecedor">Quem fornece o Acordu e o canal dele.</param>
    public static MensagemEmail Montar(
        AvisoSituacao aviso, string email, string nome, string empresa, TimeZoneInfo fuso, DateTimeOffset agora, OpcoesFornecedor fornecedor)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        ArgumentNullException.ThrowIfNull(fuso);
        ArgumentNullException.ThrowIfNull(fornecedor);
        return aviso.Tipo switch
        {
            TipoAvisoSituacao.AntesDoFimDoTeste => FimDoTeste(aviso.Referencia, email, nome, empresa, fuso, agora, fornecedor),
            TipoAvisoSituacao.Vencimento => Vencimento(aviso.Referencia, email, nome, empresa, fuso, fornecedor),
            TipoAvisoSituacao.AntesDoExpurgo => Expurgo(aviso.Referencia, email, nome, empresa, fuso, fornecedor),
            _ => throw new ArgumentOutOfRangeException(nameof(aviso), aviso.Tipo, "Tipo de aviso desconhecido."),
        };
    }

    /// <summary>
    /// Último dia do teste: o dia do instante logo antes do fim, no fuso (a prorrogação até D grava o início de D+1, design
    /// §1.2), e quantos dias civis faltam até ele (0 = termina hoje). A mesma conta da faixa da SPA (<c>gravacao.ts</c>) e
    /// do momento do aviso (<c>Tenant.AvisoDeSituacaoDevido</c>).
    /// </summary>
    public static (DateOnly UltimoDia, int DiasQueFaltam) UltimoDiaDoTeste(DateTimeOffset fimDoTeste, TimeZoneInfo fuso, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(fuso);
        var ultimo = Tenant.UltimoDiaDoTeste(fimDoTeste, fuso);
        var hoje = Dia(agora, fuso);
        return (ultimo, Math.Max(0, ultimo.DayNumber - hoje.DayNumber));
    }

    private static MensagemEmail FimDoTeste(
        DateTimeOffset fim, string email, string nome, string empresa, TimeZoneInfo fuso, DateTimeOffset agora, OpcoesFornecedor fornecedor)
    {
        var (ultimo, dias) = UltimoDiaDoTeste(fim, fuso, agora);
        var quando = dias switch
        {
            0 => "termina hoje",
            1 => "termina amanhã",
            _ => $"faltam {dias} dias",
        };
        return new MensagemEmail(
            email,
            $"O teste da {empresa} no Acordu vai até {Data(ultimo)}",
            $"""
            Olá, {nome}!

            O período de teste da {empresa} no Acordu vai até {Data(ultimo)} ({quando}).

            Depois disso, a empresa fica só leitura: a equipe e os clientes continuam consultando tudo, mas nada novo é
            gravado e nenhum e-mail sai. Para ativar a conta e seguir usando, {fornecedor.FaleComOFornecedor}.
            """,
            CategoriaFimDoTeste);
    }

    private static MensagemEmail Vencimento(
        DateTimeOffset expurgo, string email, string nome, string empresa, TimeZoneInfo fuso, OpcoesFornecedor fornecedor) =>
        new(
            email,
            $"O teste da {empresa} no Acordu terminou",
            $"""
            Olá, {nome}!

            O período de teste da {empresa} no Acordu terminou, e a empresa ficou só leitura: a equipe e os clientes
            continuam consultando tudo, mas nada novo é gravado e nenhum e-mail sai.

            Se a conta não for ativada, a empresa e todos os dados dela serão apagados a partir de {Data(Dia(expurgo, fuso))}.
            Para ativar a conta e manter os dados, {fornecedor.FaleComOFornecedor}.
            """,
            CategoriaVencimento);

    private static MensagemEmail Expurgo(
        DateTimeOffset expurgo, string email, string nome, string empresa, TimeZoneInfo fuso, OpcoesFornecedor fornecedor) =>
        new(
            email,
            $"Os dados da {empresa} no Acordu serão apagados a partir de {Data(Dia(expurgo, fuso))}",
            $"""
            Olá, {nome}!

            A {empresa} está só leitura no Acordu. A partir de {Data(Dia(expurgo, fuso))}, a empresa e todos os dados dela
            (clientes, contratos, solicitações, tarefas, horas e arquivos) serão apagados, sem volta.

            Para ativar a conta e manter os dados, {fornecedor.FaleComOFornecedor}.
            """,
            CategoriaExpurgo);

    private static DateOnly Dia(DateTimeOffset instante, TimeZoneInfo fuso) => Tenant.DiaCivil(instante, fuso);

    private static string Data(DateOnly dia) => dia.ToString("dd/MM/yyyy", PtBr);
}
