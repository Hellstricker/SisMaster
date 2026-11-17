using FluentValidation.Results;
using SisMaster.Core.Communications;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class AprovarInscricaoCategoriaCommandHandler : IRequestHandler<AprovarInscricaoCategoriaCommand, ValidationResult>
{
    private readonly IInscricaoRepository _repository;
    private readonly AvisosDeInscricao _avisos;
    private readonly InscricaoDeEquipeServico _equipes;

    public AprovarInscricaoCategoriaCommandHandler(IInscricaoRepository repository, AvisosDeInscricao avisos, InscricaoDeEquipeServico equipes)
    {
        _repository = repository;
        _avisos = avisos;
        _equipes = equipes;
    }

    public async Task<ValidationResult> Handle(AprovarInscricaoCategoriaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var ficha = await _repository.ObterPorCategoriaIdAsync(request.InscricaoCategoriaId, cancellationToken);
            if (ficha is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Inscrição não encontrada"));
                return request.ValidationResult;
            }

            var ic = ficha.Categorias.First(c => c.Id == request.InscricaoCategoriaId);
            var pessoa = ficha.Pessoa;
            var motivos = ic.TemporadaCategoria.DescreverExcecoes(pessoa.Nascimento, pessoa.Sexo, ic.TemporadaCategoria.Temporada.Ano);

            // Se a taxa da ficha já foi paga, o pedido aprovado já é efetivado.
            ficha.AprovarCategoria(ic.Id, request.JustificativaExcecao, motivos.Count > 0);

            // Atleta de convite cuja equipe já foi aprovada e cuja taxa já estava paga: o pedido efetivado entra no elenco.
            await _equipes.ColocarNaEquipeAsync(ficha, cancellationToken);
            await _repository.UnitOfWork.Commit();
            await _avisos.AprovadaAsync(ficha, ic, ficha.TaxaPaga, request.AvisarPorEmail, cancellationToken);

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
