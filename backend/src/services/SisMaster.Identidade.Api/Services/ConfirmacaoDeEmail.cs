using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SisMaster.Identidade.Api.Services;

/// <summary>Validade do link de confirmação de e-mail (24 horas), separada da do link de recuperação de senha (1 hora).</summary>
public class OpcoesDoTokenDeConfirmacao : DataProtectionTokenProviderOptions
{
    public OpcoesDoTokenDeConfirmacao()
    {
        Name = "ConfirmacaoDeEmail";
        TokenLifespan = TimeSpan.FromHours(24);
    }
}

public class ProvedorDoTokenDeConfirmacao : DataProtectorTokenProvider<IdentityUser>
{
    public ProvedorDoTokenDeConfirmacao(IDataProtectionProvider protecao, IOptions<OpcoesDoTokenDeConfirmacao> opcoes,
        ILogger<DataProtectorTokenProvider<IdentityUser>> log) : base(protecao, opcoes, log) { }
}

/// <summary>Regras da conta do público: o que vale para o link de confirmação e para a limpeza de contas nunca confirmadas.</summary>
public static class RegrasDaContaDoPublico
{
    /// <summary>Intervalo mínimo entre dois e-mails de confirmação para a mesma conta.</summary>
    public static readonly TimeSpan IntervaloEntreConfirmacoes = TimeSpan.FromMinutes(10);

    /// <summary>Conta cujo e-mail não foi confirmado neste prazo é apagada (ainda não tem nenhum outro dado).</summary>
    public const int DiasParaApagarNaoConfirmada = 7;

    /// <summary>Só caminhos internos do site (ex.: /inscricao/abc): nunca um endereço de outro site.</summary>
    public static string? CaminhoDeRetornoSeguro(string? voltar) =>
        !string.IsNullOrWhiteSpace(voltar) && voltar.Length <= 200 && System.Text.RegularExpressions.Regex.IsMatch(voltar, @"^/[A-Za-z0-9/_\-.]*$") && !voltar.Contains("//")
            ? voltar
            : null;
}
