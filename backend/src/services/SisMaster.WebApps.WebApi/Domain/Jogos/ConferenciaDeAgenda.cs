namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>Um jogo com o que importa para conferir a agenda: quando, onde e quais equipes (nulas enquanto a vaga não for resolvida).</summary>
public sealed record JogoNaAgenda(
    Guid Id, Guid TemporadaId, int Numero, Guid? CasaEquipeId, Guid? VisitanteEquipeId,
    DateOnly? Data, TimeOnly? Hora, Guid? LocalId, string? LocalNome);

/// <summary>Quando e onde um jogo passaria a acontecer.</summary>
public sealed record AgendaProposta(Guid JogoId, DateOnly? Data, TimeOnly? Hora, Guid? LocalId);

public sealed record PessoaDoElenco(Guid PessoaId, string Nome);

/// <summary>Nomes das equipes e as pessoas do elenco de cada uma, da temporada.</summary>
public sealed record ElencosDaTemporada(
    IReadOnlyDictionary<Guid, string> NomesDasEquipes,
    IReadOnlyDictionary<Guid, IReadOnlyList<PessoaDoElenco>> PessoasPorEquipe);

/// <summary>Bloqueios impedem salvar; avisos só informam.</summary>
public sealed record ResultadoDaConferencia(IReadOnlyList<string> Bloqueios, IReadOnlyList<string> Avisos)
{
    public bool Ok => Bloqueios.Count == 0;
    public static readonly ResultadoDaConferencia Vazio = new([], []);
}

/// <summary>
/// Conflitos de agenda. A duração presumida de um jogo é <see cref="DuracaoMinutos"/>: dois jogos na mesma data com menos
/// que isso entre os inícios se sobrepõem. BLOQUEIAM: o mesmo local ocupado e a mesma equipe em dois jogos. AVISA: o mesmo
/// atleta no elenco de duas equipes que jogam sobrepostas (categorias diferentes). Equipe ainda indefinida não entra na conta.
/// </summary>
public static class ConferenciaDeAgenda
{
    public const int DuracaoMinutos = 90;

    /// <param name="propostos">Os jogos como ficariam depois de agendados (precisam ter data e hora para serem conferidos).</param>
    /// <param name="existentes">Os demais jogos já agendados e ainda não terminados (não inclui os propostos).</param>
    /// <param name="descrever">Texto do jogo para as mensagens, p.ex. "#3 Águias × Ursos".</param>
    public static ResultadoDaConferencia Conferir(
        IReadOnlyList<JogoNaAgenda> propostos, IReadOnlyList<JogoNaAgenda> existentes,
        ElencosDaTemporada elencos, Func<JogoNaAgenda, string> descrever)
    {
        var bloqueios = new List<string>();
        var avisos = new List<string>();

        string Nome(Guid id) => elencos.NomesDasEquipes.GetValueOrDefault(id, "Equipe");

        for (var i = 0; i < propostos.Count; i++)
        {
            var p = propostos[i];
            if (p.Data is null || p.Hora is null) continue;

            // contra os que já existiam e contra os outros propostos (cada par uma vez)
            var outros = existentes.Concat(propostos.Skip(i + 1));
            foreach (var o in outros)
            {
                if (o.Id == p.Id || !Sobrepoe(p, o)) continue;

                if (p.LocalId is not null && p.LocalId == o.LocalId)
                    bloqueios.Add($"Local ocupado: {(p.LocalNome ?? o.LocalNome) ?? "o local"} às {o.Hora:HH\\:mm} ({descrever(o)}). Jogos no mesmo local precisam de ao menos {DuracaoMinutos} min entre os inícios.");

                var equipesDeP = Equipes(p);
                var equipesDeO = Equipes(o);
                foreach (var equipe in equipesDeP.Intersect(equipesDeO))
                    bloqueios.Add($"{Nome(equipe)} já joga às {o.Hora:HH\\:mm} ({descrever(o)}): menos de {DuracaoMinutos} min entre os dois jogos.");

                foreach (var (pessoa, equipeP, equipeO) in PessoasEmDuasEquipes(equipesDeP, equipesDeO, elencos))
                    avisos.Add($"{pessoa} está no elenco de {Nome(equipeP)} e de {Nome(equipeO)}, com jogos às {p.Hora:HH\\:mm} ({descrever(p)}) e às {o.Hora:HH\\:mm} ({descrever(o)}).");
            }
        }

        return new ResultadoDaConferencia(bloqueios.Distinct().ToList(), avisos.Distinct().ToList());
    }

    private static bool Sobrepoe(JogoNaAgenda a, JogoNaAgenda b) =>
        a.Data is not null && a.Data == b.Data && a.Hora is not null && b.Hora is not null
        && Math.Abs((a.Hora.Value.ToTimeSpan() - b.Hora.Value.ToTimeSpan()).TotalMinutes) < DuracaoMinutos;

    private static List<Guid> Equipes(JogoNaAgenda j) =>
        new[] { j.CasaEquipeId, j.VisitanteEquipeId }.Where(e => e is not null).Select(e => e!.Value).Distinct().ToList();

    private static IEnumerable<(string Pessoa, Guid EquipeP, Guid EquipeO)> PessoasEmDuasEquipes(
        List<Guid> equipesDeP, List<Guid> equipesDeO, ElencosDaTemporada elencos)
    {
        foreach (var ep in equipesDeP)
        {
            if (equipesDeO.Contains(ep)) continue; // a mesma equipe já é bloqueio
            foreach (var eo in equipesDeO)
            {
                var doP = elencos.PessoasPorEquipe.GetValueOrDefault(ep) ?? [];
                var doO = elencos.PessoasPorEquipe.GetValueOrDefault(eo) ?? [];
                foreach (var pessoa in doP.Where(x => doO.Any(y => y.PessoaId == x.PessoaId)))
                    yield return (pessoa.Nome, ep, eo);
            }
        }
    }
}
