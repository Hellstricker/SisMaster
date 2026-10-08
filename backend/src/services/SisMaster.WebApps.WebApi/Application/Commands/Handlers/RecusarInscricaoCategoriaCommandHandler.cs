using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class RecusarInscricaoCategoriaCommandHandler : IRequestHandler<RecusarInscricaoCategoriaCommand, ValidationResult>
{
    private readonly IInscricaoRepository _repository;

    public RecusarInscricaoCategoriaCommandHandler(IInscricaoRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(RecusarInscricaoCategoriaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var ficha = await _repository.ObterPorCategoriaIdAsync(request.InscricaoCategoriaId, cancellationToken);
            if (ficha is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Inscrição não encontrada"));
                return request.ValidationResult;
            }

            ficha.Categorias.First(c => c.Id == request.InscricaoCategoriaId).Recusar(request.Motivo);
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
