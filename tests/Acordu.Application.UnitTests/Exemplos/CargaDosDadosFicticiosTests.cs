using Acordu.Application.Exemplos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.Exemplos;

/// <summary>
/// Spec do F14, F14-92 (design D13): a carga do arquivo de dados fictícios monta clientes, contatos, contratos,
/// solicitações, tarefas e horas no tenant, com os clientes marcados como exemplo e sem nenhum evento para a outbox
/// (nenhum e-mail sai).
/// </summary>
public sealed class CargaDosDadosFicticiosTests
{
    private static readonly Guid Dono = Guid.NewGuid();
    private readonly Tenant _tenant = Falsos.EmTeste(Dono);
    private readonly CargaEmMemoria _memoria;

    public CargaDosDadosFicticiosTests()
    {
        _memoria = new CargaEmMemoria(_tenant.Id);
    }

    private Task<int> MontarAsync(IReadOnlyDictionary<string, Guid>? pessoas = null, bool exemplo = true) =>
        _memoria.Carga.MontarAsync(DadosFicticios.Arquivo, _tenant, Dono, pessoas, exemplo);

    [Fact]
    public async Task MontarAsync_Exemplos_CriaTudoNoTenantComOsClientesMarcados()
    {
        var clientes = await MontarAsync();

        var dados = DadosFicticios.Arquivo;
        clientes.Should().Be(dados.Clientes.Count);
        _memoria.Clientes.Itens.Should().HaveCount(dados.Clientes.Count)
            .And.OnlyContain(c => c.Exemplo && c.Contatos.Count == 1);
        _memoria.Contratos.Itens.Should().HaveCount(dados.Clientes.Count);
        _memoria.Solicitacoes.Itens.Should().HaveCount(dados.Solicitacoes.Count);
        _memoria.Projetos.Itens.Should().HaveCount(dados.Projetos.Count);
        _memoria.Tarefas.Itens.Should().HaveCount(dados.Tarefas.Count);
        _memoria.Interacoes.Itens.Should().HaveCount(
            dados.Solicitacoes.SelectMany(s => s.Passos ?? []).Count(p => p.Acao == PassoFicticio.Interacao));
        _memoria.Apontamentos.Itens.Should().NotBeEmpty();
        _memoria.Todas.Should().OnlyContain(e => e.TenantId == _tenant.Id);
    }

    [Fact]
    public async Task MontarAsync_Exemplos_NenhumEventoParaAOutbox()
    {
        await MontarAsync();

        _memoria.Todas.Should().OnlyContain(e => e.Eventos.Count == 0, "sem evento não há outbox, e sem outbox nenhum e-mail sai");
    }

    [Fact]
    public async Task MontarAsync_Exemplos_TudoLigadoACliente()
    {
        await MontarAsync();

        var clientes = _memoria.Clientes.Itens.Select(c => c.Id).ToHashSet();
        _memoria.Contratos.Itens.Should().OnlyContain(c => clientes.Contains(c.ClienteId));
        _memoria.Solicitacoes.Itens.Should().OnlyContain(s => clientes.Contains(s.ClienteId) && s.ContatoId != null && s.ContratoId != null);
        _memoria.Tarefas.Itens.Should().OnlyContain(t => t.ClienteId != null && clientes.Contains(t.ClienteId.Value));
        _memoria.Apontamentos.Itens.Should().OnlyContain(a => a.ClienteId != null && clientes.Contains(a.ClienteId.Value));
        _memoria.Solicitacoes.Itens.Should().OnlyContain(s => s.CategoriaId == null, "categorias não são ligadas a cliente");
    }

    [Fact]
    public async Task MontarAsync_SemPessoas_ODonoFazTudo()
    {
        await MontarAsync();

        _memoria.Solicitacoes.Itens.Should().OnlyContain(s => s.ResponsavelId == null || s.ResponsavelId == Dono);
        _memoria.Tarefas.Itens.Should().OnlyContain(t => t.ResponsavelId == Dono);
        _memoria.Apontamentos.Itens.Should().OnlyContain(a => a.UsuarioId == Dono);
        _memoria.Interacoes.Itens.Should().OnlyContain(i => i.AutorId == Dono);
        _memoria.Clientes.Itens.Should().OnlyContain(c => c.ResponsavelId == Dono);
    }

    [Fact]
    public async Task MontarAsync_ComPessoas_UsaOUsuarioDeCadaChave()
    {
        var rafael = Guid.NewGuid();

        await MontarAsync(new Dictionary<string, Guid> { ["rafael"] = rafael });

        Solicitacao("Caixa não imprime o cupom desde ontem").ResponsavelId.Should().Be(rafael);
        Solicitacao("Criar e-mail para a nova advogada").ResponsavelId.Should().Be(Dono, "chave sem usuário fica com o autor");
    }

    [Fact]
    public async Task MontarAsync_SemExemplo_NaoMarcaOsClientes()
    {
        await MontarAsync(exemplo: false);

        _memoria.Clientes.Itens.Should().OnlyContain(c => !c.Exemplo);
    }

    [Fact]
    public async Task MontarAsync_Mencao_ViraOUsuarioDaPessoa()
    {
        await MontarAsync();

        var interna = _memoria.Interacoes.Itens.Single(i => i.Visibilidade == Visibilidade.Interna);
        interna.Texto.Should().Contain($"@{{{Dono}}}").And.NotContain("@{marina}");
    }

    [Fact]
    public async Task MontarAsync_Passos_DeixamCadaSolicitacaoNoStatusDoManual()
    {
        await MontarAsync();

        var pdv = Solicitacao("Caixa não imprime o cupom desde ontem");
        pdv.Status.Should().Be(StatusSolicitacao.EmAndamento);
        pdv.PrimeiraRespostaEm.Should().Be(Falsos.Agora, "a resposta externa da equipe registra a primeira resposta");
        Solicitacao("Atualizar os preços do cardápio no site").Status.Should().Be(StatusSolicitacao.AguardandoCliente);
        Solicitacao("QR code do cardápio nas mesas").Status.Should().Be(StatusSolicitacao.Resolvida);
        Solicitacao("Recuperar planilha apagada sem querer").Status.Should().Be(StatusSolicitacao.Resolvida);
        Solicitacao("Wi-Fi da recepção caindo à tarde").Status.Should().Be(StatusSolicitacao.EmAndamento, "a tarefa criada nela tira de ABERTA");
        Solicitacao("Lentidão ao abrir a pasta compartilhada").Status.Should().Be(StatusSolicitacao.Aberta);
        _memoria.Solicitacoes.Itens.Select(s => s.Numero).Should().BeEquivalentTo(Enumerable.Range(1, _memoria.Solicitacoes.Itens.Count));
    }

    [Fact]
    public async Task MontarAsync_Tarefas_StatusEChecklistDoArquivo()
    {
        await MontarAsync();

        Tarefa("Layout da página inicial").Status.Should().Be(StatusTarefa.Concluida);
        Tarefa("Catálogo de produtos").Status.Should().Be(StatusTarefa.Fazendo);
        Tarefa("Orçamento do novo servidor").Status.Should().Be(StatusTarefa.AFazer);
        Tarefa("Layout da página inicial").VisivelCliente.Should().BeTrue("tarefa de projeto aparece no portal");
        var conta = Tarefa("Criar a conta de e-mail e o acesso");
        conta.Prazo.Should().Be(Falsos.Hoje);
        conta.Checklist.Should().HaveCount(3);
        conta.Checklist.Count(i => i.Feito).Should().Be(1);
    }

    [Fact]
    public async Task MontarAsync_Apontamentos_NoPassadoEComOContratoDaOrigem()
    {
        await MontarAsync();

        _memoria.Apontamentos.Itens.Should().OnlyContain(a => a.Data <= Falsos.Hoje);
        _memoria.Apontamentos.Itens.Should().OnlyContain(a => a.ContratoId != null, "a vigência dos contratos começa três meses antes");
        // Terça, 06/10: das quatro horas "da semana" da Marina, só a de segunda entra; as seis "do mês" entram todas.
        var esperados = DadosFicticios.Arquivo.Apontamentos.Count(a => a.Limite != ApontamentoFicticio.Semana) + 1;
        _memoria.Apontamentos.Itens.Should().HaveCount(esperados);
    }

    private Solicitacao Solicitacao(string titulo) => _memoria.Solicitacoes.Itens.Single(s => s.Titulo == titulo);

    private Tarefa Tarefa(string titulo) => _memoria.Tarefas.Itens.Single(t => t.Titulo == titulo);
}
