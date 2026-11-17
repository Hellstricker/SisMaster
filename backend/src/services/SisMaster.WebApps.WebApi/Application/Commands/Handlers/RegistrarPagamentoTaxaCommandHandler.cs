using FluentValidation.Results;
using SisMaster.Core.Communications;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class RegistrarPagamentoTaxaCommandHandler : IRequestHandler<RegistrarPagamentoTaxaCommand, ValidationResult>
{
    private readonly IInscricaoRepository _inscricaoRepository;
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly AvisosDeInscricao _avisos;

    public RegistrarPagamentoTaxaCommandHandler(IInscricaoRepository inscricaoRepository, ITemporadaRepository temporadaRepository, AvisosDeInscricao avisos)
    {
        _inscricaoRepository = inscricaoRepository;
        _temporadaRepository = temporadaRepository;
        _avisos = avisos;
    }

    public async Task<ValidationResult> Handle(RegistrarPagamentoTaxaCommand request, CancellationToken cancellationToken)
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

            var temporada = await _temporadaRepository.ObterPorIdAsync(ficha.TemporadaId, cancellationToken);
            if (temporada is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Temporada não encontrada"));
                return request.ValidationResult;
            }

            var pagamento = ficha.RegistrarPagamentoTaxa(temporada.TaxaInscricao, request.DataPagamento);
            _inscricaoRepository.AdicionarPagamento(pagamento);

            // Regra: quem paga a inscrição passa a ser Associado automaticamente
            // (só em temporada de associados: em torneio aberto, participar não cria vínculo)
            if (!temporada.Aberta) ficha.Pessoa.TornarAssociado();

            await _inscricaoRepository.UnitOfWork.Commit();
            await _avisos.TaxaConfirmadaAsync(ficha, request.AvisarPorEmail, cancellationToken);

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
