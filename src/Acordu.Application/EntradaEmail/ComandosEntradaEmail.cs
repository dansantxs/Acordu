using Acordu.Application.Abstractions;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// O e-mail aceito para uma empresa (RF-SOL-11; design do F15, D6), no escopo com o tenant dela fixado (ADR 0016, item 3):
/// as guardas de isolamento e de só leitura valem como em qualquer gravação. Grava o registro de entrada na mesma
/// transação do efeito; a repetição da mesma entrega não grava de novo. Antes da identificação, o automático, o laço e o
/// filtro do provedor são descartados (<see cref="DescarteDeEntrada"/>; D11, D12): ficam só no registro, como
/// <see cref="ResultadoEntradaEmail.Ignorado"/>, e nada é criado nem respondido (F15-85, F15-87, F15-88). Até o F15f, todo
/// e-mail aceito fica como <see cref="ResultadoEntradaEmail.Ignorado"/> (design §3, "entre um PR e outro").
/// </summary>
public sealed class ComandosEntradaEmail
{
    private readonly ITenantContext _contexto;
    private readonly IEmailsRecebidos _registros;
    private readonly IRepositorio<EmailRecebido> _repositorio;
    private readonly IUnitOfWork _unidade;
    private readonly TimeProvider _relogio;
    private readonly OpcoesEmailEntrada _opcoes;

    public ComandosEntradaEmail(
        ITenantContext contexto,
        IEmailsRecebidos registros,
        IRepositorio<EmailRecebido> repositorio,
        IUnitOfWork unidade,
        TimeProvider relogio,
        OpcoesEmailEntrada opcoes)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(registros);
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(opcoes);
        _contexto = contexto;
        _registros = registros;
        _repositorio = repositorio;
        _unidade = unidade;
        _relogio = relogio;
        _opcoes = opcoes;
    }

    /// <summary>
    /// Registra o e-mail que chegou a <paramref name="destino"/> na empresa do contexto. A corrida de duas entregas iguais
    /// esbarra no índice único da chave, e quem chama trata como repetição.
    /// </summary>
    public async Task<DesfechoEntrada> ReceberAsync(
        MensagemDeEntrada mensagem, DestinoDeEntrada destino, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(destino);
        var tenantId = _contexto.TenantId
            ?? throw new InvalidOperationException("A entrada por e-mail roda no escopo da empresa do destino.");

        var chave = ChaveDeEntrada.Calcular(mensagem.MessageId, destino.Endereco, mensagem.HashDoConteudo);
        if (await _registros.ExisteAsync(chave, cancellationToken))
            return DesfechoEntrada.Repetida;

        // Automático, laço e filtro (D12: depois da empresa, antes do limite e da identificação): descarte registrado.
        var descarte = DescarteDeEntrada.Avaliar(mensagem, _opcoes);
        _repositorio.Adicionar(EmailRecebido.Registrar(
            Guid.CreateVersion7(),
            tenantId,
            chave,
            ChaveDeEntrada.DoRemetente(mensagem.Remetentes),
            ResultadoEntradaEmail.Ignorado,
            _relogio.GetUtcNow()));
        await _unidade.SalvarAsync(cancellationToken);
        return descarte ?? DesfechoEntrada.Registrada;
    }
}
