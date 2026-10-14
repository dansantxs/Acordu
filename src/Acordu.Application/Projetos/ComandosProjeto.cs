using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Projetos;

/// <summary>Casos de uso de projetos (contrato §8.2). Contrato, se houver, precisa ser do mesmo cliente (modelo §4.7).</summary>
public sealed class ComandosProjeto
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Projeto> _projetos;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Contrato> _contratos;
    private readonly IUnitOfWork _unidade;
    private readonly IControleVersao _versoes;

    public ComandosProjeto(
        ITenantContext contexto,
        IRepositorio<Projeto> projetos,
        IRepositorio<Cliente> clientes,
        IRepositorio<Contrato> contratos,
        IUnitOfWork unidade,
        IControleVersao versoes)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(projetos);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(versoes);
        _contexto = contexto;
        _projetos = projetos;
        _clientes = clientes;
        _contratos = contratos;
        _unidade = unidade;
        _versoes = versoes;
    }

    public async Task<Guid> CriarAsync(NovoProjetoRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ProjetoGerenciar);
        var cliente = await _clientes.ObterPorIdAsync(dados.ClienteId, cancellationToken)
            ?? throw ErroDeCampo.Criar("clienteId", "Cliente não encontrado.");
        if (!cliente.Ativo)
            throw ErroDeCampo.Criar("clienteId", "Cliente inativo não recebe novos projetos.");
        await ValidarContratoAsync(dados.ContratoId, cliente.Id, cancellationToken);

        var projeto = Projeto.Criar(
            Guid.CreateVersion7(), _contexto.TenantId!.Value, cliente.Id, dados.Nome, dados.ContratoId, dados.Prazo,
            Estimativa(dados.EstimativaMinutos), dados.Inicio);
        _projetos.Adicionar(projeto);
        await _unidade.SalvarAsync(cancellationToken);
        return projeto.Id;
    }

    public async Task EditarAsync(Guid id, EdicaoProjetoRequest dados, string? versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var projeto = await CarregarAsync(id, versao, cancellationToken);
        projeto.Editar(dados.Nome, dados.Prazo, Estimativa(dados.EstimativaMinutos), dados.Inicio);
        if (dados.ContratoId != projeto.ContratoId)
        {
            await ValidarContratoAsync(dados.ContratoId, projeto.ClienteId, cancellationToken);
            projeto.AlterarContrato(dados.ContratoId);
        }

        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task ConcluirAsync(Guid id, string? versao, CancellationToken cancellationToken = default)
    {
        var projeto = await CarregarAsync(id, versao, cancellationToken);
        projeto.Concluir();
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task CancelarAsync(Guid id, string? versao, CancellationToken cancellationToken = default)
    {
        var projeto = await CarregarAsync(id, versao, cancellationToken);
        projeto.Cancelar();
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<Projeto> CarregarAsync(Guid id, string? versao, CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.ProjetoGerenciar);
        var projeto = await _projetos.ObterPorIdAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Projeto não encontrado.");
        _versoes.Esperar(projeto, versao);
        return projeto;
    }

    private async Task ValidarContratoAsync(Guid? contratoId, Guid clienteId, CancellationToken cancellationToken)
    {
        if (contratoId is not { } id)
            return;
        var contrato = await _contratos.ObterPorIdAsync(id, cancellationToken)
            ?? throw ErroDeCampo.Criar("contratoId", "Contrato não encontrado.");
        if (contrato.ClienteId != clienteId)
            throw new ContratoIncoerenteException("O contrato é de outro cliente.");
    }

    private static Duracao? Estimativa(int? minutos) => minutos switch
    {
        null => null,
        <= 0 => throw ErroDeCampo.Criar("estimativaMinutos", "A estimativa precisa ser maior que zero."),
        _ => Duracao.FromMinutos(minutos.Value),
    };
}
