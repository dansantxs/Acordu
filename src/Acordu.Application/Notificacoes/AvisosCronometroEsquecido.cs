using Acordu.Application.Abstractions;
using Acordu.Application.Apontamentos;
using Acordu.Application.Common;
using Acordu.Domain.Services;

namespace Acordu.Application.Notificacoes;

/// <summary>
/// Aviso de cronômetro esquecido (F12-18, F12-19), no tenant do contexto; rodado pelo job <c>tick</c>. Vai só ao dono do
/// cronômetro, uma vez por cronômetro: o marcador (o mesmo dos alertas de SLA) é gravado antes do envio e desfeito se o
/// envio falhar. Categoria <see cref="CategoriaAviso.Cronometro"/>, com e-mail ligado por padrão e desligável.
/// </summary>
public sealed class AvisosCronometroEsquecido
{
    private const string Chave = "cronometro-esquecido";

    private readonly IConsultasApontamento _consultas;
    private readonly IPessoasDaEquipe _pessoas;
    private readonly IMarcadoresDeAviso _marcadores;
    private readonly ICalendarios _calendarios;
    private readonly AvisoInterno _aviso;
    private readonly EnderecosPublicos _enderecos;
    private readonly RelogioDoTenant _relogio;

    public AvisosCronometroEsquecido(
        IConsultasApontamento consultas,
        IPessoasDaEquipe pessoas,
        IMarcadoresDeAviso marcadores,
        ICalendarios calendarios,
        AvisoInterno aviso,
        EnderecosPublicos enderecos,
        RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(pessoas);
        ArgumentNullException.ThrowIfNull(marcadores);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(aviso);
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(relogio);
        _consultas = consultas;
        _pessoas = pessoas;
        _marcadores = marcadores;
        _calendarios = calendarios;
        _aviso = aviso;
        _enderecos = enderecos;
        _relogio = relogio;
    }

    /// <summary>Avisa os cronômetros esquecidos ainda não avisados e devolve quantos foram.</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var ativos = await _consultas.CronometrosAtivosAsync(cancellationToken);
        if (ativos.Count == 0)
            return 0;

        var agora = _relogio.Agora;
        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        var avisados = 0;
        foreach (var cronometro in ativos)
        {
            if (!CronometroEsquecido.Avaliar(cronometro.IniciadoEm, agora, calendario).Esquecido)
                continue;
            // Quem saiu da equipe não recebe nada; o cronômetro fica para quem administra limpar.
            if (await _pessoas.ObterAsync(cronometro.UsuarioId, cancellationToken) is not { } pessoa)
                continue;
            if (!await _marcadores.MarcarAsync(cronometro.Id, Chave, cancellationToken))
                continue;
            try
            {
                var link = _enderecos.TarefaInterna(cronometro.TarefaId);
                await _aviso.AvisarAsync(
                    pessoa,
                    CategoriaAviso.Cronometro,
                    $"{Chave}:{cronometro.Id}",
                    $"O cronômetro em \"{cronometro.TituloTarefa}\" ficou ligado",
                    link,
                    new MensagemEmail(
                        pessoa.Email,
                        "O cronômetro ficou ligado",
                        $"""
                        Olá, {pessoa.Nome}!

                        O cronômetro em "{cronometro.TituloTarefa}" continua rodando. Se você já parou de trabalhar, pare e ajuste a duração antes de apontar:

                        {link}
                        """,
                        "aviso-cronometro-esquecido"),
                    cancellationToken);
            }
            catch
            {
                await _marcadores.DesmarcarAsync(cronometro.Id, Chave, CancellationToken.None);
                throw;
            }

            avisados++;
        }

        return avisados;
    }
}
