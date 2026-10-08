using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Fases;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class MoverFaseCommandHandler : IRequestHandler<MoverFaseCommand, ValidationResult>
{
    private readonly IFaseRepository _repository;

    public MoverFaseCommandHandler(IFaseRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(MoverFaseCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var fase = await _repository.ObterPorIdAsync(request.FaseId, cancellationToken);
            if (fase is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Fase não encontrada"));
                return request.ValidationResult;
            }

            fase.TemporadaCategoria.Temporada.ValidarCadastroFasesAberto();

            var fases = await _repository.ListarPorTemporadaCategoriaAsync(fase.TemporadaCategoriaId, cancellationToken);
            SequenciaDeFases.Mover(fases, fase.Id, request.Direcao);
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
