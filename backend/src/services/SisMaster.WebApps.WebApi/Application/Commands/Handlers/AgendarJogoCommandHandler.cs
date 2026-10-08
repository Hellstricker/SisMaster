using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class AgendarJogoCommandHandler : IRequestHandler<AgendarJogoCommand, ValidationResult>
{
    private readonly IJogoRepository _jogoRepository;
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly ILocalRepository _localRepository;

    public AgendarJogoCommandHandler(IJogoRepository jogoRepository, ITemporadaRepository temporadaRepository, ILocalRepository localRepository)
    {
        _jogoRepository = jogoRepository;
        _temporadaRepository = temporadaRepository;
        _localRepository = localRepository;
    }

    public async Task<ValidationResult> Handle(AgendarJogoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var jogo = await _jogoRepository.ObterPorIdAsync(request.JogoId, cancellationToken);
            if (jogo is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Jogo não encontrado"));
                return request.ValidationResult;
            }

            var temporada = await _temporadaRepository.ObterPorIdAsync(jogo.TemporadaId, cancellationToken);
            if (temporada is null || temporada.Status == StatusTemporada.Encerrada)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Temporada encerrada não aceita alteração de jogos"));
                return request.ValidationResult;
            }

            if (request.LocalId is not null)
            {
                var local = await _localRepository.ObterPorIdAsync(request.LocalId.Value, cancellationToken);
                if (local is null || local.AssociacaoId != temporada.Campeonato.AssociacaoId)
                {
                    request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Local não encontrado nesta associação"));
                    return request.ValidationResult;
                }
            }

            jogo.Agendar(request.Data, request.Hora, request.LocalId);
            await _jogoRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
