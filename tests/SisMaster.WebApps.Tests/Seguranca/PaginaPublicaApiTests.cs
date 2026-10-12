using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// V5: o conteúdo de "A associação" e "Contato" é cadastrado por associação (missão, quem somos, diretoria, contatos), só o
/// Administrador e o Administrador da própria associação editam, e o site mostra só o que está preenchido.
/// Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class PaginaPublicaApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public PaginaPublicaApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private async Task<Guid> NovaAssociacaoAsync(string sigla)
    {
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        await DadosDeTeste.Enviar(admin, HttpMethod.Post, "/api/associacoes", new { nome = $"Associação {sigla}", sigla, uf = "BA" });
        return (await DadosDeTeste.Enviar(admin, HttpMethod.Get, "/api/associacoes")).EnumerateArray()
            .First(a => a.GetProperty("nome").GetString() == $"Associação {sigla}").GetProperty("id").GetGuid();
    }

    private static async Task<(HttpStatusCode Status, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private static object Conteudo(object? diretoria = null) => new
    {
        missao = "Aqui o basquete vive.",
        quemSomos = "Primeiro parágrafo.\n\nSegundo parágrafo.",
        gestaoDaDiretoria = "2025/2026",
        whatsapp = "(71) 90000-0000",
        instagram = "@ababas",
        email = "contato@exemplo.org",
        ondeJogamos = "Ginásio Municipal",
        diretoria = diretoria ?? new[]
        {
            new { cargo = "Presidente", nome = (string?)"Ana Souza" },
            new { cargo = "Vice-presidente", nome = (string?)"Bruno Lima" },
            new { cargo = "Secretário", nome = (string?)null },
        }
    };

    private async Task<JsonElement> Publica(string sigla)
    {
        var r = await _fabrica.ClienteCom(null).GetAsync($"/api/publico/associacoes/{sigla}/pagina");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task Sem_conteudo_a_pagina_publica_mostra_so_a_associacao()
    {
        if (SemBanco) return;
        await NovaAssociacaoAsync("PagVazia");

        var p = await Publica("PagVazia");

        p.GetProperty("associacao").GetProperty("nome").GetString().Should().Be("Associação PagVazia");
        p.GetProperty("missao").ValueKind.Should().Be(JsonValueKind.Null);
        p.GetProperty("diretoria").GetArrayLength().Should().Be(0);
        (await _fabrica.ClienteCom(null).GetAsync("/api/publico/associacoes/NaoExiste/pagina")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Administrador_salva_e_o_site_mostra_so_o_que_tem_nome_e_o_que_foi_preenchido()
    {
        if (SemBanco) return;
        var id = await NovaAssociacaoAsync("PagSalva");
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));

        (await Chamar(admin, HttpMethod.Put, $"/api/associacoes/{id}/pagina-publica", Conteudo())).Status.Should().Be(HttpStatusCode.NoContent);

        var gestao = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/associacoes/{id}/pagina-publica")).Corpo).RootElement;
        var dados = gestao.TryGetProperty("data", out var d) ? d : gestao;
        dados.GetProperty("diretoria").GetArrayLength().Should().Be(3, "a gestão vê todos os cargos, inclusive o sem nome");
        dados.GetProperty("whatsapp").GetString().Should().Be("(71) 90000-0000");

        var p = await Publica("PagSalva");
        p.GetProperty("missao").GetString().Should().Be("Aqui o basquete vive.");
        p.GetProperty("gestao").GetString().Should().Be("2025/2026");
        p.GetProperty("diretoria").EnumerateArray().Select(m => m.GetProperty("cargo").GetString()).Should().Equal("Presidente", "Vice-presidente");
        p.GetProperty("contato").GetProperty("email").GetString().Should().Be("contato@exemplo.org");
    }

    [Fact]
    public async Task Salvar_de_novo_substitui_a_diretoria_na_ordem_da_lista_e_limpa_campos_vazios()
    {
        if (SemBanco) return;
        var id = await NovaAssociacaoAsync("PagTroca");
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        await Chamar(admin, HttpMethod.Put, $"/api/associacoes/{id}/pagina-publica", Conteudo());

        var segundo = new
        {
            missao = "  ", quemSomos = (string?)null, gestaoDaDiretoria = "2027", whatsapp = "", instagram = (string?)null, email = (string?)null, ondeJogamos = (string?)null,
            diretoria = new[] { new { cargo = "Tesoureiro", nome = (string?)"Carla" }, new { cargo = "Presidente", nome = (string?)"Ana Souza" } }
        };
        (await Chamar(admin, HttpMethod.Put, $"/api/associacoes/{id}/pagina-publica", segundo)).Status.Should().Be(HttpStatusCode.NoContent);

        var p = await Publica("PagTroca");
        p.GetProperty("missao").ValueKind.Should().Be(JsonValueKind.Null);
        p.GetProperty("contato").GetProperty("whatsapp").ValueKind.Should().Be(JsonValueKind.Null);
        p.GetProperty("diretoria").EnumerateArray().Select(m => m.GetProperty("cargo").GetString()).Should().Equal("Tesoureiro", "Presidente");
    }

    [Fact]
    public async Task Conteudo_invalido_e_recusado_com_a_mensagem()
    {
        if (SemBanco) return;
        var id = await NovaAssociacaoAsync("PagRuim");
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var url = $"/api/associacoes/{id}/pagina-publica";

        var longa = await Chamar(admin, HttpMethod.Put, url, new { missao = new string('a', 161), diretoria = Array.Empty<object>() });
        longa.Status.Should().Be(HttpStatusCode.BadRequest);
        longa.Corpo.Should().Contain("missão");

        var email = await Chamar(admin, HttpMethod.Put, url, new { email = "sem-arroba", diretoria = Array.Empty<object>() });
        email.Status.Should().Be(HttpStatusCode.BadRequest);
        email.Corpo.Should().Contain("E-mail inválido");

        var cargo = await Chamar(admin, HttpMethod.Put, url, new { diretoria = new[] { new { cargo = " ", nome = "Alguém" } } });
        cargo.Status.Should().Be(HttpStatusCode.BadRequest);
        cargo.Corpo.Should().Contain("cargo");
    }

    [Fact]
    public async Task So_administrador_da_propria_associacao_edita()
    {
        if (SemBanco) return;
        var minha = await NovaAssociacaoAsync("PagMinha");
        var outra = await NovaAssociacaoAsync("PagOutra");
        var url = $"/api/associacoes/{minha}/pagina-publica";

        var deMinha = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", minha));
        (await Chamar(deMinha, HttpMethod.Put, url, Conteudo())).Status.Should().Be(HttpStatusCode.NoContent);
        (await Chamar(deMinha, HttpMethod.Get, url)).Status.Should().Be(HttpStatusCode.OK);

        var deOutra = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", outra));
        (await Chamar(deOutra, HttpMethod.Put, url, Conteudo())).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(deOutra, HttpMethod.Get, url)).Status.Should().Be(HttpStatusCode.Forbidden);

        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", minha));
        (await Chamar(arbitro, HttpMethod.Put, url, Conteudo())).Status.Should().Be(HttpStatusCode.Forbidden);

        (await Chamar(_fabrica.ClienteCom(null), HttpMethod.Put, url, Conteudo())).Status.Should().Be(HttpStatusCode.Unauthorized);
    }
}
