using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class CriarEquipeCommandHandler : IRequestHandler<CriarEquipeCommand, ValidationResult>
{
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly IEquipeRepository _equipeRepository;

    public CriarEquipeCommandHandler(ITemporadaRepository temporadaRepository, IEquipeRepository equipeRepository)
    {
        _temporadaRepository = temporadaRepository;
        _equipeRepository = equipeRepository;
    }

    public async Task<ValidationResult> Handle(CriarEquipeCommand request, CancellationToken cancellationToken)
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

            temporada.ValidarPermiteFormarEquipes();

            var categoria = temporada.Categorias.FirstOrDefault(c => c.Id == request.TemporadaCategoriaId);
            if (categoria is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Categoria não pertence a esta temporada"));
                return request.ValidationResult;
            }

            if (await _equipeRepository.ExisteNomeNaTemporadaAsync(temporada.Id, request.Nome.Trim(), null, cancellationToken))
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Já existe uma equipe com esse nome nesta temporada"));
                return request.ValidationResult;
            }

            _equipeRepository.Adicionar(new Equipe(categoria.Id, request.Nome, request.Cor));
            await _equipeRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
