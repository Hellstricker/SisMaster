using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class AgendarJogosEmLoteCommandHandler : IRequestHandler<AgendarJogosEmLoteCommand, ValidationResult>
{
    private readonly IJogoRepository _jogoRepository;
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly ILocalRepository _localRepository;

    public AgendarJogosEmLoteCommandHandler(IJogoRepository jogoRepository, ITemporadaRepository temporadaRepository, ILocalRepository localRepository)
    {
        _jogoRepository = jogoRepository;
        _temporadaRepository = temporadaRepository;
        _localRepository = localRepository;
    }

    public async Task<ValidationResult> Handle(AgendarJogosEmLoteCommand request, CancellationToken cancellationToken)
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
            if (temporada.Status == StatusTemporada.Encerrada)
                throw new DomainException("Temporada encerrada não aceita alteração de jogos");

            if (request.LocalId is not null)
            {
                var local = await _localRepository.ObterPorIdAsync(request.LocalId.Value, cancellationToken);
                if (local is null || local.AssociacaoId != temporada.Campeonato.AssociacaoId)
                    throw new DomainException("Local não encontrado nesta associação");
            }

            var ids = request.JogoIds.Distinct().ToList();
            var encontrados = await _jogoRepository.ListarPorIdsAsync(temporada.Id, ids, cancellationToken);
            if (encontrados.Count != ids.Count)
                throw new DomainException("Algum jogo selecionado não pertence a esta temporada");

            // A ordem pedida define os horários (não a numeração dos jogos).
            var jogos = ids.Select(id => encontrados.First(j => j.Id == id)).ToList();

            var inicio = request.HoraInicial.ToTimeSpan();
            for (var i = 0; i < jogos.Count; i++)
            {
                var hora = inicio + TimeSpan.FromMinutes(request.IntervaloMinutos * i);
                if (hora >= TimeSpan.FromDays(1))
                    throw new DomainException("Os horários passam da meia-noite: reduza o intervalo ou o número de jogos");
                jogos[i].Agendar(request.Data, TimeOnly.FromTimeSpan(hora), request.LocalId);
            }

            await _jogoRepository.UnitOfWork.Commit();
            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
