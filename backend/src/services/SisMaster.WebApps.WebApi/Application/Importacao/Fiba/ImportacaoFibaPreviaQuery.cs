using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Application.Importacao.Fiba;

/// <summary>A súmula, o jogo do feed já traduzido e a conferência de um e de outro.</summary>
public sealed record AnaliseFiba(Sumula Sumula, string Codigo, string JsonBruto, FibaTraducao Traducao, PreviaImportacaoFiba Previa);

/// <summary>Prévia da importação do FIBA LiveStats numa súmula: baixa o feed, traduz e confere — não grava nada.</summary>
public class ImportacaoFibaPreviaQuery
{
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IFibaLiveStatsClient _fiba;

    public ImportacaoFibaPreviaQuery(ISumulaRepository sumulaRepository, IFibaLiveStatsClient fiba)
    {
        _sumulaRepository = sumulaRepository;
        _fiba = fiba;
    }

    /// <summary>Nulo = súmula não encontrada. Lança <see cref="ArgumentException"/> se o código for inválido.</summary>
    public async Task<PreviaImportacaoFiba?> ExecutarAsync(Guid sumulaId, string? codigo, bool inverterLados, CancellationToken ct) =>
        (await AnalisarAsync(sumulaId, codigo, inverterLados, ct))?.Previa;

    /// <summary>Como <see cref="ExecutarAsync"/>, mas devolve também a súmula e a tradução (para quem vai aplicar).</summary>
    public async Task<AnaliseFiba?> AnalisarAsync(Guid sumulaId, string? codigoInformado, bool inverterLados, CancellationToken ct)
    {
        var sumula = await _sumulaRepository.ObterPorIdAsync(sumulaId, ct);
        if (sumula is null) return null;

        var codigo = FibaLiveStatsClient.NormalizarCodigo(codigoInformado)
            ?? throw new ArgumentException("Informe o código do jogo no LiveStats (só números, ex.: 2888968)");

        var feed = await _fiba.ObterJogoAsync(codigo, ct);
        var jogo = feed.Jogo;
        var traducao = FibaTradutor.Traduzir(jogo, inverterLados);

        var relacao = sumula.Times
            .SelectMany(t => t.Jogadores.Select(j => new RelacionadoDaSumula(t.Lado, j.Numero, j.Nome, j.Titular)))
            .ToList();
        var nomes = sumula.Times.ToDictionary(t => t.Lado, t => t.Nome);

        return new AnaliseFiba(sumula, codigo, feed.Json, traducao, FibaAnalisador.Analisar(jogo, traducao, relacao, nomes, inverterLados));
    }
}
