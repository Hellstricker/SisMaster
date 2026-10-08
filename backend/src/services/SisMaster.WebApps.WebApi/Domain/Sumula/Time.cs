using System.Text.RegularExpressions;
using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Sumula;

public enum LadoTime
{
    Casa,
    Visitante
}

/// <summary>Limites da relação de jogadores (FIBA OBR 2024, FIMBA Art. 22 bis/65, regulamento FBBM Art. 23 e 29).</summary>
public static class RegrasDeRelacao
{
    /// <summary>No máximo 12 relacionados por jogo.</summary>
    public const int Maximo = 12;

    /// <summary>Um jogo não começa com menos de 4 relacionados (no master, os demais podem ser acrescentados depois do início).</summary>
    public const int MinimoParaIniciar = 4;

    public const int Titulares = 5;

    /// <summary>Em categoria com rodízio, abaixo de 7 relacionados a equipe não cumpre o rodízio e perde os pontos da vitória.</summary>
    public const int MinimoParaRodizio = 7;

    private static readonly Regex Camisa = new("^(0|00|[1-9][0-9]?)$", RegexOptions.Compiled);
    public static bool CamisaValida(string? numero) => numero is not null && Camisa.IsMatch(numero);
}

/// <summary>
/// A participação de uma Equipe em um Jogo, como o bloco de cada equipe na súmula oficial: comissão técnica, capitão
/// e os jogadores relacionados com a camisa do jogo. A identidade permanente continua sendo a Equipe.
/// </summary>
public class Time : Entity
{
    public Guid SumulaId { get; private set; }
    public LadoTime Lado { get; private set; }
    public Guid EquipeId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Cor { get; private set; }

    public string? Tecnico { get; private set; }
    public string? AuxiliarTecnico { get; private set; }
    public Guid? CapitaoId { get; private set; }

    private readonly List<Jogador> _jogadores = [];
    public IReadOnlyCollection<Jogador> Jogadores => _jogadores.AsReadOnly();

    protected Time() { }

    internal Time(Guid sumulaId, LadoTime lado, Guid equipeId, string nome, string? cor)
    {
        SumulaId = sumulaId;
        Lado = lado;
        EquipeId = equipeId;
        Nome = nome;
        Cor = cor;
    }

    /// <summary>Substitui a relação inteira (comissão, capitão e jogadores). Devolve o que saiu e o que entrou.</summary>
    internal (List<Jogador> Removidos, List<Jogador> Adicionados) SubstituirRelacao(
        string? tecnico, string? auxiliar, IReadOnlyList<RelacionadoNovo> relacionados, Guid? capitaoAtletaId)
    {
        Validar(tecnico, auxiliar, relacionados, capitaoAtletaId);

        var removidos = _jogadores.ToList();
        _jogadores.Clear();

        var adicionados = relacionados.Select(r => new Jogador(Id, r)).ToList();
        _jogadores.AddRange(adicionados);

        Tecnico = string.IsNullOrWhiteSpace(tecnico) ? null : tecnico.Trim();
        AuxiliarTecnico = string.IsNullOrWhiteSpace(auxiliar) ? null : auxiliar.Trim();
        CapitaoId = capitaoAtletaId is null ? null : adicionados.First(j => j.AtletaId == capitaoAtletaId).Id;
        return (removidos, adicionados);
    }

    /// <summary>
    /// Acrescenta um atleta depois do início do jogo (chegou atrasado). Entra no banco; quem decide se vai para a
    /// quadra é a substituição. Respeita o máximo, a camisa válida e única e a unicidade do atleta.
    /// </summary>
    internal Jogador AcrescentarJogador(RelacionadoNovo r, int periodo, bool noInicio)
    {
        if (_jogadores.Count >= RegrasDeRelacao.Maximo)
            throw new DomainException($"No máximo {RegrasDeRelacao.Maximo} jogadores podem ser relacionados");
        if (_jogadores.Any(j => j.AtletaId == r.AtletaId))
            throw new DomainException($"{r.Nome} já está relacionado nesta súmula");
        if (!RegrasDeRelacao.CamisaValida(r.Numero))
            throw new DomainException("Camisa inválida: use 0, 00 ou de 1 a 99");
        if (_jogadores.Any(j => j.Numero == r.Numero))
            throw new DomainException($"A camisa {r.Numero} já está em uso na equipe");

        var jogador = new Jogador(Id, r with { Titular = false }, periodo, noInicio);
        _jogadores.Add(jogador);
        return jogador;
    }

    private static void Validar(string? tecnico, string? auxiliar, IReadOnlyList<RelacionadoNovo> relacionados, Guid? capitaoAtletaId)
    {
        if (tecnico is { Length: > 100 } || auxiliar is { Length: > 100 })
            throw new DomainException("O nome do técnico deve ter até 100 caracteres");
        if (relacionados.Count > RegrasDeRelacao.Maximo)
            throw new DomainException($"No máximo {RegrasDeRelacao.Maximo} jogadores podem ser relacionados");
        if (relacionados.Select(r => r.AtletaId).Distinct().Count() != relacionados.Count)
            throw new DomainException("Um atleta não pode ser relacionado duas vezes");

        foreach (var r in relacionados)
            if (!RegrasDeRelacao.CamisaValida(r.Numero))
                throw new DomainException($"Camisa inválida para {r.Nome}: use 0, 00 ou de 1 a 99");

        var repetida = relacionados.GroupBy(r => r.Numero).FirstOrDefault(g => g.Count() > 1);
        if (repetida is not null)
            throw new DomainException($"A camisa {repetida.Key} está repetida na equipe");

        if (relacionados.Count(r => r.Titular) > RegrasDeRelacao.Titulares)
            throw new DomainException($"No máximo {RegrasDeRelacao.Titulares} titulares");
        if (capitaoAtletaId is not null && relacionados.All(r => r.AtletaId != capitaoAtletaId))
            throw new DomainException("O capitão precisa estar entre os relacionados");
    }

    /// <summary>O que impede de iniciar a súmula (vazio = pronta). Titulares e capitão são obrigatórios; técnico e auxiliar são opcionais.</summary>
    public IReadOnlyList<string> Pendencias()
    {
        var p = new List<string>();
        if (_jogadores.Count < RegrasDeRelacao.MinimoParaIniciar)
            p.Add($"mínimo de {RegrasDeRelacao.MinimoParaIniciar} relacionados");
        // Com 5 ou mais relacionados são 5 titulares; com 4, todos começam em quadra.
        var esperados = Math.Min(RegrasDeRelacao.Titulares, _jogadores.Count);
        var titulares = _jogadores.Count(j => j.Titular);
        if (_jogadores.Count >= RegrasDeRelacao.MinimoParaIniciar && titulares != esperados)
            p.Add(esperados == RegrasDeRelacao.Titulares
                ? $"marcar exatamente {RegrasDeRelacao.Titulares} titulares ({titulares})"
                : $"marcar os {esperados} relacionados como titulares ({titulares})");
        if (CapitaoId is null || _jogadores.All(j => j.Id != CapitaoId))
            p.Add("definir o capitão");
        return p;
    }
}
