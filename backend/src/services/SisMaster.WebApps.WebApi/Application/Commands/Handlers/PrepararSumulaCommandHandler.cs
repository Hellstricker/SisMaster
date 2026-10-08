using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class PrepararSumulaCommandHandler : IRequestHandler<PrepararSumulaCommand, ValidationResult>
{
    private readonly IJogoRepository _jogoRepository;
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly ITemporadaRepository _temporadaRepository;

    public PrepararSumulaCommandHandler(IJogoRepository jogoRepository, ISumulaRepository sumulaRepository,
        IEquipeRepository equipeRepository, ITemporadaRepository temporadaRepository)
    {
        _jogoRepository = jogoRepository;
        _sumulaRepository = sumulaRepository;
        _equipeRepository = equipeRepository;
        _temporadaRepository = temporadaRepository;
    }

    public async Task<ValidationResult> Handle(PrepararSumulaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var jogo = await _jogoRepository.ObterPorIdAsync(request.JogoId, cancellationToken);
            if (jogo is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Jogo não encontrado"));
                return request.ValidationResult;
            }

            var temporada = await _temporadaRepository.ObterPorIdAsync(jogo.TemporadaId, cancellationToken);
            if (temporada is null || temporada.Status == StatusTemporada.Encerrada)
                throw new DomainException("Temporada encerrada não aceita alteração de jogos");

            if (jogo.Status != StatusJogo.Agendado)
                throw new DomainException("Só é possível preparar a súmula de um jogo agendado");
            if (await _sumulaRepository.ExisteParaJogoAsync(jogo.Id, cancellationToken))
                throw new DomainException("Este jogo já tem súmula");
            if (jogo.Casa.EquipeId is null || jogo.Visitante.EquipeId is null)
                throw new DomainException("As duas equipes ainda não foram definidas: a súmula só pode ser preparada depois que as fases de origem terminarem");

            var casa = await _equipeRepository.ObterPorIdAsync(jogo.Casa.EquipeId.Value, cancellationToken)
                ?? throw new DomainException("Equipe da casa não encontrada");
            var visitante = await _equipeRepository.ObterPorIdAsync(jogo.Visitante.EquipeId.Value, cancellationToken)
                ?? throw new DomainException("Equipe visitante não encontrada");

            var sumula = new Sumula(jogo.Id, casa.Id, casa.Nome, casa.Cor, visitante.Id, visitante.Nome, visitante.Cor);
            _sumulaRepository.Adicionar(sumula);
            jogo.VincularSumula(sumula.Id);
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
