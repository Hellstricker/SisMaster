using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Time;
using SisMaster.WebApps.WebApi.Domain.Time.VOs;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class AdicionarJogadorCommandHandler : IRequestHandler<AdicionarJogadorCommand, ValidationResult>
{
    private readonly ITimeRepository _timeRepository;

    public AdicionarJogadorCommandHandler(ITimeRepository timeRepository)
    {
        _timeRepository = timeRepository;
    }

    public async Task<ValidationResult> Handle(AdicionarJogadorCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        var time = await _timeRepository.ObterPorIdAsync(request.TimeId, cancellationToken);
        if (time is null)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure("TimeId", "Time não encontrado"));
            return request.ValidationResult;
        }

        try
        {
            var cpfLimpo = Cpf.RemoverFormatacao(request.Cpf);
            var pessoa = new Pessoa(request.Nome, cpfLimpo);
            time.AdicionarJogador(request.Numero, pessoa);

            _timeRepository.Atualizar(time);
            await _timeRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
