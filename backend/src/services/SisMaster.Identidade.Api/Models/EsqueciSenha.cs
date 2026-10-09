using System.ComponentModel.DataAnnotations;

namespace SisMaster.Identidade.Api.Models;

public class EsqueciSenha
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string? Email { get; set; }
}
