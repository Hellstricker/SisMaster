using System.Net;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using SisMaster.WebApps.WebApi.Hubs;
using SisMaster.WebApps.WebApi.Seguranca;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// Autenticação, perfis e isolamento entre associações, contra a API inteira. Exigem SQL Server (ver BancoDeTeste);
/// SISMASTER_TESTE_SEM_BANCO=1 os dispensa.
/// </summary>
[Collection(ColecaoApi.Nome)]
public class SegurancaApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private static readonly Guid Qualquer = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    private readonly ApiFactory _fabrica;

    public SegurancaApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private HttpClient Admin() => _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
    private HttpClient AdminDeAssociacao(params Guid[] assoc) => _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", assoc));
    private HttpClient Arbitro(params Guid[] assoc) => _fabrica.ClienteCom(ApiFactory.Token("Arbitro", assoc));

    private static async Task<HttpStatusCode> Status(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = System.Net.Http.Json.JsonContent.Create(corpo);
        return (await c.SendAsync(req)).StatusCode;
    }

    // ---------------- sem login ----------------

    public static IEnumerable<object[]> RotasDeGestaoEDaMesa() =>
    [
        ["GET", "/api/associacoes"],
        ["GET", $"/api/associacoes/{Qualquer}"],
        ["POST", "/api/associacoes"],
        ["GET", $"/api/campeonatos/{Qualquer}"],
        ["GET", $"/api/temporadas/{Qualquer}"],
        ["GET", $"/api/temporadas/{Qualquer}/inscricoes"],
        ["GET", $"/api/temporadas/{Qualquer}/equipes"],
        ["GET", $"/api/fases/{Qualquer}/classificacao"],
        ["GET", $"/api/jogos/{Qualquer}"],
        ["GET", $"/api/jogos/{Qualquer}/sumula"],
        ["GET", "/api/arbitros/jogos"],
        ["GET", $"/api/sumulas/{Qualquer}/sumula"],
        ["POST", $"/api/sumulas/{Qualquer}/eventos"],
        ["POST", $"/api/sumulas/{Qualquer}/encerrar"],
        ["POST", $"/api/sumulas/{Qualquer}/importacao-fiba"],
    ];

    [Theory]
    [MemberData(nameof(RotasDeGestaoEDaMesa))]
    public async Task Sem_login_tudo_recusa_com_401(string metodo, string url)
    {
        if (SemBanco) return;
        (await Status(_fabrica.ClienteCom(null), new HttpMethod(metodo), url, metodo == "POST" ? new { } : null)).Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A conta do público (Participante) tem token válido, mas nenhuma rota da gestão, da mesa ou da súmula a aceita.</summary>
    [Theory]
    [MemberData(nameof(RotasDeGestaoEDaMesa))]
    public async Task Participante_nao_alcanca_nenhuma_rota_da_equipe(string metodo, string url)
    {
        if (SemBanco) return;
        var participante = _fabrica.ClienteCom(ApiFactory.Token("Participante"));
        (await Status(participante, new HttpMethod(metodo), url, metodo == "POST" ? new { } : null)).Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void Toda_rota_nao_publica_declara_a_politica_e_nao_depende_so_do_padrao()
    {
        // O padrão (sem política) já exclui o Participante, mas toda rota deve dizer de propósito quem pode usá-la.
        var semPolitica = _fabrica.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => !a.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            .Where(a => !a.EndpointMetadata.OfType<IAuthorizeData>().Any(d => !string.IsNullOrEmpty(d.Policy) || !string.IsNullOrEmpty(d.Roles)))
            .Select(a => $"{a.ControllerName}.{a.ActionName}").Distinct().ToList();

        semPolitica.Should().BeEmpty("toda rota não pública precisa de [Authorize(Policy = ...)] (na ação ou no controller)");
    }

    [Fact]
    public async Task Placar_e_jogadores_da_partida_continuam_publicos()
    {
        if (SemBanco) return;
        var anonimo = _fabrica.ClienteCom(null);
        // 404 (a partida não existe) prova que passou pela autenticação sem exigir login
        (await Status(anonimo, HttpMethod.Get, $"/api/partidas/{Qualquer}/placar")).Should().Be(HttpStatusCode.NotFound);
        (await Status(anonimo, HttpMethod.Get, $"/api/partidas/{Qualquer}/jogadores")).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Token_adulterado_ou_de_outra_chave_e_recusado()
    {
        if (SemBanco) return;
        var token = ApiFactory.Token("Administrador");
        (await Status(_fabrica.ClienteCom(token[..^3] + "abc"), HttpMethod.Get, "/api/associacoes")).Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------- perfis ----------------

    [Fact]
    public async Task Arbitro_nao_acessa_a_gestao()
    {
        if (SemBanco) return;
        var arbitro = Arbitro(Qualquer);
        (await Status(arbitro, HttpMethod.Get, "/api/associacoes")).Should().Be(HttpStatusCode.Forbidden);
        (await Status(arbitro, HttpMethod.Post, "/api/associacoes", new { nome = "X", sigla = "X", uf = "BA" })).Should().Be(HttpStatusCode.Forbidden);
        (await Status(arbitro, HttpMethod.Get, $"/api/campeonatos/{Qualquer}")).Should().Be(HttpStatusCode.Forbidden);
        (await Status(arbitro, HttpMethod.Get, $"/api/temporadas/{Qualquer}/inscricoes")).Should().Be(HttpStatusCode.Forbidden);
        (await Status(arbitro, HttpMethod.Post, "/api/inscricoes", new { })).Should().Be(HttpStatusCode.Forbidden);
        (await Status(arbitro, HttpMethod.Get, $"/api/jogos/{Qualquer}")).Should().Be(HttpStatusCode.Forbidden);          // detalhe do jogo é da gestão
        (await Status(arbitro, HttpMethod.Post, $"/api/jogos/{Qualquer}/wo", new { })).Should().Be(HttpStatusCode.Forbidden);
        (await Status(arbitro, HttpMethod.Post, $"/api/sumulas/{Qualquer}/importacao-fiba", new { })).Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Arbitro_acessa_a_area_do_arbitro()
    {
        if (SemBanco) return;
        var arbitro = Arbitro(Qualquer);
        (await Status(arbitro, HttpMethod.Get, "/api/arbitros/jogos")).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Administrador_de_associacao_nao_cria_associacao_nem_muda_o_status()
    {
        if (SemBanco) return;
        var adm = AdminDeAssociacao(Qualquer);
        (await Status(adm, HttpMethod.Post, "/api/associacoes", new { nome = "Outra", sigla = "OT", uf = "BA" })).Should().Be(HttpStatusCode.Forbidden);
        (await Status(adm, HttpMethod.Patch, $"/api/associacoes/{Qualquer}/status", new { })).Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------- isolamento entre associações ----------------

    [Fact]
    public async Task Administrador_de_associacao_so_enxerga_e_altera_a_propria()
    {
        if (SemBanco) return;
        var sufixo = Guid.NewGuid().ToString("N")[..6];
        var admin = Admin();
        var a = await DadosDeTeste.CriarAsync(admin, $"A{sufixo}");
        var b = await DadosDeTeste.CriarAsync(admin, $"B{sufixo}");
        var admA = AdminDeAssociacao(a.AssociacaoId);

        // lista: só a própria
        var lista = await DadosDeTeste.Enviar(admA, HttpMethod.Get, "/api/associacoes");
        lista.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().BeEquivalentTo([a.AssociacaoId]);

        // a própria: acesso; a do vizinho: 403 em toda a cadeia (associação, campeonato, temporada, fase, jogo)
        (await Status(admA, HttpMethod.Get, $"/api/associacoes/{a.AssociacaoId}")).Should().Be(HttpStatusCode.OK);
        (await Status(admA, HttpMethod.Get, $"/api/campeonatos/{a.CampeonatoId}")).Should().Be(HttpStatusCode.OK);
        (await Status(admA, HttpMethod.Get, $"/api/jogos/{a.JogoId}")).Should().Be(HttpStatusCode.OK);

        (await Status(admA, HttpMethod.Get, $"/api/associacoes/{b.AssociacaoId}")).Should().Be(HttpStatusCode.Forbidden);
        (await Status(admA, HttpMethod.Post, $"/api/associacoes/{b.AssociacaoId}/campeonatos", new { nome = "Invasor" })).Should().Be(HttpStatusCode.Forbidden);
        (await Status(admA, HttpMethod.Get, $"/api/campeonatos/{b.CampeonatoId}")).Should().Be(HttpStatusCode.Forbidden);
        (await Status(admA, HttpMethod.Get, $"/api/temporadas/{b.TemporadaId}")).Should().Be(HttpStatusCode.Forbidden);
        (await Status(admA, HttpMethod.Get, $"/api/temporadas/{b.TemporadaId}/jogos")).Should().Be(HttpStatusCode.Forbidden);
        (await Status(admA, HttpMethod.Get, $"/api/fases/{b.FaseId}/classificacao")).Should().Be(HttpStatusCode.Forbidden);
        (await Status(admA, HttpMethod.Get, $"/api/jogos/{b.JogoId}")).Should().Be(HttpStatusCode.Forbidden);
        (await Status(admA, HttpMethod.Patch, $"/api/jogos/{b.JogoId}", new { data = "2030-01-01", hora = "10:00:00" })).Should().Be(HttpStatusCode.Forbidden);

        // uma inscrição pedindo a temporada do vizinho (id vindo no corpo) também é barrada
        (await Status(admA, HttpMethod.Post, "/api/inscricoes", new { temporadaId = b.TemporadaId })).Should().Be(HttpStatusCode.Forbidden);

        // o Administrador (global) acessa as duas
        (await Status(admin, HttpMethod.Get, $"/api/jogos/{a.JogoId}")).Should().Be(HttpStatusCode.OK);
        (await Status(admin, HttpMethod.Get, $"/api/jogos/{b.JogoId}")).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Arbitro_so_opera_jogos_das_associacoes_vinculadas()
    {
        if (SemBanco) return;
        var sufixo = Guid.NewGuid().ToString("N")[..6];
        var admin = Admin();
        var a = await DadosDeTeste.CriarAsync(admin, $"C{sufixo}");
        var b = await DadosDeTeste.CriarAsync(admin, $"D{sufixo}");
        var arbA = Arbitro(a.AssociacaoId);

        // a lista do árbitro traz só os jogos da associação dele
        var jogos = (await arbA.GetStringAsync("/api/arbitros/jogos"));
        jogos.Should().Contain(a.JogoId.ToString());
        jogos.Should().NotContain(b.JogoId.ToString());

        // a súmula do jogo dele: acesso; a do jogo da outra associação: 403
        (await Status(arbA, HttpMethod.Post, $"/api/jogos/{a.JogoId}/sumula")).Should().Be(HttpStatusCode.NoContent);
        (await Status(arbA, HttpMethod.Get, $"/api/jogos/{a.JogoId}/sumula")).Should().Be(HttpStatusCode.OK);
        (await Status(arbA, HttpMethod.Get, $"/api/jogos/{b.JogoId}/sumula")).Should().Be(HttpStatusCode.Forbidden);
        (await Status(arbA, HttpMethod.Post, $"/api/jogos/{b.JogoId}/sumula")).Should().Be(HttpStatusCode.Forbidden);

        // quem não tem vínculo nenhum não acessa nada de associação
        var semVinculo = Arbitro();
        (await Status(semVinculo, HttpMethod.Get, $"/api/jogos/{a.JogoId}/sumula")).Should().Be(HttpStatusCode.Forbidden);
        (await semVinculo.GetStringAsync("/api/arbitros/jogos")).Should().NotContain(a.JogoId.ToString());
    }

    // ---------------- guardas de regressão ----------------

    [Fact]
    public void Toda_rota_com_id_declara_o_que_o_id_significa()
    {
        // O filtro de associação só consegue conferir o "id" se souber que recurso ele é. Sem isso, uma rota nova ficaria sem escopo.
        var acoes = _fabrica.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => a.AttributeRouteInfo?.Template is { } t && System.Text.RegularExpressions.Regex.IsMatch(t, @"\{id[:}]"))
            .Where(a => !a.EndpointMetadata.OfType<IAllowAnonymous>().Any());

        var semEscopo = acoes.Where(a => !a.EndpointMetadata.OfType<IdEhAttribute>().Any())
            .Select(a => $"{a.ControllerName}.{a.ActionName} ({a.AttributeRouteInfo!.Template})").Distinct().ToList();

        semEscopo.Should().BeEmpty("toda rota com {id} precisa de [IdEh(Recurso.X)] (na ação ou no controller)");
    }

    [Fact]
    public void So_estas_rotas_sao_publicas()
    {
        var publicas = _fabrica.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => a.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            .Select(a => $"{a.ControllerName}.{a.ActionName}").Distinct().Order().ToList();

        publicas.Should().BeEquivalentTo(["Publico.Associacao", "Publico.Campeonatos", "Publico.Classificacao", "Publico.Inicio", "Publico.InscricoesAbertas", "Publico.Jogo", "Publico.Pagina",
            "Publico.Temporada", "Sumulas.ListarJogadoresDaPartida", "Sumulas.ObterPlacar"],
            "só o placar, os nomes dos jogadores e a API do site público (somente leitura, sem dado pessoal) são públicos; qualquer outra rota pública precisa de decisão explícita");
    }

    [Fact]
    public void No_hub_so_ver_o_placar_e_publico()
    {
        // Operar a mesa (cronômetro, posse, período, shot clock) exige perfil de árbitro ou superior.
        var metodos = typeof(SumulaHub).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(Task));

        foreach (var m in metodos)
        {
            var anonimo = m.GetCustomAttributes<AllowAnonymousAttribute>().Any();
            var exigeLogin = m.GetCustomAttributes<AuthorizeAttribute>().Any();
            if (m.Name == nameof(Hub.OnConnectedAsync)) anonimo.Should().BeTrue("ver o placar ao vivo é público");
            else exigeLogin.Should().BeTrue($"{m.Name} opera a mesa e precisa de [Authorize]");
        }
    }
}
