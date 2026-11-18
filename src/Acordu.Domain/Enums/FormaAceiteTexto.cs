namespace Acordu.Domain.Enums;

/// <summary>Como a conta registrou a versão de um texto legal (RF-AUT-11; spec do F14, pergunta 12).</summary>
public enum FormaAceiteTexto
{
    /// <summary>Caixa de aceite marcada no cadastro ou no aceite de convite da equipe (F14-81, F14-82).</summary>
    Aceite = 1,

    /// <summary>"Ciente" do aviso de versão nova, ou de quem ainda não tinha aceite (F14-83, F14-119).</summary>
    Ciente = 2,
}
