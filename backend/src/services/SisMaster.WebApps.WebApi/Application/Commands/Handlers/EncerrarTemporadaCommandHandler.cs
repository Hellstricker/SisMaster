using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class EncerrarTemporadaCommandHandler : IRequestHandler<EncerrarTemporadaCommand, ValidationResult>
{
    private readonly ICampeonatoRepository _repository;
    private readonly IJogoRepository _jogoRepository;

    public EncerrarTemporadaCommandHandler(ICampeonatoRepository repository, IJogoRepository jogoRepository)
    {
        _repository = repository;
        _jogoRepository = jogoRepository;
    }

    public async Task<ValidationResult> Handle(EncerrarTemporadaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var campeonato = await _repository.ObterPorIdAsync(request.CampeonatoId, cancellationToken);
            if (campeonato is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Campeonato não encontrado"));
                return request.ValidationResult;
            }

            var temporada = campeonato.ObterTemporada(request.TemporadaId);
            if (temporada is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Temporada não encontrada"));
                return request.ValidationResult;
            }

            var emAberto = await _jogoRepository.ContarEmAbertoDaTemporadaAsync(temporada.Id, cancellationToken);
            temporada.Encerrar(emAberto);
            await _repository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
