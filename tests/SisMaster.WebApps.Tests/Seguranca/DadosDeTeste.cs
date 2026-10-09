using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>Monta, pela própria API e com o token do Administrador, a estrutura de uma associação até um jogo agendado.</summary>
public sealed record Estrutura(Guid AssociacaoId, Guid CampeonatoId, Guid TemporadaId, Guid FaseId, Guid JogoId);

public static class DadosDeTeste
{
    public static async Task<JsonElement> Enviar(HttpClient c, HttpMethod metodo, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(metodo, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        var texto = await resp.Content.ReadAsStringAsync();
        resp.IsSuccessStatusCode.Should().BeTrue($"{metodo} {url} -> {(int)resp.StatusCode}: {texto}");
        return string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
    }

    /// <summary>Associação -> categoria -> campeonato -> temporada -> 2 equipes -> fase -> tabela de jogos -> jogo agendado para hoje.</summary>
    public static async Task<Estrutura> CriarAsync(HttpClient admin, string nome)
    {
        var sigla = nome.Length > 8 ? nome[..8] : nome;
        await Enviar(admin, HttpMethod.Post, "/api/associacoes", new { nome = $"Associação {nome}", sigla, uf = "BA" });
        var assoc = (await Enviar(admin, HttpMethod.Get, "/api/associacoes")).EnumerateArray()
            .First(a => a.GetProperty("nome").GetString() == $"Associação {nome}").GetProperty("id").GetGuid();

        await Enviar(admin, HttpMethod.Post, $"/api/associacoes/{assoc}/categorias",
            new { nome = "Master 40+", idadeMinima = 40, sexo = "Masculino", aceitaAbaixoIdadeMinima = false, minimoPeriodosEmQuadra = 1, minimoPeriodosForaQuadra = 1 });
        var categoria = (await Enviar(admin, HttpMethod.Get, $"/api/associacoes/{assoc}/categorias")).EnumerateArray().First().GetProperty("id").GetGuid();
        await Enviar(admin, HttpMethod.Post, $"/api/associacoes/{assoc}/locais", new { nome = "Ginásio", cidade = "Salvador", estado = "BA" });
        var local = (await Enviar(admin, HttpMethod.Get, $"/api/associacoes/{assoc}/locais")).EnumerateArray().First().GetProperty("id").GetGuid();

        await Enviar(admin, HttpMethod.Post, $"/api/associacoes/{assoc}/campeonatos", new { nome = "Campeonato" });
        var campeonato = (await Enviar(admin, HttpMethod.Get, $"/api/associacoes/{assoc}")).GetProperty("campeonatos").EnumerateArray().First().GetProperty("id").GetGuid();

        var hoje = DateTime.UtcNow;
        await Enviar(admin, HttpMethod.Post, $"/api/campeonatos/{campeonato}/temporadas",
            new { ano = hoje.Year, dataInicioInscricoes = hoje.AddDays(-1), dataFimInscricoes = hoje.AddDays(30) });
        var temporada = (await Enviar(admin, HttpMethod.Get, $"/api/campeonatos/{campeonato}")).GetProperty("temporadas").EnumerateArray().First().GetProperty("id").GetGuid();
        await Enviar(admin, HttpMethod.Post, $"/api/temporadas/{temporada}/categorias", new { categoriaId = categoria });
        var tc = (await Enviar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}")).GetProperty("categorias").EnumerateArray().First().GetProperty("id").GetGuid();

        await Enviar(admin, HttpMethod.Patch, $"/api/campeonatos/{campeonato}/temporadas/{temporada}/encerrar-inscricoes", new { });
        foreach (var equipe in new[] { "Águias", "Ursos" })
            await Enviar(admin, HttpMethod.Post, $"/api/temporadas/{temporada}/equipes", new { temporadaId = temporada, temporadaCategoriaId = tc, nome = equipe, cor = "#ff0000" });
        await Enviar(admin, HttpMethod.Post, $"/api/temporadas/{temporada}/fases",
            new { temporadaId = temporada, temporadaCategoriaId = tc, nome = "Fase única", tipo = "PontosCorridos", numeroTurnos = 1 });
        await Enviar(admin, HttpMethod.Post, $"/api/temporadas/{temporada}/fases/encerrar-cadastro", new { });
        await Enviar(admin, HttpMethod.Post, $"/api/temporadas/{temporada}/tabela-jogos", new { });

        var lista = await Enviar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}/jogos");
        var jogo = lista.GetProperty("jogos").EnumerateArray().First().GetProperty("id").GetGuid();
        var fase = lista.GetProperty("fases").EnumerateArray().First().GetProperty("id").GetGuid();
        await Enviar(admin, HttpMethod.Patch, $"/api/jogos/{jogo}", new { data = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"), hora = "19:00:00", localId = local });
        return new Estrutura(assoc, campeonato, temporada, fase, jogo);
    }
}
