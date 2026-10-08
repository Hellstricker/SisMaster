using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class AdicionarAtletaCommandHandler : IRequestHandler<AdicionarAtletaCommand, ValidationResult>
{
    private readonly IEquipeRepository _equipeRepository;
    private readonly IInscricaoRepository _inscricaoRepository;

    public AdicionarAtletaCommandHandler(IEquipeRepository equipeRepository, IInscricaoRepository inscricaoRepository)
    {
        _equipeRepository = equipeRepository;
        _inscricaoRepository = inscricaoRepository;
    }

    public async Task<ValidationResult> Handle(AdicionarAtletaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var equipe = await _equipeRepository.ObterPorIdAsync(request.EquipeId, cancellationToken);
            if (equipe is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Equipe não encontrada"));
                return request.ValidationResult;
            }

            equipe.TemporadaCategoria.Temporada.ValidarPermiteFormarEquipes();

            var ficha = await _inscricaoRepository.ObterPorCategoriaIdAsync(request.InscricaoCategoriaId, cancellationToken);
            var pedido = ficha?.Categorias.FirstOrDefault(c => c.Id == request.InscricaoCategoriaId);
            if (ficha is null || pedido is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Inscrição não encontrada"));
                return request.ValidationResult;
            }

            if (pedido.Status != StatusInscricaoCategoria.Efetivada)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Só é possível vincular a uma equipe uma inscrição efetivada (aprovada e com a taxa paga)"));
                return request.ValidationResult;
            }

            // Lacuna do modelo de referência: aqui o atleta só entra em equipe da MESMA categoria do pedido.
            if (pedido.TemporadaCategoriaId != equipe.TemporadaCategoriaId)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "A inscrição é de outra categoria: o atleta só pode entrar em equipe da sua categoria"));
                return request.ValidationResult;
            }

            if (await _equipeRepository.AtletaExisteParaAsync(pedido.Id, cancellationToken))
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Este atleta já está em uma equipe"));
                return request.ValidationResult;
            }

            var atleta = equipe.AdicionarAtleta(pedido.Id);
            _equipeRepository.AdicionarAtleta(atleta);
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
