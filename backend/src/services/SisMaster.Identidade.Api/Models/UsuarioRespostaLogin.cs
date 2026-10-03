namespace SisMaster.Identidade.Api.Models;

public class UsuarioRespostaLogin
{
    public string? AccessToken { get; internal set; }
    public double ExpiresIn { get; internal set; }
    public UsuarioToken? UserToken { get; internal set; }
}
