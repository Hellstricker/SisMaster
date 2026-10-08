using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;
using Cpf = SisMaster.WebApps.WebApi.Domain.Participantes.Cpf;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class EnviarInscricaoCommandHandler : IRequestHandler<EnviarInscricaoCommand, ValidationResult>
{
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly IPessoaRepository _pessoaRepository;
    private readonly IInscricaoRepository _inscricaoRepository;

    public EnviarInscricaoCommandHandler(
        ITemporadaRepository temporadaRepository,
        IPessoaRepository pessoaRepository,
        IInscricaoRepository inscricaoRepository)
    {
        _temporadaRepository = temporadaRepository;
        _pessoaRepository = pessoaRepository;
        _inscricaoRepository = inscricaoRepository;
    }

    public async Task<ValidationResult> Handle(EnviarInscricaoCommand request, CancellationToken cancellationToken)
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

            temporada.ValidarAceitaInscricoes();

            var categorias = new List<TemporadaCategoria>();
            foreach (var id in request.TemporadaCategoriaIds.Distinct())
            {
                var tc = temporada.Categorias.FirstOrDefault(c => c.Id == id);
                if (tc is null)
                {
                    request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Categoria não pertence a esta temporada"));
                    return request.ValidationResult;
                }
                categorias.Add(tc);
            }

            var cpf = Cpf.RemoverFormatacao(request.Cpf);
            var pessoa = await _pessoaRepository.ObterPorCpfAsync(cpf, cancellationToken);
            var pessoaNova = pessoa is null;
            pessoa ??= new Pessoa(request.Nome, request.Cpf, request.Nascimento, request.Sexo, request.Email, request.Telefone);

            if (!pessoaNova)
            {
                foreach (var tc in categorias)
                {
                    if (await _inscricaoRepository.ExisteAtivaAsync(pessoa.Id, tc.Id, cancellationToken))
                    {
                        request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty,
                            $"Já existe uma inscrição ativa de {pessoa.Nome} na categoria {tc.Nome}"));
                        return request.ValidationResult;
                    }
                }
            }

            var inscricao = new Inscricao(pessoa.Id, temporada.Id, request.AlturaCm, request.PesoKg, request.Posicao,
                request.PossuiPlanoSaude, request.NomePlanoSaude, request.ConsentimentoLgpd);
            foreach (var tc in categorias)
                inscricao.AdicionarCategoria(tc.Id);

            if (pessoaNova) _pessoaRepository.Adicionar(pessoa);
            _inscricaoRepository.Adicionar(inscricao);
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
