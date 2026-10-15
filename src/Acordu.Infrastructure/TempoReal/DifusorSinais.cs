using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Acordu.Infrastructure.TempoReal;

/// <summary>"Algo deste tipo e Id mudou": o front invalida o cache e busca de novo pela API (contrato §11e).</summary>
public sealed record SinalMudanca(string Tipo, Guid Id);

/// <summary>
/// Difusor em memória dos sinais de mudança, por tenant (ADR 0009). Cada conexão de <c>/eventos</c> tem um canal limitado
/// que descarta o mais antigo quando enche: conexão lenta perde sinal, nunca segura quem publica. Uma instância da API
/// basta no v1; com mais de uma, troca-se por LISTEN/NOTIFY do PostgreSQL atrás da mesma interface.
/// </summary>
public sealed class DifusorSinais
{
    public const int CapacidadePorConexao = 256;

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<SinalMudanca>>> _porTenant = new();

    /// <summary>Quantas conexões o tenant tem agora (para testes e métricas).</summary>
    public int Conexoes(Guid tenantId) => _porTenant.TryGetValue(tenantId, out var canais) ? canais.Count : 0;

    public void Publicar(Guid tenantId, IEnumerable<SinalMudanca> sinais)
    {
        ArgumentNullException.ThrowIfNull(sinais);
        if (!_porTenant.TryGetValue(tenantId, out var canais) || canais.IsEmpty)
            return;
        foreach (var sinal in sinais)
        {
            foreach (var canal in canais.Values)
                canal.Writer.TryWrite(sinal);
        }
    }

    /// <summary>Abre um canal para a conexão; descartar a assinatura o fecha.</summary>
    public Assinatura Assinar(Guid tenantId)
    {
        var canal = Channel.CreateBounded<SinalMudanca>(new BoundedChannelOptions(CapacidadePorConexao)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        var id = Guid.NewGuid();
        _porTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, Channel<SinalMudanca>>())[id] = canal;
        return new Assinatura(canal.Reader, () =>
        {
            if (_porTenant.TryGetValue(tenantId, out var canais))
                canais.TryRemove(id, out _);
            canal.Writer.TryComplete();
        });
    }

    public sealed class Assinatura : IDisposable
    {
        private readonly Action _encerrar;

        internal Assinatura(ChannelReader<SinalMudanca> leitor, Action encerrar)
        {
            Leitor = leitor;
            _encerrar = encerrar;
        }

        public ChannelReader<SinalMudanca> Leitor { get; }

        public void Dispose() => _encerrar();
    }
}
