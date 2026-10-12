using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// A bonificação do rodízio é DECIMAL (ex.: 0,5 ponto por atleta que cumpriu), nunca inteira: o valor configurado volta sem
/// arredondar na gestão, na classificação e na vitrine pública, e mais de 2 casas é recusado (em vez de arredondar em silêncio).
/// Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class BonificacaoDecimalApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public BonificacaoDecimalApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("1.25", 1.25)]
    [InlineData("2", 2)]
    public async Task Valor_decimal_e_guardado_e_devolvido_sem_arredondar(string valorJson, double esperado)
    {
        if (SemBanco) return;
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var e = await DadosDeTeste.CriarAsync(admin, "Bonus" + valorJson.Replace(".", ""));

        var resp = await admin.PatchAsync($"/api/campeonatos/{e.CampeonatoId}/temporadas/{e.TemporadaId}/bonificacao",
            new StringContent($"{{\"valorPorAtleta\":{valorJson}}}", System.Text.Encoding.UTF8, "application/json"));
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent, await resp.Content.ReadAsStringAsync());

        var temporada = await DadosDeTeste.Enviar(admin, HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}");
        temporada.GetProperty("valorBonificacaoPorAtleta").GetDecimal().Should().Be((decimal)esperado);

        var classificacao = await DadosDeTeste.Enviar(admin, HttpMethod.Get, $"/api/fases/{e.FaseId}/classificacao");
        classificacao.GetProperty("temporada").GetProperty("valorBonificacaoPorAtleta").GetDecimal().Should().Be((decimal)esperado);
        classificacao.GetProperty("bonificacaoAtiva").GetBoolean().Should().BeTrue();

        var publica = JsonDocument.Parse(await _fabrica.ClienteCom(null).GetStringAsync($"/api/publico/fases/{e.FaseId}/classificacao")).RootElement;
        publica.GetProperty("temporada").GetProperty("valorBonificacaoPorAtleta").GetDecimal().Should().Be((decimal)esperado);
    }

    [Fact]
    public async Task Mais_de_duas_casas_decimais_e_negativo_sao_recusados()
    {
        if (SemBanco) return;
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var e = await DadosDeTeste.CriarAsync(admin, "BonusRuim");
        var url = $"/api/campeonatos/{e.CampeonatoId}/temporadas/{e.TemporadaId}/bonificacao";

        var tresCasas = await admin.PatchAsync(url, new StringContent("{\"valorPorAtleta\":0.125}", System.Text.Encoding.UTF8, "application/json"));
        tresCasas.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tresCasas.Content.ReadAsStringAsync()).Should().Contain("2 casas decimais");

        var negativo = await admin.PatchAsync(url, new StringContent("{\"valorPorAtleta\":-0.5}", System.Text.Encoding.UTF8, "application/json"));
        negativo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
