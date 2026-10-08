using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class ExcluirFaseCommandHandler : IRequestHandler<ExcluirFaseCommand, ValidationResult>
{
    private readonly IFaseRepository _repository;
    private readonly IJogoRepository _jogoRepository;

    public ExcluirFaseCommandHandler(IFaseRepository repository, IJogoRepository jogoRepository)
    {
        _repository = repository;
        _jogoRepository = jogoRepository;
    }

    public async Task<ValidationResult> Handle(ExcluirFaseCommand request, CancellationToken cancellationToken)
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

            if (await _repository.ExisteDependenteAsync(fase.Id, cancellationToken))
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Não é possível excluir: outra fase vem desta. Altere a fase seguinte antes"));
                return request.ValidationResult;
            }

            // Cruzamentos de mata-mata de outras fases que usam esta fase (ou os confrontos dela) como origem também bloqueiam.
            // Os confrontos da própria fase saem junto (cascata). Jogos só existem com o cadastro encerrado, que já bloqueia acima.
            var confrontos = await _jogoRepository.ListarConfrontosDaFaseAsync(fase.Id, cancellationToken);
            if (await _jogoRepository.ExisteReferenciaAFaseAsync(fase.Id, cancellationToken)
                || await _jogoRepository.ExisteReferenciaAosConfrontosAsync(confrontos.Select(c => c.Id).ToList(), cancellationToken))
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Não é possível excluir: um cruzamento de mata-mata usa esta fase como origem. Altere o cruzamento antes"));
                return request.ValidationResult;
            }

            var categoria = fase.TemporadaCategoriaId;
            _repository.Remover(fase);

            var restantes = (await _repository.ListarPorTemporadaCategoriaAsync(categoria, cancellationToken))
                .Where(f => f.Id != fase.Id).ToList();
            SequenciaDeFases.Renumerar(restantes);

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
