using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class RecusarInscricaoCategoriaCommandHandler : IRequestHandler<RecusarInscricaoCategoriaCommand, ValidationResult>
{
    private readonly IInscricaoRepository _repository;
    private readonly AvisosDeInscricao _avisos;

    public RecusarInscricaoCategoriaCommandHandler(IInscricaoRepository repository, AvisosDeInscricao avisos)
    {
        _repository = repository;
        _avisos = avisos;
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

            var pedido = ficha.Categorias.First(c => c.Id == request.InscricaoCategoriaId);
            pedido.Recusar(request.Motivo);
            await _repository.UnitOfWork.Commit();
            await _avisos.RecusadaAsync(ficha, pedido, request.AvisarPorEmail, cancellationToken);

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
