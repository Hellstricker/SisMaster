namespace SisMaster.Identidade.Api.Models;

public class UsuarioToken
{
    public string? Id { get; internal set; }
    public string? Email { get; internal set; }
    public string? Nome { get; internal set; }
    public object? Claims { get; internal set; }
}
