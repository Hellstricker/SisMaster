using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class EditarLocalCommandHandler : IRequestHandler<EditarLocalCommand, ValidationResult>
{
    private readonly ILocalRepository _repository;

    public EditarLocalCommandHandler(ILocalRepository repository) => _repository = repository;

    public async Task<ValidationResult> Handle(EditarLocalCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var local = await _repository.ObterPorIdAsync(request.LocalId, cancellationToken);
            if (local is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Local não encontrado"));
                return request.ValidationResult;
            }

            local.Editar(request.Nome, request.Cidade, request.Estado);
            if (await _repository.ExisteAsync(local.AssociacaoId, local.Nome, local.Cidade, local.Id, cancellationToken))
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Já existe um local com esse nome nessa cidade"));
                return request.ValidationResult;
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
}

public class ExcluirLocalCommandHandler : IRequestHandler<ExcluirLocalCommand, ValidationResult>
{
    private readonly ILocalRepository _repository;
    private readonly IJogoRepository _jogoRepository;

    public ExcluirLocalCommandHandler(ILocalRepository repository, IJogoRepository jogoRepository)
    {
        _repository = repository;
        _jogoRepository = jogoRepository;
    }

    public async Task<ValidationResult> Handle(ExcluirLocalCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        var local = await _repository.ObterPorIdAsync(request.LocalId, cancellationToken);
        if (local is null)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Local não encontrado"));
            return request.ValidationResult;
        }

        var usos = await _jogoRepository.ContarJogosNoLocalAsync(local.Id, cancellationToken);
        if (usos > 0)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty,
                $"Não é possível excluir: o local está em {usos} jogo(s). Troque o local desses jogos antes"));
            return request.ValidationResult;
        }

        _repository.Remover(local);
        await _repository.UnitOfWork.Commit();
        return request.ValidationResult;
    }
}
