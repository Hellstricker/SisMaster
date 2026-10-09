namespace SisMaster.Identidade.Api.Models;

/// <summary>O usuário que entrou: o front usa para escolher a tela inicial e o menu.</summary>
public class UsuarioToken
{
    public string? Id { get; internal set; }
    public string? Email { get; internal set; }
    public string? Nome { get; internal set; }
    public string? Perfil { get; internal set; }
    public IReadOnlyList<string> Associacoes { get; internal set; } = [];

    /// <summary>Verdadeiro enquanto a pessoa usa uma senha temporária: o front leva direto para a troca.</summary>
    public bool TrocarSenha { get; internal set; }
}
