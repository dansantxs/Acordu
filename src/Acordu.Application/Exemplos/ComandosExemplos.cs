using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.Exemplos;

/// <summary>
/// Dados de exemplo da empresa (RF-EMP-05, F14-91 a F14-96; design do F14, D13; contrato §6): a empresa em TESTE ainda
/// sem dados carrega o conjunto fictício (<see cref="DadosFicticios"/>) e o apaga quando quiser. Só quem gerencia a
/// configuração (o Dono). Nenhum e-mail sai: a carga não passa pela outbox (<see cref="CargaDosDadosFicticios"/>).
/// <para>
/// Carregar e apagar são gravação da empresa: na empresa só leitura (ADR 0015) a recusa é a dela,
/// <c>422 empresa-somente-leitura</c>, conferida aqui antes da regra dos exemplos, porque a remoção passa em parte por
/// fora do change tracker (convenções §1); a guarda do contexto confere de novo na gravação.
/// </para>
/// </summary>
public sealed class ComandosExemplos
{
    public const string MensagemIndisponivel =
        "Os dados de exemplo só podem ser carregados numa empresa em teste que ainda não tem dados.";

    private readonly ITenantContext _contexto;
    private readonly ITenantAtual _tenant;
    private readonly IDadosDeExemplo _exemplos;
    private readonly CargaDosDadosFicticios _carga;
    private readonly IAuditoria _auditoria;
    private readonly IUnitOfWork _unidade;
    private readonly IArmazenamentoArquivos _armazenamento;

    public ComandosExemplos(
        ITenantContext contexto,
        ITenantAtual tenant,
        IDadosDeExemplo exemplos,
        CargaDosDadosFicticios carga,
        IAuditoria auditoria,
        IUnitOfWork unidade,
        IArmazenamentoArquivos armazenamento)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(exemplos);
        ArgumentNullException.ThrowIfNull(carga);
        ArgumentNullException.ThrowIfNull(auditoria);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(armazenamento);
        _contexto = contexto;
        _tenant = tenant;
        _exemplos = exemplos;
        _carga = carga;
        _auditoria = auditoria;
        _unidade = unidade;
        _armazenamento = armazenamento;
    }

    /// <summary>Se a carga está disponível (a oferta na Hoje, F14-91 e F14-93) e se há exemplos para apagar.</summary>
    public async Task<DadosExemploDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        var tenant = await _tenant.ObterAsync(cancellationToken);
        var situacao = await _exemplos.SituacaoAsync(tenant.CriadoPorUsuarioId, cancellationToken);
        return new DadosExemploDto(Disponivel(tenant, situacao), situacao.Carregados);
    }

    /// <summary>
    /// Carrega os exemplos (F14-92) com o Dono que pediu como responsável e autor de tudo. Empresa fora de TESTE, com
    /// dado próprio ou com os exemplos já carregados: <see cref="TransicaoInvalidaException"/> (422), sem carregar nada
    /// (F14-93).
    /// </summary>
    public async Task CarregarAsync(CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        var tenant = await GravavelAsync(cancellationToken);
        var situacao = await _exemplos.SituacaoAsync(tenant.CriadoPorUsuarioId, cancellationToken);
        if (!Disponivel(tenant, situacao))
            throw new TransicaoInvalidaException(MensagemIndisponivel);

        var clientes = await _carga.MontarAsync(
            DadosFicticios.Arquivo, tenant, _contexto.Usuario(), pessoas: null, exemplo: true, cancellationToken);
        _auditoria.Registrar(AcoesAuditoria.ExemplosCarga, "empresa", tenant.Id, new { clientes });
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>
    /// Apaga os exemplos e o que a equipe criou dentro deles (F14-94); o resto fica. Sem exemplos, nada muda. Os anexos
    /// saem do storage depois do commit, em melhor esforço.
    /// </summary>
    public async Task ApagarAsync(CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        var tenant = await GravavelAsync(cancellationToken);
        var removidos = await _exemplos.RemoverAsync(cancellationToken);
        if (removidos.Clientes == 0)
            return;

        _auditoria.Registrar(AcoesAuditoria.ExemplosRemocao, "empresa", tenant.Id, new { clientes = removidos.Clientes });
        await _unidade.SalvarAsync(cancellationToken);
        foreach (var chave in removidos.Arquivos)
            await RemoverEmSilencioAsync(chave);
    }

    /// <summary>A oferta: empresa em TESTE (a demonstração nunca), sem exemplos e sem dado próprio.</summary>
    private static bool Disponivel(Tenant tenant, SituacaoDosExemplos situacao) =>
        tenant.Situacao == SituacaoEmpresa.Teste && !tenant.Demonstracao && !situacao.Carregados && !situacao.DadosProprios;

    private async Task<Tenant> GravavelAsync(CancellationToken cancellationToken)
    {
        var tenant = await _tenant.ObterAsync(cancellationToken);
        return tenant.SomenteLeitura ? throw new EmpresaSomenteLeituraException() : tenant;
    }

    /// <summary>Melhor esforço: o anexo que sobrar fica no bucket, sem dono (como no expurgo das empresas).</summary>
    private async Task RemoverEmSilencioAsync(string chave)
    {
        try
        {
            await _armazenamento.RemoverAsync(chave, CancellationToken.None);
        }
#pragma warning disable CA1031 // A limpeza do storage não pode desfazer a remoção já gravada.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}

/// <param name="Disponivel">A empresa pode carregar os exemplos agora (a Hoje oferece).</param>
/// <param name="Carregados">Há dados de exemplo na empresa (a Hoje oferece apagar).</param>
public sealed record DadosExemploDto(bool Disponivel, bool Carregados);
