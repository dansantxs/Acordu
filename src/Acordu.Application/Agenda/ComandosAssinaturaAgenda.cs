using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;

namespace Acordu.Application.Agenda;

/// <summary>
/// Link da agenda assinável da própria pessoa (contrato §11d, RF-COL-04): emitir (substitui o anterior), trocar o modo,
/// revogar. Só a equipe; emissão e revogação vão para a auditoria.
/// </summary>
public sealed class ComandosAssinaturaAgenda
{
    private readonly ITenantContext _contexto;
    private readonly IAssinaturasAgenda _assinaturas;
    private readonly EnderecosPublicos _enderecos;
    private readonly IAuditoria _auditoria;
    private readonly IUnitOfWork _unidade;

    public ComandosAssinaturaAgenda(
        ITenantContext contexto, IAssinaturasAgenda assinaturas, EnderecosPublicos enderecos, IAuditoria auditoria, IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(assinaturas);
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(auditoria);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _assinaturas = assinaturas;
        _enderecos = enderecos;
        _auditoria = auditoria;
        _unidade = unidade;
    }

    public async Task<AssinaturaAgendaDto> ObterAsync(CancellationToken cancellationToken = default) =>
        await _assinaturas.DaPessoaAsync(_contexto.Usuario(), cancellationToken) is { } atual
            ? new AssinaturaAgendaDto(true, atual.Discreta, atual.CriadaEm)
            : new AssinaturaAgendaDto(false, false, null);

    public async Task<LinkAgendaDto> EmitirAsync(AssinaturaAgendaRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var usuario = _contexto.Usuario();
        var token = await _assinaturas.EmitirAsync(usuario, dados.Discreta, cancellationToken);
        _auditoria.Registrar(AcoesAuditoria.AgendaLinkEmissao, "usuario", usuario, new { dados.Discreta });
        await _unidade.SalvarAsync(cancellationToken);
        return new LinkAgendaDto(_enderecos.AgendaIcs(token), dados.Discreta);
    }

    public async Task<AssinaturaAgendaDto> AlterarModoAsync(AssinaturaAgendaRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        if (!await _assinaturas.AlterarModoAsync(_contexto.Usuario(), dados.Discreta, cancellationToken))
            throw new NaoEncontradoException("Você ainda não tem link da agenda.");
        await _unidade.SalvarAsync(cancellationToken);
        return await ObterAsync(cancellationToken);
    }

    public async Task RevogarAsync(CancellationToken cancellationToken = default)
    {
        var usuario = _contexto.Usuario();
        if (!await _assinaturas.RevogarAsync(usuario, cancellationToken))
            throw new NaoEncontradoException("Você ainda não tem link da agenda.");
        _auditoria.Registrar(AcoesAuditoria.AgendaLinkRevogacao, "usuario", usuario, null);
        await _unidade.SalvarAsync(cancellationToken);
    }
}
