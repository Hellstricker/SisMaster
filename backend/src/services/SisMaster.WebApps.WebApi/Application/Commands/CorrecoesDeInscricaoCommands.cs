using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Corrige altura, peso, posição, plano de saúde, e-mail e telefone da ficha (não mexe em elegibilidade).</summary>
public class EditarDadosFichaCommand : Command
{
    public Guid InscricaoId { get; set; }
    public int? AlturaCm { get; set; }
    public int? PesoKg { get; set; }
    public string? Posicao { get; set; }
    public bool PossuiPlanoSaude { get; set; }
    public string? NomePlanoSaude { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Telefone { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new EditarDadosFichaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class EditarDadosFichaCommandValidation : AbstractValidator<EditarDadosFichaCommand>
{
    public EditarDadosFichaCommandValidation()
    {
        RuleFor(c => c.InscricaoId).NotEmpty().WithMessage("Id da inscrição inválido");
        RuleFor(c => c.Email).NotEmpty().MaximumLength(150).WithMessage("E-mail inválido");
    }
}

/// <summary>Só o Administrador: nome, CPF, nascimento e sexo definem a elegibilidade nas categorias.</summary>
public class CorrigirIdentificacaoCommand : Command
{
    public Guid PessoaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public DateOnly Nascimento { get; set; }
    public Sexo Sexo { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new CorrigirIdentificacaoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class CorrigirIdentificacaoCommandValidation : AbstractValidator<CorrigirIdentificacaoCommand>
{
    public CorrigirIdentificacaoCommandValidation()
    {
        RuleFor(c => c.PessoaId).NotEmpty().WithMessage("Id da pessoa inválido");
        RuleFor(c => c.Nome).NotEmpty().Length(3, 150).WithMessage("Nome deve ter entre 3 e 150 caracteres");
        RuleFor(c => c.Cpf).NotEmpty().WithMessage("CPF é obrigatório");
    }
}

/// <summary>Estorna um pagamento (taxa ou saldo): continua no histórico, com motivo, e deixa de contar.</summary>
public class EstornarPagamentoCommand : Command
{
    public Guid InscricaoId { get; set; }
    public Guid PagamentoId { get; set; }
    public string Motivo { get; set; } = string.Empty;

    public override bool EhValido()
    {
        ValidationResult = new EstornarPagamentoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class EstornarPagamentoCommandValidation : AbstractValidator<EstornarPagamentoCommand>
{
    public EstornarPagamentoCommandValidation()
    {
        RuleFor(c => c.InscricaoId).NotEmpty().WithMessage("Id da inscrição inválido");
        RuleFor(c => c.PagamentoId).NotEmpty().WithMessage("Id do pagamento inválido");
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(300).WithMessage("Informe o motivo do estorno (até 300 caracteres)");
    }
}

/// <summary>Volta um pedido aprovado para pendente. Só quando a pessoa não está em equipe.</summary>
public class DesfazerAprovacaoCommand : Command
{
    public Guid InscricaoCategoriaId { get; set; }
    public string Motivo { get; set; } = string.Empty;

    public override bool EhValido()
    {
        ValidationResult = new DesfazerAprovacaoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class DesfazerAprovacaoCommandValidation : AbstractValidator<DesfazerAprovacaoCommand>
{
    public DesfazerAprovacaoCommandValidation()
    {
        RuleFor(c => c.InscricaoCategoriaId).NotEmpty().WithMessage("Id da inscrição inválido");
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(300).WithMessage("Informe o motivo (até 300 caracteres)");
    }
}

/// <summary>Reabre um pedido recusado: volta a pendente.</summary>
public class ReabrirPedidoCommand : Command
{
    public Guid InscricaoCategoriaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ReabrirPedidoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class ReabrirPedidoCommandValidation : AbstractValidator<ReabrirPedidoCommand>
{
    public ReabrirPedidoCommandValidation()
    {
        RuleFor(c => c.InscricaoCategoriaId).NotEmpty().WithMessage("Id da inscrição inválido");
    }
}
