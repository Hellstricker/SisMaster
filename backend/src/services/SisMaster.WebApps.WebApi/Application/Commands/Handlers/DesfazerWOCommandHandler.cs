using FluentValidation.Results;
using SisMaster.Core.Communications;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Auditoria;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class DesfazerWOCommandHandler : IRequestHandler<DesfazerWOCommand, ValidationResult>
{
    private readonly IJogoRepository _jogoRepository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly DesfazerWOServico _regras;
    private readonly AvancoDaTemporada _avanco;
    private readonly IAuditoria _auditoria;

    public DesfazerWOCommandHandler(IJogoRepository jogoRepository, IEquipeRepository equipeRepository, DesfazerWOServico regras,
        AvancoDaTemporada avanco, IAuditoria auditoria)
    {
        _jogoRepository = jogoRepository;
        _equipeRepository = equipeRepository;
        _regras = regras;
        _avanco = avanco;
        _auditoria = auditoria;
    }

    public async Task<ValidationResult> Handle(DesfazerWOCommand request, CancellationToken cancellationToken)
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

            var impedimento = await _regras.MotivoQueImpedeAsync(jogo, cancellationToken);
            if (impedimento is not null)
                throw new DomainException(impedimento);

            var nomes = (await _equipeRepository.ObterElencosAsync(jogo.TemporadaId, cancellationToken)).NomesDasEquipes;
            string Nome(Guid? equipe) => equipe is { } id ? nomes.GetValueOrDefault(id, "equipe") : "equipe";
            var ausente = jogo.PlacarCasa == 0 ? Nome(jogo.Casa.EquipeId) : Nome(jogo.Visitante.EquipeId);

            jogo.DesfazerWO();

            // Se o W.O. decidiu uma série, o jogo "se necessário" dispensado volta a Agendado; o avanço dispensa de novo
            // o que ainda estiver decidido pelos outros resultados.
            if (jogo.ConfrontoId is { } confronto)
            {
                var dados = await _jogoRepository.CarregarParaAvancoAsync(jogo.TemporadaId, cancellationToken);
                foreach (var outro in dados.Jogos.Where(j => j.ConfrontoId == confronto && j.Status == StatusJogo.Dispensado))
                    outro.ReativarDispensado();
            }
            await _avanco.ExecutarAsync(jogo.TemporadaId, cancellationToken);

            _auditoria.Registrar("DesfazerWO", "Jogo", jogo.Id, $"W.O. de {ausente} desfeito; o jogo voltou a Agendado. Motivo: {request.Motivo.Trim()}");
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
