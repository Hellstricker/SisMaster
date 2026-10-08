using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class SalvarRelacaoCommandHandler : IRequestHandler<SalvarRelacaoCommand, ValidationResult>
{
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IEquipeRepository _equipeRepository;

    public SalvarRelacaoCommandHandler(ISumulaRepository sumulaRepository, IEquipeRepository equipeRepository)
    {
        _sumulaRepository = sumulaRepository;
        _equipeRepository = equipeRepository;
    }

    public async Task<ValidationResult> Handle(SalvarRelacaoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var sumula = await _sumulaRepository.ObterPorJogoAsync(request.JogoId, cancellationToken);
            if (sumula is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Este jogo ainda não tem súmula: comece a prepará-la antes"));
                return request.ValidationResult;
            }

            // Só atletas do elenco da equipe; o nome vira um retrato na súmula.
            var time = sumula.TimeDoLado(request.Lado);
            var elenco = (await _equipeRepository.ObterElencoAsync(time.EquipeId, cancellationToken)).ToDictionary(a => a.AtletaId);

            var relacionados = new List<RelacionadoNovo>();
            foreach (var j in request.Jogadores)
            {
                if (!elenco.TryGetValue(j.AtletaId, out var atleta))
                    throw new DomainException($"O atleta informado não pertence ao elenco de {time.Nome}");
                relacionados.Add(new RelacionadoNovo(atleta.AtletaId, atleta.Nome, j.Numero?.Trim() ?? string.Empty, j.Titular));
            }

            var (removidos, adicionados) = sumula.SalvarRelacao(request.Lado, request.Tecnico, request.AuxiliarTecnico, relacionados, request.CapitaoAtletaId);
            _sumulaRepository.RemoverJogadores(removidos);
            _sumulaRepository.AdicionarJogadores(adicionados);
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
