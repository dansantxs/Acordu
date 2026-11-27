using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Agregados de apoio, sempre válidos pelo domínio. Outubro de 2026, horário de Brasília.</summary>
internal static class Dados
{
    public static readonly DateTimeOffset Agora = new(2026, 10, 6, 10, 0, 0, TimeSpan.FromHours(-3));

    public static readonly DateOnly Hoje = new(2026, 10, 6);

    private static int _numero;

    public static PoliticaSla Politica() => new(
    [
        new PrazoSla(Prioridade.Urgente, Duracao.FromHoras(1), Duracao.FromHoras(4)),
        new PrazoSla(Prioridade.Alta, Duracao.FromHoras(2), Duracao.FromHoras(16)),
        new PrazoSla(Prioridade.Media, Duracao.FromHoras(4), Duracao.FromHoras(32)),
        new PrazoSla(Prioridade.Baixa, Duracao.FromHoras(8), Duracao.FromHoras(80)),
    ]);

    public static Cliente Cliente(Guid tenant, string nome = "Padaria Bom Grão") =>
        Domain.Entities.Cliente.Criar(Guid.NewGuid(), tenant, nome, "11.222.333/0001-81", null);

    public static Contrato Contrato(Guid tenant, Guid cliente) =>
        Domain.Entities.Contrato.Criar(
            Guid.NewGuid(), tenant, cliente, TipoContrato.BancoHoras, new Periodo(new DateOnly(2026, 10, 1)),
            Duracao.FromHoras(20), Politica());

    public static Solicitacao Solicitacao(Guid tenant, Guid cliente, Guid? contratoId = null) =>
        Domain.Entities.Solicitacao.Abrir(
            Guid.NewGuid(), tenant, cliente, Interlocked.Increment(ref _numero), "Ajustar banner", "Trocar a data",
            Prioridade.Urgente, OrigemSolicitacao.Interna, null, null, null,
            contratoId, contratoId is null ? null : Politica(), Agora, CalendarioUtil.Padrao(tenant));

    public static Tarefa Tarefa(Guid tenant, Guid? cliente, Guid? solicitacaoId = null) =>
        Domain.Entities.Tarefa.Criar(
            Guid.NewGuid(), tenant, "Refazer arte", null, cliente, cliente is null ? null : solicitacaoId ?? Guid.NewGuid(),
            null, null, Hoje.AddDays(3), Duracao.FromHoras(2), visivelCliente: cliente is not null);

    public static Apontamento Apontamento(Guid tenant, Guid? cliente, Guid tarefaId, Guid? contratoId = null) =>
        Domain.Entities.Apontamento.Registrar(
            Guid.NewGuid(), tenant, tarefaId, cliente, Guid.NewGuid(), contratoId, Hoje, Duracao.FromMinutos(90),
            "Ajuste de layout", null, faturavel: true, visivelCliente: null, []);

    public static Projeto Projeto(Guid tenant, Guid cliente) =>
        Domain.Entities.Projeto.Criar(Guid.NewGuid(), tenant, cliente, "Site", null, null, Duracao.FromHoras(40));

    public static Interacao Interacao(Guid tenant, Guid cliente, Guid solicitacaoId) =>
        Domain.Entities.Interacao.Registrar(
            Guid.NewGuid(), tenant, cliente, solicitacaoId, Guid.NewGuid(), false, "Segue a arte", Visibilidade.Externa,
            CanalMensagem.Acordu, [new NovoAnexo("arte.png", "image/png", 1024, $"anexos/{Guid.NewGuid()}")],
            new PoliticaAnexos(20L * 1024 * 1024, ["image/png"], 5), Agora);

    public static RecorrenciaTarefa Recorrencia(Guid tenant, Guid cliente) =>
        RecorrenciaTarefa.Criar(
            Guid.NewGuid(), tenant, cliente, null,
            new ModeloTarefa("Fechamento mensal", null, null, ["Conciliar extratos", "Emitir guias"], Duracao.FromHoras(4), true),
            new RegraRecorrencia(Periodicidade.Mensal, 31, 3, new DateOnly(2026, 10, 1)), new DateOnly(2026, 10, 1));

    public static AcessoPortalEmitido Acesso(Guid tenant, Guid cliente, Guid solicitacaoId) =>
        AcessoPortal.Emitir(Guid.NewGuid(), tenant, cliente, solicitacaoId, Guid.NewGuid(), Agora, TimeSpan.FromDays(30));

    public static ConversaPorEmail Conversa(Guid tenant, Guid cliente, Guid solicitacaoId) =>
        ConversaPorEmail.Criar(Guid.NewGuid(), tenant, cliente, solicitacaoId, Agora);

    /// <summary>Registro de entrada (F15d) com uma chave nova a cada chamada.</summary>
    public static EmailRecebido EmailRecebido(Guid tenant, string? chave = null) =>
        Domain.Entities.EmailRecebido.Registrar(
            Guid.NewGuid(), tenant, chave ?? Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())),
            null, ResultadoEntradaEmail.Ignorado, Agora);

    /// <summary>Item da triagem de e-mails (F15h), com um anexo na chave da triagem.</summary>
    public static ItemTriagemEmail ItemTriagem(Guid tenant, MotivoTriagem motivo = MotivoTriagem.Desconhecido, IReadOnlyList<Guid>? clientes = null)
    {
        var id = Guid.NewGuid();
        return ItemTriagemEmail.Receber(
            id, tenant, "pedro@outro.com.br", "Pedro", "Orçamento do forno", "Quanto custa?", motivo, clientes ?? [],
            [new NovoAnexo("foto.png", "image/png", 1024, $"{tenant}/triagem/{id}/{Guid.NewGuid()}")],
            new AnexosRecusados([new AnexoRecusado("programa.exe", MotivoRecusaAnexo.TipoNaoPermitido)]),
            new PoliticaAnexos(20L * 1024 * 1024, ["image/png"], 5), Agora);
    }

    public static Cronometro Cronometro(Guid tenant) =>
        Domain.Entities.Cronometro.Iniciar(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), Agora);
}
