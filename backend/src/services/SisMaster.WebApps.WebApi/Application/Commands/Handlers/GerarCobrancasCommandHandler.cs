using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class GerarCobrancasCommandHandler : IRequestHandler<GerarCobrancasCommand, ValidationResult>
{
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly IInscricaoRepository _inscricaoRepository;

    public GerarCobrancasCommandHandler(ITemporadaRepository temporadaRepository, IInscricaoRepository inscricaoRepository)
    {
        _temporadaRepository = temporadaRepository;
        _inscricaoRepository = inscricaoRepository;
    }

    public async Task<ValidationResult> Handle(GerarCobrancasCommand request, CancellationToken cancellationToken)
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

            temporada.ValidarPodeGerarCobranca();

            var fichas = await _inscricaoRepository.ListarPorTemporadaAsync(temporada.Id, cancellationToken);
            var cobrancas = new List<(Inscricao Ficha, decimal Total, decimal Desconto)>();
            var semValor = new SortedSet<string>();

            foreach (var ficha in fichas)
            {
                var cobradas = ficha.CategoriasCobradas;
                if (cobradas.Count == 0) continue;

                decimal total = 0;
                foreach (var ic in cobradas)
                {
                    var tc = temporada.Categorias.First(c => c.Id == ic.TemporadaCategoriaId);
                    if (tc.Valor is null) semValor.Add(tc.Nome);
                    else total += tc.Valor.Value;
                }

                cobrancas.Add((ficha, total, temporada.CalcularDesconto(cobradas.Select(c => c.TemporadaCategoriaId).ToList(), total)));
            }

            if (semValor.Count > 0)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty,
                    $"Defina o valor das categorias antes de gerar as cobranças: {string.Join(", ", semValor)}"));
                return request.ValidationResult;
            }

            foreach (var (ficha, total, desconto) in cobrancas)
                ficha.GerarCobranca(total, desconto);

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
