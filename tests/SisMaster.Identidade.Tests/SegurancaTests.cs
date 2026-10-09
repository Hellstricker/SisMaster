using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SisMaster.Identidade.Tests;

/// <summary>Varredura da Identidade: só o login e a recuperação de senha são públicos.</summary>
[Collection(ColecaoIdentidade.Nome)]
public class SegurancaTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly IdentidadeFactory _fabrica;

    public SegurancaTests(IdentidadeFactory fabrica) => _fabrica = fabrica;

    [Fact]
    public void So_login_e_recuperacao_sao_publicos()
    {
        if (SemBanco) return;
        var acoes = _fabrica.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items;

        var publicas = acoes.Where(a => a.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            .Select(a => a.AttributeRouteInfo?.Template).ToList();

        publicas.Should().BeEquivalentTo(["api/identidade/autenticar", "api/identidade/esqueci-senha"]);
        acoes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Sem_token_a_gestao_de_usuarios_e_a_troca_de_senha_recusam()
    {
        if (SemBanco) return;
        var cliente = _fabrica.CreateClient();
        foreach (var url in new[] { "/api/identidade/usuarios" })
            (await cliente.GetAsync(url)).StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized, url);
        foreach (var url in new[] { "/api/identidade/renovar", "/api/identidade/trocar-senha" })
            (await cliente.PostAsync(url, null)).StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized, url);
    }
}
