using Acordu.Application.Abstractions;
using Acordu.Application.Common;

namespace Acordu.Application.Notificacoes;

/// <summary>Sino e preferências da própria pessoa (contrato §11): sem permissão específica, só a sessão com conta.</summary>
public sealed class ComandosNotificacoes
{
    private readonly ITenantContext _contexto;
    private readonly IConsultasNotificacoes _consultas;
    private readonly IPreferenciasNotificacao _preferencias;
    private readonly RelogioDoTenant _relogio;

    public ComandosNotificacoes(
        ITenantContext contexto, IConsultasNotificacoes consultas, IPreferenciasNotificacao preferencias, RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(preferencias);
        ArgumentNullException.ThrowIfNull(relogio);
        _contexto = contexto;
        _consultas = consultas;
        _preferencias = preferencias;
        _relogio = relogio;
    }

    public Task<NotificacoesDto> ListarAsync(bool apenasNaoLidas, Paginacao paginacao, CancellationToken cancellationToken = default) =>
        _consultas.ListarAsync(_contexto.Usuario(), apenasNaoLidas, paginacao, cancellationToken);

    public async Task MarcarLidaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await _consultas.MarcarLidaAsync(_contexto.Usuario(), id, _relogio.Agora, cancellationToken))
            throw new NaoEncontradoException("Notificação não encontrada.");
    }

    public Task<int> MarcarTodasAsync(CancellationToken cancellationToken = default) =>
        _consultas.MarcarTodasAsync(_contexto.Usuario(), _relogio.Agora, cancellationToken);

    public async Task<PreferenciasDto> PreferenciasAsync(CancellationToken cancellationToken = default) =>
        Dto(await _preferencias.EmailDesligadoAsync(_contexto.Usuario(), cancellationToken));

    public async Task<PreferenciasDto> SalvarPreferenciasAsync(PreferenciasDto dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var desligadas = (dados.Categorias ?? []).Where(c => !c.Email).Select(c => c.Categoria).ToHashSet();
        if (desligadas.Any(c => !Enum.IsDefined(c)))
            throw ErroDeCampo.Criar("categorias", "Categoria desconhecida.");
        await _preferencias.SalvarAsync(_contexto.Usuario(), desligadas, cancellationToken);
        return Dto(desligadas);
    }

    private static PreferenciasDto Dto(IReadOnlySet<CategoriaAviso> desligadas) =>
        new([.. Enum.GetValues<CategoriaAviso>().Select(c => new PreferenciaDto(c, !desligadas.Contains(c)))]);
}
