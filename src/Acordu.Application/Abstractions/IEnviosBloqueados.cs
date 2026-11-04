namespace Acordu.Application.Abstractions;

/// <summary>
/// Registro dos avisos automáticos descartados porque a empresa ainda não foi liberada (RF-AUT-06; spec do F9, decisão
/// 6): só a categoria e a empresa, nunca o endereço.
/// </summary>
public interface IEnviosBloqueados
{
    void Registrar(string categoria);
}
