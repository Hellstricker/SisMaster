using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class EnviarInscricaoCommand : Command
{
    public Guid TemporadaId { get; set; }

    // Pessoa (reconhecida pelo CPF; se já existir, os dados cadastrados são mantidos)
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public DateOnly Nascimento { get; set; }
    public Sexo Sexo { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Telefone { get; set; }

    // Ficha
    public int? AlturaCm { get; set; }
    public int? PesoKg { get; set; }
    public string? Posicao { get; set; }
    public bool PossuiPlanoSaude { get; set; }
    public string? NomePlanoSaude { get; set; }
    public bool ConsentimentoLgpd { get; set; }

    public List<Guid> TemporadaCategoriaIds { get; set; } = [];

    public override bool EhValido()
    {
        ValidationResult = new EnviarInscricaoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class EnviarInscricaoCommandValidation : AbstractValidator<EnviarInscricaoCommand>
{
    public EnviarInscricaoCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
        RuleFor(c => c.Nome).NotEmpty().Length(3, 150).WithMessage("Nome deve ter entre 3 e 150 caracteres");
        RuleFor(c => c.Cpf).NotEmpty().WithMessage("CPF é obrigatório");
        RuleFor(c => c.Email).NotEmpty().MaximumLength(150).WithMessage("E-mail inválido");
        RuleFor(c => c.ConsentimentoLgpd).Equal(true).WithMessage("O consentimento LGPD é obrigatório");
        RuleFor(c => c.TemporadaCategoriaIds).NotEmpty().WithMessage("Escolha ao menos uma categoria");
    }
}
