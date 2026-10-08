using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class ExcluirEquipeCommandHandler : IRequestHandler<ExcluirEquipeCommand, ValidationResult>
{
    private readonly IEquipeRepository _repository;

    public ExcluirEquipeCommandHandler(IEquipeRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(ExcluirEquipeCommand request, CancellationToken cancellationToken)
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

            if (equipe.TemporadaCategoria.Temporada.TabelaJogosGerada)
                throw new DomainException("Não é possível excluir: a tabela de jogos da temporada já foi gerada");

            // O elenco sai junto (os atletas voltam para "sem equipe").
            _repository.Remover(equipe);
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
