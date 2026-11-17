using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SisMaster.Core.Communications;
using Xunit;

namespace SisMaster.WebApps.Tests.Domain;

/// <summary>O mediador próprio (no lugar do MediatR): um manipulador por comando, todos os manipuladores por notificação, erros que chegam intactos.</summary>
public class MediadorProprioTests
{
    private sealed record Soma(int A, int B) : IRequest<int>;
    private sealed record SemManipulador : IRequest<string>;
    private sealed record Explode : IRequest<int>;
    private sealed class FalhaEsperada(string mensagem) : Exception(mensagem);

    private sealed class SomaHandler : IRequestHandler<Soma, int>
    {
        public Task<int> Handle(Soma request, CancellationToken cancellationToken) => Task.FromResult(request.A + request.B);
    }

    private sealed class ExplodeHandler : IRequestHandler<Explode, int>
    {
        public Task<int> Handle(Explode request, CancellationToken cancellationToken) => throw new FalhaEsperada("falhou de propósito");
    }

    private class Aviso : INotification { }
    private sealed class AvisoEspecial : Aviso { }

    private sealed class Registro { public List<string> Chamadas { get; } = []; }

    private sealed class PrimeiroDoAviso(Registro r) : INotificationHandler<Aviso>
    {
        public Task Handle(Aviso notification, CancellationToken cancellationToken) { r.Chamadas.Add("primeiro"); return Task.CompletedTask; }
    }

    private sealed class SegundoDoAviso(Registro r) : INotificationHandler<Aviso>
    {
        public async Task Handle(Aviso notification, CancellationToken cancellationToken) { await Task.Delay(10, cancellationToken); r.Chamadas.Add("segundo"); }
    }

    private sealed class DoEspecial(Registro r) : INotificationHandler<AvisoEspecial>
    {
        public Task Handle(AvisoEspecial notification, CancellationToken cancellationToken) { r.Chamadas.Add("especial"); return Task.CompletedTask; }
    }

    private static ServiceProvider Montar(Action<IServiceCollection>? extra = null)
    {
        var s = new ServiceCollection();
        s.AddSingleton<Registro>();
        s.AddMediator(typeof(MediadorProprioTests).Assembly);
        extra?.Invoke(s);
        return s.BuildServiceProvider();
    }

    [Fact]
    public async Task Send_chama_o_unico_manipulador_e_devolve_a_resposta()
    {
        using var sp = Montar();
        (await sp.GetRequiredService<IMediator>().Send(new Soma(2, 3))).Should().Be(5);
    }

    [Fact]
    public async Task Send_sem_manipulador_registrado_diz_qual_comando_ficou_sem()
    {
        using var sp = Montar();
        var act = () => sp.GetRequiredService<IMediator>().Send(new SemManipulador());
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*SemManipulador*");
    }

    [Fact]
    public async Task Excecao_do_manipulador_chega_com_o_tipo_original_e_nao_embrulhada()
    {
        using var sp = Montar();
        var act = () => sp.GetRequiredService<IMediator>().Send(new Explode());
        (await act.Should().ThrowAsync<FalhaEsperada>()).WithMessage("falhou de propósito");
    }

    [Fact]
    public async Task Publish_chama_todos_os_manipuladores_um_de_cada_vez_na_ordem_de_registro()
    {
        using var sp = Montar();
        await sp.GetRequiredService<IMediator>().Publish(new Aviso());
        sp.GetRequiredService<Registro>().Chamadas.Should().HaveCount(2).And.Contain(["primeiro", "segundo"]);
    }

    [Fact]
    public async Task Publish_de_tipo_derivado_chama_tambem_os_manipuladores_do_tipo_base()
    {
        using var sp = Montar();
        await sp.GetRequiredService<IMediator>().Publish(new AvisoEspecial());
        sp.GetRequiredService<Registro>().Chamadas.Should().BeEquivalentTo("especial", "primeiro", "segundo");
    }

    [Fact]
    public async Task Publish_sem_manipuladores_nao_faz_nada_nem_falha()
    {
        using var sp = Montar();
        await sp.GetRequiredService<IMediator>().Publish(new OutroAvisoSemManipulador());
        sp.GetRequiredService<Registro>().Chamadas.Should().BeEmpty();
    }

    private sealed class OutroAvisoSemManipulador : INotification { }

    [Fact]
    public async Task Manipulador_ja_registrado_explicitamente_antes_da_varredura_nao_roda_em_dobro()
    {
        var s = new ServiceCollection();
        s.AddSingleton<Registro>();
        s.AddScoped<INotificationHandler<Aviso>, PrimeiroDoAviso>();
        s.AddMediator(typeof(MediadorProprioTests).Assembly);
        using var sp = s.BuildServiceProvider();
        using var escopo = sp.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<IMediator>().Publish(new Aviso());
        sp.GetRequiredService<Registro>().Chamadas.Count(c => c == "primeiro").Should().Be(1);
    }

    [Fact]
    public void Manipuladores_do_sistema_sao_encontrados_pela_varredura()
    {
        var s = new ServiceCollection();
        s.AddMediator(typeof(SisMaster.WebApps.WebApi.Configurations.DependenciesConfiguration).Assembly);
        s.Count(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)).Should().BeGreaterThan(50);
    }
}
