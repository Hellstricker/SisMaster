using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class AlterarInscricoesHabilitadasCommandHandler : IRequestHandler<AlterarInscricoesHabilitadasCommand, ValidationResult>
{
    private readonly ICampeonatoRepository _repository;
    private readonly IInscricaoRepository _inscricaoRepository;

    public AlterarInscricoesHabilitadasCommandHandler(ICampeonatoRepository repository, IInscricaoRepository inscricaoRepository)
    {
        _repository = repository;
        _inscricaoRepository = inscricaoRepository;
    }

    public async Task<ValidationResult> Handle(AlterarInscricoesHabilitadasCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var campeonato = await _repository.ObterPorIdAsync(request.CampeonatoId, cancellationToken);
            if (campeonato is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Campeonato não encontrado"));
                return request.ValidationResult;
            }

            var temporada = campeonato.ObterTemporada(request.TemporadaId);
            if (temporada is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Temporada não encontrada"));
                return request.ValidationResult;
            }

            var cobrancasGeradas = request.Habilitadas && await _inscricaoRepository.ExisteCobrancaGeradaAsync(temporada.Id, cancellationToken);
            temporada.AlterarInscricoesHabilitadas(request.Habilitadas, cobrancasGeradas);
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
