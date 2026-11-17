using FluentValidation.Results;
using SisMaster.Core.Communications;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class RemoverAtletaCommandHandler : IRequestHandler<RemoverAtletaCommand, ValidationResult>
{
    private readonly IEquipeRepository _repository;

    public RemoverAtletaCommandHandler(IEquipeRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(RemoverAtletaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var equipe = await _repository.ObterPorIdAsync(request.EquipeId, cancellationToken);
            if (equipe is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Equipe não encontrada"));
                return request.ValidationResult;
            }

            equipe.TemporadaCategoria.Temporada.ValidarPermiteFormarEquipes();

            if (await _repository.AtletaApareceEmSumulaAsync(request.AtletaId, cancellationToken))
                throw new DomainException("O atleta já consta na súmula de um jogo e não pode ser removido do elenco; transfira para outra equipe");

            var atleta = equipe.RemoverAtleta(request.AtletaId);
            _repository.RemoverAtleta(atleta);
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
