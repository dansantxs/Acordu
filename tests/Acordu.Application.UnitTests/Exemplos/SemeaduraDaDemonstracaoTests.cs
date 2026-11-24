using Acordu.Application.Common;
using Acordu.Application.Exemplos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.Exemplos;

/// <summary>
/// Spec do F14, F14-87 e F14-88 (F14s; design D13): a semeadura da demonstração monta os dados fictícios inteiros (os do
/// manual) com a equipe fictícia, as pessoas indicadas como Dono, categorias, recorrências, o cronômetro e o portal, sem
/// nenhum evento para a outbox e sem marcar exemplo.
/// </summary>
public sealed class SemeaduraDaDemonstracaoTests
{
    private static readonly DadosFicticios Dados = DadosFicticios.Arquivo;

    private readonly Tenant _tenant = Tenant.CriarDemonstracao(Guid.NewGuid(), "Estúdio Aurora", Falsos.Agora, Guid.NewGuid());
    private readonly CargaEmMemoria _carga;
    private readonly Repositorio<Papel> _papeis = new();
    private readonly Repositorio<Vinculo> _vinculos = new();
    private readonly Repositorio<CalendarioUtil> _calendarios = new();
    private readonly Repositorio<Categoria> _categorias = new();
    private readonly Repositorio<RecorrenciaTarefa> _recorrencias = new();
    private readonly Repositorio<Cronometro> _cronometros = new();
    private readonly Dictionary<string, Guid> _pessoas;
    private readonly Guid _contatoDoPortal = Guid.NewGuid();

    public SemeaduraDaDemonstracaoTests()
    {
        _carga = new CargaEmMemoria(_tenant.Id);
        _pessoas = Dados.Pessoas.ToDictionary(p => p.Chave, _ => Guid.NewGuid(), StringComparer.Ordinal);
    }

    private Task<int> SemearAsync(params Guid[] indicadas)
    {
        var calendarios = new CalendariosFixos(_tenant.Id);
        var semeadura = new SemeaduraDaDemonstracao(
            _carga.Carga, _papeis, _vinculos, _calendarios, _categorias, _recorrencias, _cronometros,
            new RelogioDoTenant(calendarios, new RelogioFixo(Falsos.Agora)));
        return semeadura.SemearAsync(Dados, _tenant, _pessoas, _contatoDoPortal, indicadas);
    }

    [Fact]
    public async Task Semear_Demonstracao_TodosOsDadosDoArquivoNoTenant() // F14-87
    {
        var clientes = await SemearAsync();

        clientes.Should().Be(Dados.Clientes.Count);
        _carga.Clientes.Itens.Select(c => c.Nome).Should().BeEquivalentTo(Dados.Clientes.Select(c => c.Nome));
        _carga.Clientes.Itens.Should().OnlyContain(c => !c.Exemplo, "a demonstração não é dado de exemplo");
        _carga.Solicitacoes.Itens.Should().HaveCount(Dados.Solicitacoes.Count);
        _carga.Tarefas.Itens.Should().HaveCount(Dados.Tarefas.Count);
        _papeis.Itens.Select(p => p.Nome).Should().BeEquivalentTo(Papel.Padroes(_tenant.Id).Select(p => p.Nome));
        _calendarios.Itens.Should().ContainSingle();
        _categorias.Itens.Select(c => c.Nome).Should().BeEquivalentTo(Dados.Categorias.Select(c => c.Nome));
        _recorrencias.Itens.Should().HaveCount(Dados.Recorrencias.Count);
        _cronometros.Itens.Should().ContainSingle().Which.UsuarioId.Should().Be(_pessoas[Dados.Cronometro!.Por]);
        _carga.Todas.Concat(_papeis.Itens).Concat(_vinculos.Itens).Concat(_categorias.Itens).Concat(_recorrencias.Itens)
            .Concat(_cronometros.Itens).Concat(_calendarios.Itens)
            .Should().OnlyContain(e => e.TenantId == _tenant.Id && e.Eventos.Count == 0, "no tenant e sem nada para a outbox");
    }

    [Fact]
    public async Task Semear_Equipe_CadaPessoaComOPapelEACapacidadeDoArquivo() // F14-87
    {
        await SemearAsync();

        foreach (var pessoa in Dados.Pessoas)
        {
            var vinculo = _vinculos.Itens.Single(v => v.UsuarioId == _pessoas[pessoa.Chave]);
            _papeis.Itens.Single(p => p.Id == vinculo.PapelId).Nome.Should().Be(pessoa.Papel);
            vinculo.CapacidadeSemanal!.Value.Minutos.Should().Be(pessoa.CapacidadeSemanalMinutos);
        }
    }

    [Fact]
    public async Task Semear_Portal_ContatoComOVinculoDeClienteEAConta() // F14-87
    {
        await SemearAsync();

        var portal = Dados.Portal!;
        var cliente = _carga.Clientes.Itens.Single(c => c.Nome == Dados.Clientes.Single(x => x.Chave == portal.Cliente).Nome);
        cliente.Contatos.Single().UsuarioId.Should().Be(_contatoDoPortal);
        var vinculo = _vinculos.Itens.Single(v => v.UsuarioId == _contatoDoPortal);
        vinculo.ClienteId.Should().Be(cliente.Id);
        vinculo.EscopoPortal.Should().Be(portal.Escopo);
        _papeis.Itens.Single(p => p.Id == vinculo.PapelId).Tipo.Should().Be(TipoPapel.Cliente);
    }

    [Fact]
    public async Task Semear_Categorias_NasSolicitacoesDoArquivo() // F14-87: os mesmos dados do manual
    {
        await SemearAsync();

        foreach (var dado in Dados.Solicitacoes)
        {
            var solicitacao = _carga.Solicitacoes.Itens.Single(s => s.Titulo == dado.Titulo);
            var esperada = dado.Categoria is { } chave
                ? _categorias.Itens.Single(c => c.Nome == Dados.Categorias.Single(x => x.Chave == chave).Nome).Id
                : (Guid?)null;
            solicitacao.CategoriaId.Should().Be(esperada, dado.Titulo);
        }
    }

    [Fact]
    public async Task Semear_Indicadas_EntramComoDono() // F14-88, F14-90
    {
        var indicada = Guid.NewGuid();

        await SemearAsync(indicada, _pessoas[Dados.Pessoas[1].Chave]);

        var vinculo = _vinculos.Itens.Single(v => v.UsuarioId == indicada);
        _papeis.Itens.Single(p => p.Id == vinculo.PapelId).EhDono.Should().BeTrue();
        _vinculos.Itens.Count(v => v.UsuarioId == _pessoas[Dados.Pessoas[1].Chave])
            .Should().Be(1, "a conta fictícia não ganha um segundo vínculo");
    }
}
