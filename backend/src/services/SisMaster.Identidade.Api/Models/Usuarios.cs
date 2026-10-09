using System.ComponentModel.DataAnnotations;

namespace SisMaster.Identidade.Api.Models;

public class NovoUsuario
{
    [Required(ErrorMessage = "Informe o nome."), MinLength(3, ErrorMessage = "O nome precisa de pelo menos 3 letras."), MaxLength(120)]
    public string? Nome { get; set; }

    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), MaxLength(200)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Escolha o perfil.")]
    public string? Perfil { get; set; }

    public List<Guid> Associacoes { get; set; } = [];
}

public class EdicaoDeUsuario
{
    [Required(ErrorMessage = "Informe o nome."), MinLength(3, ErrorMessage = "O nome precisa de pelo menos 3 letras."), MaxLength(120)]
    public string? Nome { get; set; }

    [Required(ErrorMessage = "Escolha o perfil.")]
    public string? Perfil { get; set; }

    public List<Guid> Associacoes { get; set; } = [];
}

public class UsuarioResumo
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Perfil { get; set; }
    public List<Guid> Associacoes { get; set; } = [];
    /// <summary>"Ativo", "Desativado" ou "Bloqueado" (5 erros de senha seguidos).</summary>
    public string Situacao { get; set; } = "Ativo";
    public bool SenhaTemporaria { get; set; }
}
