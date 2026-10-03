using Microsoft.AspNetCore.Identity;

namespace SisMaster.Identidade.Api.Extensions;

public class IdentityPortugueseMessages : IdentityErrorDescriber
{
    public override IdentityError DefaultError()
        => new() { Code = nameof(DefaultError), Description = "Ocorreu um erro desconhecido ao processar a solicitação." };

    public override IdentityError ConcurrencyFailure()
        => new() { Code = nameof(ConcurrencyFailure), Description = "Falha de concorrência otimista, tente novamente." };

    public override IdentityError PasswordTooShort(int length)
        => new() { Code = nameof(PasswordTooShort), Description = $"A senha deve ter pelo menos {length} caracteres." };

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "A senha deve conter pelo menos um caractere não alfanumérico." };

    public override IdentityError PasswordRequiresDigit()
        => new() { Code = nameof(PasswordRequiresDigit), Description = "A senha deve conter pelo menos um dígito ('0'-'9')." };

    public override IdentityError PasswordRequiresLower()
        => new() { Code = nameof(PasswordRequiresLower), Description = "A senha deve conter pelo menos uma letra minúscula ('a'-'z')." };

    public override IdentityError PasswordRequiresUpper()
        => new() { Code = nameof(PasswordRequiresUpper), Description = "A senha deve conter pelo menos uma letra maiúscula ('A'-'Z')." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"A senha deve conter pelo menos {uniqueChars} caracteres exclusivos." };

    public override IdentityError InvalidUserName(string? userName)
        => new() { Code = nameof(InvalidUserName), Description = $"Nome de usuário inválido '{userName}'." };

    public override IdentityError InvalidEmail(string? email)
        => new() { Code = nameof(InvalidEmail), Description = $"Email inválido '{email}'." };

    public override IdentityError DuplicateUserName(string? userName)
        => new() { Code = nameof(DuplicateUserName), Description = $"Nome de usuário '{userName}' já está em uso." };

    public override IdentityError DuplicateEmail(string? email)
        => new() { Code = nameof(DuplicateEmail), Description = $"Email '{email}' já está em uso." };

    public override IdentityError InvalidToken()
        => new() { Code = nameof(InvalidToken), Description = "Token inválido." };

    public override IdentityError LoginAlreadyAssociated()
        => new() { Code = nameof(LoginAlreadyAssociated), Description = "Já existe um usuário associado a este login." };

    public override IdentityError UserAlreadyHasPassword()
        => new() { Code = nameof(UserAlreadyHasPassword), Description = "O usuário já possui uma senha definida." };

    public override IdentityError UserLockoutNotEnabled()
        => new() { Code = nameof(UserLockoutNotEnabled), Description = "Bloqueio de usuário não está habilitado." };

    public override IdentityError UserAlreadyInRole(string? role)
        => new() { Code = nameof(UserAlreadyInRole), Description = $"O usuário já possui a função '{role}'." };

    public override IdentityError UserNotInRole(string? role)
        => new() { Code = nameof(UserNotInRole), Description = $"O usuário não possui a função '{role}'." };

    public override IdentityError PasswordMismatch()
        => new() { Code = nameof(PasswordMismatch), Description = "Senha incorreta." };
    
    public override IdentityError RecoveryCodeRedemptionFailed()
        => new() { Code = nameof(RecoveryCodeRedemptionFailed), Description = "Falha ao resgatar o código de recuperação." };

    public override IdentityError InvalidRoleName(string? role)
        => new() { Code = nameof(InvalidRoleName), Description = $"Nome da função inválido '{role}'." };

    public override IdentityError DuplicateRoleName(string? role)
        => new() { Code = nameof(DuplicateRoleName), Description = $"Nome da função '{role}' já está em uso." };
}
