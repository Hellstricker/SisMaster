using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class CriarFaseCommandHandler : IRequestHandler<CriarFaseCommand, ValidationResult>
{
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly IFaseRepository _faseRepository;
    private readonly IEquipeRepository _equipeRepository;

    public CriarFaseCommandHandler(ITemporadaRepository temporadaRepository, IFaseRepository faseRepository, IEquipeRepository equipeRepository)
    {
        _temporadaRepository = temporadaRepository;
        _faseRepository = faseRepository;
        _equipeRepository = equipeRepository;
    }

    public async Task<ValidationResult> Handle(CriarFaseCommand request, CancellationToken cancellationToken)
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

            temporada.ValidarCadastroFasesAberto();

            var categoria = temporada.Categorias.FirstOrDefault(c => c.Id == request.TemporadaCategoriaId);
            if (categoria is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Categoria não pertence a esta temporada"));
                return request.ValidationResult;
            }

            var fases = await _faseRepository.ListarPorTemporadaCategoriaAsync(categoria.Id, cancellationToken);
            if (request.FaseAnteriorId is not null)
                SequenciaDeFases.ObterAnterior(fases, request.FaseAnteriorId.Value, null);

            // A fase nova entra no fim da sequência; então qualquer fase existente da categoria já fica antes dela.
            // Equipes da categoria: base para validar grupos e classificados (0 = equipes ainda não formadas).
            var equipes = (await _equipeRepository.ContarPorCategoriaAsync(temporada.Id, cancellationToken)).GetValueOrDefault(categoria.Id);

            var fase = new Fase(categoria.Id, SequenciaDeFases.ProximaOrdem(fases), request.Nome, request.Tipo,
                request.Estrutura(), request.FaseAnteriorId, equipes);

            _faseRepository.Adicionar(fase);
            await _faseRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
