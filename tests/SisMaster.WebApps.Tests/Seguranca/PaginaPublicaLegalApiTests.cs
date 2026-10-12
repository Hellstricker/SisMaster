using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// L1: os dados da controladora (CNPJ, endereço, encarregado, e-mail de privacidade, foro) que entram na política de privacidade e nos
/// termos de uso são cadastrados por associação e saem na API pública. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class PaginaPublicaLegalApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public PaginaPublicaLegalApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private async Task<Guid> NovaAsync(string sigla)
    {
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        await DadosDeTeste.Enviar(admin, HttpMethod.Post, "/api/associacoes", new { nome = $"Associação {sigla}", sigla, uf = "BA" });
        return (await DadosDeTeste.Enviar(admin, HttpMethod.Get, "/api/associacoes")).EnumerateArray()
            .First(a => a.GetProperty("nome").GetString() == $"Associação {sigla}").GetProperty("id").GetGuid();
    }

    private static async Task<(HttpStatusCode Status, string Corpo)> Salvar(HttpClient c, Guid id, object corpo)
    {
        var resp = await c.PutAsJsonAsync($"/api/associacoes/{id}/pagina-publica", corpo);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Dados_legais_sao_salvos_formatados_e_saem_na_pagina_publica()
    {
        if (SemBanco) return;
        var id = await NovaAsync("LegalOk");
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));

        var r = await Salvar(admin, id, new
        {
            diretoria = Array.Empty<object>(), cnpj = "12345678000195", endereco = "Rua A, 10 - Salvador/BA", encarregadoDeDados = "Maria Silva",
            emailPrivacidade = "privacidade@exemplo.org", foroCidade = "Salvador/BA",
        });
        r.Status.Should().Be(HttpStatusCode.NoContent, r.Corpo);

        var p = JsonDocument.Parse(await _fabrica.ClienteCom(null).GetStringAsync("/api/publico/associacoes/LegalOk/pagina")).RootElement;
        var legal = p.GetProperty("legal");
        legal.GetProperty("cnpj").GetString().Should().Be("12.345.678/0001-95", "o CNPJ sai formatado");
        legal.GetProperty("endereco").GetString().Should().Be("Rua A, 10 - Salvador/BA");
        legal.GetProperty("encarregadoDeDados").GetString().Should().Be("Maria Silva");
        legal.GetProperty("emailPrivacidade").GetString().Should().Be("privacidade@exemplo.org");
        legal.GetProperty("foroCidade").GetString().Should().Be("Salvador/BA");
    }

    [Fact]
    public async Task Dados_legais_invalidos_sao_recusados_e_sem_dados_saem_nulos()
    {
        if (SemBanco) return;
        var id = await NovaAsync("LegalRuim");
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));

        var cnpj = await Salvar(admin, id, new { diretoria = Array.Empty<object>(), cnpj = "123" });
        cnpj.Status.Should().Be(HttpStatusCode.BadRequest);
        cnpj.Corpo.Should().Contain("CNPJ");

        var email = await Salvar(admin, id, new { diretoria = Array.Empty<object>(), emailPrivacidade = "sem-arroba" });
        email.Status.Should().Be(HttpStatusCode.BadRequest);
        email.Corpo.Should().Contain("privacidade inválido");

        var p = JsonDocument.Parse(await _fabrica.ClienteCom(null).GetStringAsync("/api/publico/associacoes/LegalRuim/pagina")).RootElement;
        p.GetProperty("legal").GetProperty("cnpj").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
