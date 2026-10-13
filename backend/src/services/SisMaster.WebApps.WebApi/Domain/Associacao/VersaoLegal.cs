using SisMaster.Core.Data;
using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

/// <summary>
/// Uma versão publicada dos textos legais da associação (Política de privacidade e Termos de uso, sempre publicados em par). Nunca se
/// edita nem se apaga: mudar o texto é publicar uma versão nova, e a anterior fica no histórico. Texto vazio = continua valendo o texto
/// padrão do sistema. Mudança relevante sobe o número principal (2.0) e pede novo aceite; mudança pequena sobe o secundário (1.1).
/// </summary>
public class VersaoLegal : Entity, IAggregateRoot
{
    public const string VersaoInicial = "1.0";
    public const int MaxTexto = 60_000;

    public Guid AssociacaoId { get; private set; }

    /// <summary>Ordem de publicação (1, 2, 3...). A vigente é a de maior número.</summary>
    public int Numero { get; private set; }

    public string Versao { get; private set; } = VersaoInicial;
    public bool Relevante { get; private set; }
    public string Resumo { get; private set; } = string.Empty;
    public string? TextoPrivacidade { get; private set; }
    public string? TextoTermos { get; private set; }
    public DateOnly VigenteDesde { get; private set; }
    public string PublicadaPor { get; private set; } = string.Empty;
    public DateTime PublicadaEm { get; private set; }

    protected VersaoLegal() { }

    public VersaoLegal(Guid associacaoId, VersaoLegal? anterior, bool relevante, string resumo, string? textoPrivacidade, string? textoTermos,
        DateOnly vigenteDesde, string publicadaPor)
    {
        Validacoes.ValidarSeVazio(resumo, "Descreva o que mudou nesta versão");
        resumo = resumo.Trim();
        Validacoes.ValidarSeVerdadeiro(resumo.Length > 300, "O resumo da mudança deve ter até 300 caracteres");
        textoPrivacidade = string.IsNullOrWhiteSpace(textoPrivacidade) ? null : textoPrivacidade.Trim();
        textoTermos = string.IsNullOrWhiteSpace(textoTermos) ? null : textoTermos.Trim();
        Validacoes.ValidarSeVerdadeiro((textoPrivacidade?.Length ?? 0) > MaxTexto || (textoTermos?.Length ?? 0) > MaxTexto,
            $"Cada texto pode ter até {MaxTexto:N0} caracteres");
        Validacoes.ValidarSeVerdadeiro(anterior is not null && vigenteDesde < anterior.VigenteDesde,
            "A vigência não pode ser anterior à da versão atual");

        AssociacaoId = associacaoId;
        Numero = (anterior?.Numero ?? 0) + 1;
        Versao = ProximaVersao(anterior?.Versao ?? VersaoInicial, relevante);
        Relevante = relevante;
        Resumo = resumo;
        TextoPrivacidade = textoPrivacidade;
        TextoTermos = textoTermos;
        VigenteDesde = vigenteDesde;
        PublicadaPor = string.IsNullOrWhiteSpace(publicadaPor) ? "?" : publicadaPor.Trim();
        PublicadaEm = DateTime.UtcNow;
    }

    /// <summary>"1.0" + relevante = "2.0"; "1.0" + pequena = "1.1".</summary>
    public static string ProximaVersao(string atual, bool relevante)
    {
        var partes = atual.Split('.');
        var principal = int.TryParse(partes[0], out var p) ? p : 1;
        var secundario = partes.Length > 1 && int.TryParse(partes[1], out var s) ? s : 0;
        return relevante ? $"{principal + 1}.0" : $"{principal}.{secundario + 1}";
    }
}

public interface IVersaoLegalRepository : IRepository<VersaoLegal>
{
    /// <summary>Todas as versões da associação, da mais nova à mais antiga.</summary>
    Task<IReadOnlyList<VersaoLegal>> ListarAsync(Guid associacaoId, CancellationToken ct = default);

    Task<VersaoLegal?> ObterVigenteAsync(Guid associacaoId, CancellationToken ct = default);

    void Adicionar(VersaoLegal versao);
}
