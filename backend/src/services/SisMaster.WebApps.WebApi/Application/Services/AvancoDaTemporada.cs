using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.WebApi.Application.Services;

/// <summary>
/// Chamado quando um jogo termina (súmula encerrada ou W.O.), antes do commit: carrega a temporada, deixa o
/// <see cref="ServicoAvanco"/> dispensar jogos de séries decididas e resolver as vagas das fases seguintes, e
/// descarta as súmulas preparadas dos jogos dispensados. A gravação fica com o commit de quem chamou.
/// </summary>
public class AvancoDaTemporada
{
    private readonly IJogoRepository _jogoRepository;
    private readonly ISumulaRepository _sumulaRepository;
    private readonly BonificacaoDosJogos _bonificacao;

    public AvancoDaTemporada(IJogoRepository jogoRepository, ISumulaRepository sumulaRepository, BonificacaoDosJogos bonificacao)
    {
        _jogoRepository = jogoRepository;
        _sumulaRepository = sumulaRepository;
        _bonificacao = bonificacao;
    }

    public async Task<ResultadoAvanco> ExecutarAsync(Guid temporadaId, CancellationToken ct)
    {
        var dados = await _jogoRepository.CarregarParaAvancoAsync(temporadaId, ct);
        var bonificacoes = (await _bonificacao.CalcularAsync(dados.Jogos, dados.Fases, ct))
            .ToDictionary(b => b.Key, b => (b.Value.Casa, b.Value.Visitante));
        var resultado = ServicoAvanco.Avancar(dados.Fases, dados.Grupos, dados.Vagas, dados.Confrontos, dados.Jogos, bonificacoes, dados.Sorteios);

        foreach (var jogo in resultado.Dispensados)
        {
            var sumula = await _sumulaRepository.ObterPorJogoAsync(jogo.Id, ct);
            if (sumula is not null && sumula.Status == StatusSumula.EmPreparacao) _sumulaRepository.Remover(sumula);
        }
        return resultado;
    }
}
