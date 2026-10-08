using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class CriarCategoriaCommandHandler : IRequestHandler<CriarCategoriaCommand, ValidationResult>
{
    private readonly IAssociacaoRepository _associacaoRepository;
    private readonly ICategoriaRepository _categoriaRepository;

    public CriarCategoriaCommandHandler(IAssociacaoRepository associacaoRepository, ICategoriaRepository categoriaRepository)
    {
        _associacaoRepository = associacaoRepository;
        _categoriaRepository = categoriaRepository;
    }

    public async Task<ValidationResult> Handle(CriarCategoriaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var associacao = await _associacaoRepository.ObterPorIdAsync(request.AssociacaoId, cancellationToken);
            if (associacao is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Associação não encontrada"));
                return request.ValidationResult;
            }

            var nome = request.Nome.Trim();
            if (await _categoriaRepository.ExisteComNomeAsync(request.AssociacaoId, nome, cancellationToken))
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Já existe uma categoria com esse nome nesta associação"));
                return request.ValidationResult;
            }

            _categoriaRepository.Adicionar(new Categoria(request.AssociacaoId, nome, request.IdadeMinima, request.Sexo,
                request.MinimoPeriodosEmQuadra, request.MinimoPeriodosForaQuadra, request.AceitaAbaixoIdadeMinima));
            await _categoriaRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
