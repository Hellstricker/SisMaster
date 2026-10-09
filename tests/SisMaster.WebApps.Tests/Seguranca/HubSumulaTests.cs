using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// O hub da súmula de verdade (conexão, não só os atributos): o placar público conecta sem login e só recebe;
/// operar a mesa exige perfil de arbitragem. Exigem SQL Server (ver BancoDeTeste); SISMASTER_TESTE_SEM_BANCO=1 os dispensa.
/// </summary>
[Collection(ColecaoApi.Nome)]
public class HubSumulaTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private static readonly Guid Qualquer = Guid.Parse("00000000-0000-0000-0000-0000000000bb");
    private readonly ApiFactory _fabrica;

    public HubSumulaTests(ApiFactory fabrica) => _fabrica = fabrica;

    private HubConnection Conectar(string? token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_fabrica.Server.BaseAddress, $"/hubs/sumula?partidaId={Qualquer}"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => _fabrica.Server.CreateHandler();
                if (token is not null) o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    [Fact]
    public async Task O_placar_conecta_sem_login()
    {
        if (SemBanco) return;
        await using var conexao = Conectar(null);

        await conexao.StartAsync();

        conexao.State.Should().Be(HubConnectionState.Connected, "o placar é público: sem isso a tela do público fica 'Desconectado'");
    }

    [Fact]
    public async Task Sem_login_nao_opera_a_mesa()
    {
        if (SemBanco) return;
        await using var conexao = Conectar(null);
        await conexao.StartAsync();

        var tentativa = () => conexao.InvokeAsync("IniciarCronometro", Qualquer.ToString());

        await tentativa.Should().ThrowAsync<HubException>("operar o cronômetro exige estar logado como árbitro");
    }

    [Fact]
    public async Task Arbitro_passa_pela_autorizacao_e_so_para_na_associacao_do_jogo()
    {
        if (SemBanco) return;
        await using var conexao = Conectar(ApiFactory.Token("Arbitro", Guid.NewGuid()));
        await conexao.StartAsync();

        var tentativa = () => conexao.InvokeAsync("IniciarCronometro", Qualquer.ToString());

        // A partida não existe nem é de uma associação dele: recusa por permissão do jogo, não por falta de login.
        var erro = (await tentativa.Should().ThrowAsync<HubException>()).Which;
        erro.Message.Should().NotContain("Unauthorized").And.NotContain("authorized");
    }
}
