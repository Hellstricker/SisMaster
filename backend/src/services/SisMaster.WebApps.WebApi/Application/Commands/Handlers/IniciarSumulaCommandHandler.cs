using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class IniciarSumulaCommandHandler : IRequestHandler<IniciarSumulaCommand, ValidationResult>
{
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IJogoRepository _jogoRepository;

    public IniciarSumulaCommandHandler(ISumulaRepository sumulaRepository, IJogoRepository jogoRepository)
    {
        _sumulaRepository = sumulaRepository;
        _jogoRepository = jogoRepository;
    }

    public async Task<ValidationResult> Handle(IniciarSumulaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var sumula = await _sumulaRepository.ObterPorJogoAsync(request.JogoId, cancellationToken);
            var jogo = await _jogoRepository.ObterPorIdAsync(request.JogoId, cancellationToken);
            if (sumula is null || jogo is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Este jogo ainda não tem súmula: comece a prepará-la antes"));
                return request.ValidationResult;
            }

            // Jogo e súmula mudam juntos, na mesma transação.
            sumula.Iniciar();
            jogo.IniciarPelaSumula(sumula.Id);
            await _sumulaRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
