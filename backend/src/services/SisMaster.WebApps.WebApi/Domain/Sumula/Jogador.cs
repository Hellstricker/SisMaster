using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Sumula;

/// <summary>
/// Atleta relacionado para o jogo. O nome é um retrato do momento da relação (a súmula não muda se o cadastro mudar);
/// o <see cref="AtletaId"/> liga de volta ao elenco da equipe. A camisa é do jogo, não do atleta.
/// </summary>
public class Jogador : Entity
{
    public Guid TimeId { get; private set; }
    public Guid AtletaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;

    /// <summary>"0", "00" ou "1" a "99" (texto, porque "0" e "00" são camisas diferentes).</summary>
    public string Numero { get; private set; } = string.Empty;
    public bool Titular { get; private set; }

    /// <summary>Usado só pela importação de um jogo realizado, que ajusta os titulares aos do feed.</summary>
    internal void DefinirTitular(bool titular) => Titular = titular;

    /// <summary>Período (1–5) em que o atleta foi acrescentado à súmula depois do início do jogo; nulo = relacionado desde o começo.</summary>
    public int? ChegouNoPeriodo { get; private set; }

    /// <summary>Foi acrescentado antes de o relógio do período correr (vale como presente desde o início do período).</summary>
    public bool ChegouNoInicio { get; private set; } = true;

    protected Jogador() { }

    /// <summary>Atleta acrescentado depois do início do jogo (chegou atrasado).</summary>
    internal Jogador(Guid timeId, RelacionadoNovo r, int periodo, bool noInicio) : this(timeId, r)
    {
        ChegouNoPeriodo = periodo;
        ChegouNoInicio = noInicio;
    }

    internal Jogador(Guid timeId, RelacionadoNovo r)
    {
        TimeId = timeId;
        AtletaId = r.AtletaId;
        Nome = r.Nome;
        Numero = r.Numero;
        Titular = r.Titular;
    }
}

/// <summary>Dados de um atleta que está sendo relacionado (ainda sem identidade na súmula).</summary>
public sealed record RelacionadoNovo(Guid AtletaId, string Nome, string Numero, bool Titular);
