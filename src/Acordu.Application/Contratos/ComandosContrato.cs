using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Contratos;

/// <summary>
/// Casos de uso de escrita de contratos (contrato §5). A regra "um ATIVO de cada tipo por cliente no período"
/// (RF-CTR-02) cruza agregados e é checada aqui ao criar, reativar e mudar a vigência.
/// </summary>
public sealed class ComandosContrato
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Contrato> _contratos;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<AjusteSaldo> _ajustes;
    private readonly IConsultasContrato _consultas;
    private readonly IUnitOfWork _unidade;
    private readonly IControleVersao _versoes;
    private readonly RelogioDoTenant _relogio;

    public ComandosContrato(
        ITenantContext contexto,
        IRepositorio<Contrato> contratos,
        IRepositorio<Cliente> clientes,
        IRepositorio<AjusteSaldo> ajustes,
        IConsultasContrato consultas,
        IUnitOfWork unidade,
        IControleVersao versoes,
        RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(ajustes);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(versoes);
        ArgumentNullException.ThrowIfNull(relogio);
        _contexto = contexto;
        _contratos = contratos;
        _clientes = clientes;
        _ajustes = ajustes;
        _consultas = consultas;
        _unidade = unidade;
        _versoes = versoes;
        _relogio = relogio;
    }

    public async Task<Guid> CriarAsync(NovoContratoRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ContratoGerenciar);
        var cliente = await _clientes.ObterPorIdAsync(dados.ClienteId, cancellationToken)
            ?? throw ErroDeCampo.Criar("clienteId", "Cliente não encontrado.");
        if (!cliente.Ativo)
            throw ErroDeCampo.Criar("clienteId", "Cliente inativo não recebe novos contratos.");

        var contrato = Contrato.Criar(
            Guid.CreateVersion7(),
            _contexto.TenantId!.Value,
            cliente.Id,
            dados.Tipo,
            Vigencia(dados.Inicio, dados.Fim),
            Franquia(dados.FranquiaMinutos),
            Politica(dados.Sla));
        await GarantirUnicoAtivoAsync(contrato, cancellationToken);
        _contratos.Adicionar(contrato);
        await _unidade.SalvarAsync(cancellationToken);
        return contrato.Id;
    }

    public async Task EditarAsync(Guid id, EdicaoContratoRequest dados, string? versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var contrato = await CarregarAsync(id, Permissoes.ContratoGerenciar, versao, cancellationToken);
        contrato.AlterarFranquia(Franquia(dados.FranquiaMinutos));
        contrato.DefinirSla(Politica(dados.Sla));
        if (dados.Fim != contrato.Vigencia.Fim)
        {
            if (dados.Fim is { } fim && fim < contrato.Vigencia.Inicio)
                throw ErroDeCampo.Criar("fim", "O fim da vigência não pode ser anterior ao início.");
            contrato.AlterarFimVigencia(dados.Fim);
            await GarantirUnicoAtivoAsync(contrato, cancellationToken);
        }

        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task SuspenderAsync(Guid id, string? versao, CancellationToken cancellationToken = default)
    {
        var contrato = await CarregarAsync(id, Permissoes.ContratoGerenciar, versao, cancellationToken);
        contrato.Suspender();
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task ReativarAsync(Guid id, string? versao, CancellationToken cancellationToken = default)
    {
        var contrato = await CarregarAsync(id, Permissoes.ContratoGerenciar, versao, cancellationToken);
        contrato.Reativar();
        await GarantirUnicoAtivoAsync(contrato, cancellationToken);
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task EncerrarAsync(Guid id, DateOnly? data, string? versao, CancellationToken cancellationToken = default)
    {
        var contrato = await CarregarAsync(id, Permissoes.ContratoGerenciar, versao, cancellationToken);
        var fim = data ?? await _relogio.HojeAsync(cancellationToken);
        if (fim < contrato.Vigencia.Inicio)
            throw ErroDeCampo.Criar("data", "O encerramento não pode ser anterior ao início da vigência.");
        contrato.Encerrar(fim);
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task<AjusteSaldoDto> AjustarSaldoAsync(Guid id, AjusteSaldoRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var contrato = await CarregarAsync(id, Permissoes.ContratoAjustarSaldo, null, cancellationToken);
        var ajuste = AjusteSaldo.Registrar(
            Guid.CreateVersion7(),
            contrato,
            new DuracaoAssinada(dados.QuantidadeMinutos),
            dados.Motivo,
            dados.Data ?? await _relogio.HojeAsync(cancellationToken),
            _contexto.Usuario());
        _ajustes.Adicionar(ajuste);
        await _unidade.SalvarAsync(cancellationToken);
        return new AjusteSaldoDto(ajuste.Id, contrato.Id, ajuste.Quantidade.Minutos, ajuste.Motivo, ajuste.Data, ajuste.AutorId);
    }

    public async Task FecharPeriodoAsync(Guid id, MesReferencia periodo, CancellationToken cancellationToken = default)
    {
        var contrato = await CarregarAsync(id, Permissoes.ContratoFecharPeriodo, null, cancellationToken);
        contrato.FecharPeriodo(periodo, await _relogio.HojeAsync(cancellationToken));
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<Contrato> CarregarAsync(Guid id, string permissao, string? versao, CancellationToken cancellationToken)
    {
        _contexto.Exigir(permissao);
        var contrato = await _contratos.ObterPorIdAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Contrato não encontrado.");
        _versoes.Esperar(contrato, versao);
        return contrato;
    }

    private async Task GarantirUnicoAtivoAsync(Contrato contrato, CancellationToken cancellationToken)
    {
        if (contrato.Status != StatusContrato.Ativo)
            return;
        if (await _consultas.ExisteAtivoSobrepostoAsync(contrato.ClienteId, contrato.Tipo, contrato.Vigencia, contrato.Id, cancellationToken))
        {
            throw new ContratoIncoerenteException(
                "Já existe contrato ativo deste tipo para o cliente com vigência no mesmo período.");
        }
    }

    private static Periodo Vigencia(DateOnly inicio, DateOnly? fim) =>
        fim is { } f && f < inicio
            ? throw ErroDeCampo.Criar("fim", "O fim da vigência não pode ser anterior ao início.")
            : new Periodo(inicio, fim);

    private static Duracao? Franquia(int? minutos) => minutos switch
    {
        null => null,
        < 0 => throw ErroDeCampo.Criar("franquiaMinutos", "A franquia não pode ser negativa."),
        _ => Duracao.FromMinutos(minutos.Value),
    };

    private static PoliticaSla Politica(IReadOnlyList<PrazoSlaDto>? sla)
    {
        if (sla is null || sla.Count == 0)
            throw ErroDeCampo.Criar("sla", "Informe os prazos de SLA das quatro prioridades.");
        if (sla.Any(p => p.PrimeiraRespostaMinutos < 0 || p.ResolucaoMinutos < 0))
            throw ErroDeCampo.Criar("sla", "Prazos de SLA não podem ser negativos.");
        return new PoliticaSla(sla.Select(p => new PrazoSla(
            p.Prioridade, Duracao.FromMinutos(p.PrimeiraRespostaMinutos), Duracao.FromMinutos(p.ResolucaoMinutos))));
    }
}
