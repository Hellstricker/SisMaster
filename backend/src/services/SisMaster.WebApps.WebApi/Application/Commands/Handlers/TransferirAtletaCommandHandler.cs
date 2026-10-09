using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Equipes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class TransferirAtletaCommandHandler : IRequestHandler<TransferirAtletaCommand, ValidationResult>
{
    private readonly IEquipeRepository _repository;

    public TransferirAtletaCommandHandler(IEquipeRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(TransferirAtletaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var origem = await _repository.ObterPorIdAsync(request.EquipeId, cancellationToken);
            var destino = await _repository.ObterPorIdAsync(request.EquipeDestinoId, cancellationToken);
            if (origem is null || destino is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Equipe não encontrada"));
                return request.ValidationResult;
            }

            origem.TemporadaCategoria.Temporada.ValidarPermiteFormarEquipes();

            // O vínculo muda de equipe sem perder a identidade: os jogos já feitos continuam na equipe
            // registrada na súmula. Relação de jogo ainda não encerrado precisa ser refeita antes.
            await ValidarSemSumulaEmAbertoAsync(request.AtletaId, cancellationToken);
            if (request.AtletaTrocaId is { } trocaId)
            {
                await ValidarSemSumulaEmAbertoAsync(trocaId, cancellationToken);
                origem.TrocarAtletaCom(destino, request.AtletaId, trocaId);
            }
            else
            {
                origem.TransferirAtletaPara(destino, request.AtletaId);
            }

            await _repository.UnitOfWork.Commit();
            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }

    private async Task ValidarSemSumulaEmAbertoAsync(Guid atletaId, CancellationToken ct)
    {
        if (await _repository.AtletaEstaEmSumulaEmAbertoAsync(atletaId, ct))
            throw new DomainException("O atleta está na relação de um jogo que ainda não terminou. Refaça a relação desse jogo (ou encerre-o) antes de transferir");
    }
}
