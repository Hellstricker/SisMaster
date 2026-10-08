using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Fases;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class DefinirDistribuicaoManualCommandHandler : IRequestHandler<DefinirDistribuicaoManualCommand, ValidationResult>
{
    private readonly IFaseRepository _repository;

    public DefinirDistribuicaoManualCommandHandler(IFaseRepository repository) => _repository = repository;

    public async Task<ValidationResult> Handle(DefinirDistribuicaoManualCommand request, CancellationToken cancellationToken)
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

            var temporada = fase.TemporadaCategoria.Temporada;
            temporada.ValidarPermiteCadastrarFases();
            if (temporada.TabelaJogosGerada)
                throw new DomainException("A tabela de jogos já foi gerada: os grupos não podem mais mudar");

            fase.DefinirDistribuicaoManual(request.Grupos);
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
