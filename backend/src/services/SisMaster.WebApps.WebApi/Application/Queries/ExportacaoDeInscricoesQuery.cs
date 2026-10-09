using System.Globalization;
using System.Text;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Auditoria;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Queries;

public sealed record FiltroDeExportacao(StatusInscricaoCategoria? Status, Guid? TemporadaCategoriaId, string? Busca);

/// <summary>CPF, contato e dados da ficha são opcionais: o padrão não leva dado pessoal além do nome.</summary>
public sealed record ColunasDeExportacao(bool Cpf, bool Contato, bool Ficha);

public sealed record ArquivoExportado(byte[] Conteudo, string Nome, int Linhas);

/// <summary>
/// Exporta as inscrições da temporada em CSV (UTF-8 com marca, separador ";", decimais com vírgula: abre direto no Excel).
/// Uma linha por pedido de categoria; o financeiro é o da ficha. Cada exportação fica registrada na auditoria.
/// </summary>
public class ExportacaoDeInscricoesQuery(IInscricaoRepository fichas, ITemporadaRepository temporadas, IAuditoria auditoria)
{
    private static readonly CultureInfo Pt = new("pt-BR");

    public async Task<ArquivoExportado?> ExecutarAsync(Guid temporadaId, FiltroDeExportacao filtro, ColunasDeExportacao colunas, CancellationToken ct = default)
    {
        var temporada = await temporadas.ObterPorIdAsync(temporadaId, ct);
        if (temporada is null) return null;

        var busca = filtro.Busca?.Trim();
        var buscaDigitos = new string((busca ?? string.Empty).Where(char.IsDigit).ToArray());

        var cabecalho = new List<string> { "Nome", "Idade", "Perfil" };
        if (colunas.Cpf) cabecalho.Add("CPF");
        if (colunas.Contato) cabecalho.AddRange(["E-mail", "Telefone"]);
        if (colunas.Ficha) cabecalho.AddRange(["Altura (cm)", "Peso (kg)", "Posição"]);
        cabecalho.AddRange(["Categoria", "Situação", "Valor final", "Desconto", "Pago", "Saldo", "Taxa paga"]);

        var linhas = new List<string> { string.Join(';', cabecalho) };
        foreach (var f in await fichas.ListarFichasPorTemporadaAsync(temporadaId, ct))
        {
            var p = f.Pessoa;
            if (!string.IsNullOrEmpty(busca)
                && !p.Nome.Contains(busca, StringComparison.CurrentCultureIgnoreCase)
                && !(buscaDigitos.Length > 0 && p.Cpf.Numero.Contains(buscaDigitos)))
                continue;

            foreach (var pedido in f.Categorias.OrderBy(c => c.TemporadaCategoria.Nome))
            {
                if (filtro.Status is not null && pedido.Status != filtro.Status) continue;
                if (filtro.TemporadaCategoriaId is not null && pedido.TemporadaCategoriaId != filtro.TemporadaCategoriaId) continue;

                var campos = new List<string> { Texto(p.Nome), (temporada.Ano - p.Nascimento.Year).ToString(Pt), p.Perfil.ToString() };
                if (colunas.Cpf) campos.Add(p.Cpf.ToString());
                if (colunas.Contato) campos.AddRange([Texto(p.Email), Texto(p.Telefone)]);
                if (colunas.Ficha) campos.AddRange([f.AlturaCm?.ToString(Pt) ?? "", f.PesoKg?.ToString(Pt) ?? "", Texto(f.Posicao)]);
                campos.Add(Texto(pedido.TemporadaCategoria.Nome));
                campos.Add(Rotulo(pedido.Status));
                campos.Add(Dinheiro(f.ValorFinal));
                campos.Add(Dinheiro(f.CobrancaGerada ? f.ValorDesconto : null));
                campos.Add(Dinheiro(f.TotalPago));
                campos.Add(Dinheiro(f.Saldo));
                campos.Add(f.TaxaPaga ? "Sim" : "Não");
                linhas.Add(string.Join(';', campos));
            }
        }

        var total = linhas.Count - 1;
        var colunasUsadas = new List<string> { "nome/situação/financeiro" };
        if (colunas.Cpf) colunasUsadas.Add("CPF");
        if (colunas.Contato) colunasUsadas.Add("e-mail e telefone");
        if (colunas.Ficha) colunasUsadas.Add("altura/peso/posição");
        await auditoria.RegistrarEGravarAsync("ExportacaoDeInscricoes", "Temporada", temporadaId,
            $"{total} linhas. Colunas: {string.Join(", ", colunasUsadas)}. Filtro: situação={filtro.Status?.ToString() ?? "todas"}, " +
            $"categoria={(filtro.TemporadaCategoriaId?.ToString() ?? "todas")}, busca={(string.IsNullOrWhiteSpace(busca) ? "-" : "informada")}", ct);

        var conteudo = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join("\r\n", linhas) + "\r\n")).ToArray();
        return new ArquivoExportado(conteudo, $"inscricoes-{temporada.Ano}.csv", total);
    }

    private static string Rotulo(StatusInscricaoCategoria s) => s switch
    {
        StatusInscricaoCategoria.AguardandoPagamento => "Aguardando pagamento",
        _ => s.ToString(),
    };

    private static string Dinheiro(decimal? v) => v is null ? "" : v.Value.ToString("0.00", Pt);

    /// <summary>Aspas quando preciso; e uma apóstrofo antes de =,+,-,@ para o Excel não executar o texto como fórmula.</summary>
    private static string Texto(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return "";
        if (valor[0] is '=' or '+' or '-' or '@' or '\t' or '\r') valor = "'" + valor;
        return valor.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + valor.Replace("\"", "\"\"") + "\"" : valor;
    }
}
