using System.ComponentModel.DataAnnotations;

namespace SisMaster.Identidade.Api.Models;

public class UsuarioLogin
{
    [Required(ErrorMessage = "O campo {0} é obrigatório")]
    [EmailAddress(ErrorMessage = "O campo {0} está em formato inválido")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "O campo {0} é obrigatório")]
    public string? Password { get; set; }
}
