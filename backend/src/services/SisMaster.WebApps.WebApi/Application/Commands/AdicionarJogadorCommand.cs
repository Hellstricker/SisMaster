using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class AdicionarJogadorCommand : Command
{
    public Guid TimeId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public int Numero { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new AdicionarJogadorCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class AdicionarJogadorCommandValidation : AbstractValidator<AdicionarJogadorCommand>
{
    public AdicionarJogadorCommandValidation()
    {
        RuleFor(c => c.TimeId).NotEmpty().WithMessage("TimeId é obrigatório");
        RuleFor(c => c.Nome).NotEmpty().Length(3, 150).WithMessage("Nome deve ter entre 3 e 150 caracteres");
        RuleFor(c => c.Cpf).NotEmpty().Length(11, 14).WithMessage("CPF inválido");
        RuleFor(c => c.Numero).InclusiveBetween(0, 99).WithMessage("Número deve estar entre 0 e 99");
    }
}
