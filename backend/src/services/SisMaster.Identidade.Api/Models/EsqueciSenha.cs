using System.ComponentModel.DataAnnotations;

namespace SisMaster.Identidade.Api.Models;

public class EsqueciSenha
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string? Email { get; set; }
}

public class RedefinirSenha
{
    [Required(ErrorMessage = "Link inválido: falta o e-mail.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Link inválido: falta o código.")]
    public string? Token { get; set; }

    [Required(ErrorMessage = "Informe a nova senha.")]
    public string? NovaSenha { get; set; }
}
