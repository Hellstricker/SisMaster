using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class RemoverCategoriaDaTemporadaCommandHandler : IRequestHandler<RemoverCategoriaDaTemporadaCommand, ValidationResult>
{
    private readonly ITemporadaRepository _repository;

    public RemoverCategoriaDaTemporadaCommandHandler(ITemporadaRepository repository) => _repository = repository;

    public async Task<ValidationResult> Handle(RemoverCategoriaDaTemporadaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var temporada = await _repository.ObterPorIdAsync(request.TemporadaId, cancellationToken);
            if (temporada is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Temporada não encontrada"));
                return request.ValidationResult;
            }

            var vinculo = temporada.Categorias.FirstOrDefault(c => c.CategoriaId == request.CategoriaId)
                ?? throw new DomainException("Categoria não está vinculada a esta temporada");

            var uso = await _repository.ObterUsoDaCategoriaAsync(vinculo.Id, cancellationToken);
            if (uso.Pedidos > 0)
                throw new DomainException($"Não é possível remover: a categoria tem {uso.Pedidos} pedido(s) de inscrição (os recusados ficam como histórico)");
            if (uso.Equipes > 0)
                throw new DomainException($"Não é possível remover: a categoria tem {uso.Equipes} equipe(s) formada(s)");
            if (uso.Fases > 0)
                throw new DomainException($"Não é possível remover: a categoria tem {uso.Fases} fase(s) cadastrada(s)");

            var removida = temporada.RemoverCategoria(request.CategoriaId);
            _repository.RemoverCategoria(removida);
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
