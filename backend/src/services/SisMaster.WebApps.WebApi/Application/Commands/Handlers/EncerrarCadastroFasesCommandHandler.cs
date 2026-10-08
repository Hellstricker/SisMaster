using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Fases;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class EncerrarCadastroFasesCommandHandler : IRequestHandler<EncerrarCadastroFasesCommand, ValidationResult>
{
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly IFaseRepository _faseRepository;

    public EncerrarCadastroFasesCommandHandler(ITemporadaRepository temporadaRepository, IFaseRepository faseRepository)
    {
        _temporadaRepository = temporadaRepository;
        _faseRepository = faseRepository;
    }

    public async Task<ValidationResult> Handle(EncerrarCadastroFasesCommand request, CancellationToken cancellationToken)
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

            var fases = await _faseRepository.ListarPorTemporadaAsync(temporada.Id, cancellationToken);

            var incompletas = fases.Where(f => !f.EstruturaCompleta).Select(f => f.Nome).ToList();
            if (incompletas.Count > 0)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty,
                    $"Complete a estrutura das fases antes de encerrar o cadastro: {string.Join(", ", incompletas)}"));
                return request.ValidationResult;
            }

            temporada.EncerrarCadastroFases(fases.Count > 0);
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
