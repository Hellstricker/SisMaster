using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class RegistrarPagamentoSaldoCommandHandler : IRequestHandler<RegistrarPagamentoSaldoCommand, ValidationResult>
{
    private readonly IInscricaoRepository _inscricaoRepository;

    public RegistrarPagamentoSaldoCommandHandler(IInscricaoRepository inscricaoRepository)
    {
        _inscricaoRepository = inscricaoRepository;
    }

    public async Task<ValidationResult> Handle(RegistrarPagamentoSaldoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var ficha = await _inscricaoRepository.ObterPorIdAsync(request.InscricaoId, cancellationToken);
            if (ficha is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Inscrição não encontrada"));
                return request.ValidationResult;
            }

            var pagamento = ficha.RegistrarPagamentoSaldo(request.Valor, request.DataPagamento);
            _inscricaoRepository.AdicionarPagamento(pagamento);
            await _inscricaoRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
