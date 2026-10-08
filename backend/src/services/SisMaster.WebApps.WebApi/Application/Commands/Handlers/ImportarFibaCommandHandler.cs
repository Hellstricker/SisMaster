using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Importacao.Fiba;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Hubs;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class ImportarFibaCommandHandler : IRequestHandler<ImportarFibaCommand, ValidationResult>
{
    private readonly ImportacaoFibaPreviaQuery _previa;
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly AvancoDaTemporada _avanco;
    private readonly IHubContext<SumulaHub> _hub;

    public ImportarFibaCommandHandler(ImportacaoFibaPreviaQuery previa, ISumulaRepository sumulaRepository,
        IJogoRepository jogoRepository, AvancoDaTemporada avanco, IHubContext<SumulaHub> hub)
    {
        _previa = previa;
        _sumulaRepository = sumulaRepository;
        _jogoRepository = jogoRepository;
        _avanco = avanco;
        _hub = hub;
    }

    public async Task<ValidationResult> Handle(ImportarFibaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        void Falha(string mensagem) => request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, mensagem));

        try
        {
            AnaliseFiba? analise;
            try
            {
                analise = await _previa.AnalisarAsync(request.SumulaId, request.Codigo, request.InverterLados, cancellationToken);
            }
            catch (ArgumentException ex) { Falha(ex.Message); return request.ValidationResult; }
            catch (HttpRequestException) { Falha("Não foi possível obter o jogo no FIBA LiveStats — confira o código e tente de novo"); return request.ValidationResult; }

            if (analise is null) { Falha("Súmula não encontrada"); return request.ValidationResult; }

            // Só aplica o que a conferência aprovou: o que foi reconstruído bate com os números oficiais do feed.
            if (!analise.Previa.PodeAplicar)
            {
                foreach (var problema in analise.Previa.Problemas) Falha(problema);
                return request.ValidationResult;
            }

            var sumula = analise.Sumula;
            if (await _sumulaRepository.ExisteOutraComCodigoExternoAsync(analise.Codigo, sumula.Id, cancellationToken))
            {
                Falha($"O jogo {analise.Codigo} do LiveStats já foi importado em outra súmula");
                return request.ValidationResult;
            }

            var jogo = await _jogoRepository.ObterPorIdAsync(sumula.JogoId, cancellationToken)
                ?? throw new DomainException("Jogo da súmula não encontrado");

            // camisa do feed → jogador relacionado (a conferência já garantiu que todas existem)
            Guid Jogador(LadoTime lado, string camisa) => sumula.TimeDoLado(lado).Jogadores.First(j => j.Numero == camisa).Id;

            var eventos = analise.Traducao.Eventos
                .Select(e => new EventoImportado(Jogador(e.Lado, e.Camisa), e.Tipo, e.Periodo, e.TempoRestanteSegundos)).ToList();
            var trocas = analise.Traducao.Trocas
                .Select(t => new TrocaImportada(t.CamisaSai is null ? null : Jogador(t.Lado, t.CamisaSai), Jogador(t.Lado, t.CamisaEntra), t.Periodo, t.TempoRestanteSegundos)).ToList();

            // Súmula, jogo e classificação mudam juntos, na mesma transação.
            var resultado = sumula.ImportarJogoRealizado(analise.Codigo, eventos, trocas);
            _sumulaRepository.AplicarImportacao(resultado);
            await _sumulaRepository.GuardarDadosExternosAsync(sumula.Id, analise.JsonBruto, cancellationToken);
            jogo.ReceberPlacarImportado(sumula.Id, sumula.PlacarCasa, sumula.PlacarVisitante);
            await _avanco.ExecutarAsync(jogo.TemporadaId, cancellationToken);
            await _sumulaRepository.UnitOfWork.Commit();

            await _hub.Clients.Group($"partida-{sumula.Id}")
                .SendAsync("SumulaEncerrada", new { PartidaId = sumula.Id, sumula.PlacarCasa, sumula.PlacarVisitante }, cancellationToken);
            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            Falha(ex.Message);
            return request.ValidationResult;
        }
    }
}
