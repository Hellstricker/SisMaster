using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.WebApi.Domain.Sumula;

/// <summary>Lance de um jogo já realizado, vindo de fora (ex.: FIBA LiveStats). <see cref="TempoJogoSegundos"/> é o que falta no período.</summary>
public sealed record EventoImportado(Guid JogadorId, TipoEvento Tipo, Periodo Periodo, int TempoJogoSegundos);

/// <summary>Troca de um jogo já realizado. <see cref="JogadorSaiId"/> nulo = o jogador só entra (quadra com menos de 5).</summary>
public sealed record TrocaImportada(Guid? JogadorSaiId, Guid JogadorEntraId, Periodo Periodo, int TempoJogoSegundos);

/// <summary>O que a importação tirou e pôs na súmula, para o repositório aplicar com Remove/Add explícitos.</summary>
public sealed record ResultadoImportacao(
    IReadOnlyList<EventoSumula> EventosRemovidos, IReadOnlyList<Substituicao> SubstituicoesRemovidas,
    IReadOnlyList<EventoSumula> EventosNovos, IReadOnlyList<Substituicao> SubstituicoesNovas);
