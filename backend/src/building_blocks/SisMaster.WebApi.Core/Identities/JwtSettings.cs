namespace SisMaster.WebApi.Core.Identities
{
    public class JwtSettings
    {
        public string? Segredo { get; set; }
        public string? Emissor { get; set; }
        public string? Audiencia { get; set; }

        /// <summary>Validade do token de Administrador e de Administrador de associação (renovada pelo uso).</summary>
        public int MinutosParaExpirar { get; set; } = 30;

        /// <summary>Validade do token de Árbitro: cobre um jogo inteiro sem cair no meio.</summary>
        public int HorasParaExpirarArbitro { get; set; } = 8;
    }
}
