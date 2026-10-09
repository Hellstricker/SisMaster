using System.ComponentModel.DataAnnotations;

namespace SisMaster.Identidade.Api.Models;

public class TrocarSenha
{
    [Required(ErrorMessage = "Informe a senha atual.")]
    public string? SenhaAtual { get; set; }

    [Required(ErrorMessage = "Informe a nova senha.")]
    public string? NovaSenha { get; set; }
}
