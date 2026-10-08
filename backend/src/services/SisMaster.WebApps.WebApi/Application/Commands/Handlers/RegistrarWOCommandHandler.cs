using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class RegistrarWOCommandHandler : IRequestHandler<RegistrarWOCommand, ValidationResult>
{
    private readonly IJogoRepository _jogoRepository;
    private readonly ISumulaRepository _sumulaRepository;
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly AvancoDaTemporada _avanco;

    public RegistrarWOCommandHandler(IJogoRepository jogoRepository, ISumulaRepository sumulaRepository, ITemporadaRepository temporadaRepository, AvancoDaTemporada avanco)
    {
        _jogoRepository = jogoRepository;
        _sumulaRepository = sumulaRepository;
        _temporadaRepository = temporadaRepository;
        _avanco = avanco;
    }

    public async Task<ValidationResult> Handle(RegistrarWOCommand request, CancellationToken cancellationToken)
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
            if (jogo.Casa.EquipeId is null || jogo.Visitante.EquipeId is null)
                throw new DomainException("As duas equipes ainda não foram definidas");

            // Uma súmula só preparada é descartada junto com o W.O.; uma já iniciada impede o W.O.
            var sumula = await _sumulaRepository.ObterPorJogoAsync(jogo.Id, cancellationToken);
            if (sumula is not null)
            {
                if (sumula.Status != StatusSumula.EmPreparacao)
                    throw new DomainException("A súmula já foi iniciada: o W.O. não pode mais ser registrado");
                _sumulaRepository.Remover(sumula);
                jogo.DesvincularSumula();
            }

            jogo.RegistrarWO(request.CasaAusente);
            await _avanco.ExecutarAsync(jogo.TemporadaId, cancellationToken);
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
