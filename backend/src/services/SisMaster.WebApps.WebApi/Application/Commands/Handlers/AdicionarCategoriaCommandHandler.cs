using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class AdicionarCategoriaCommandHandler : IRequestHandler<AdicionarCategoriaCommand, ValidationResult>
{
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly ICategoriaRepository _categoriaRepository;

    public AdicionarCategoriaCommandHandler(ITemporadaRepository temporadaRepository, ICategoriaRepository categoriaRepository)
    {
        _temporadaRepository = temporadaRepository;
        _categoriaRepository = categoriaRepository;
    }

    public async Task<ValidationResult> Handle(AdicionarCategoriaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var temporada = await _temporadaRepository.ObterPorIdAsync(request.TemporadaId, cancellationToken);
            if (temporada is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Temporada não encontrada"));
                return request.ValidationResult;
            }

            var categoria = await _categoriaRepository.ObterPorIdAsync(request.CategoriaId, cancellationToken);
            if (categoria is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Categoria não encontrada"));
                return request.ValidationResult;
            }

            if (categoria.AssociacaoId != temporada.Campeonato.AssociacaoId)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Categoria não pertence à associação do campeonato desta temporada"));
                return request.ValidationResult;
            }

            var tc = temporada.AdicionarCategoria(categoria);
            _temporadaRepository.AdicionarCategoria(tc);
            await _temporadaRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
