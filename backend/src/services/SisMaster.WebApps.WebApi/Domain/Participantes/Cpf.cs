using SisMaster.Core.DomainObjects;
using System.Text.RegularExpressions;

namespace SisMaster.WebApps.WebApi.Domain.Participantes;

public class Cpf
{
    public const int TamanhoCpf = 11;

    public string Numero { get; private set; }

    protected Cpf() { Numero = string.Empty; }

    public Cpf(string numero)
    {
        Validacoes.ValidarSeVazio(numero, "CPF não pode ser vazio");
        Validacoes.ValidarTamanho(numero, TamanhoCpf, TamanhoCpf, $"CPF deve ter {TamanhoCpf} caracteres");
        Validacoes.ValidarSeDiferente(@"^\d{11}$", numero, "CPF deve conter apenas dígitos");

        Numero = numero;
    }

    public static string RemoverFormatacao(string cpf) =>
        Regex.Replace(cpf ?? string.Empty, @"[^\d]", string.Empty);

    public override string ToString() =>
        $"{Numero[..3]}.{Numero[3..6]}.{Numero[6..9]}-{Numero[9..11]}";
}
