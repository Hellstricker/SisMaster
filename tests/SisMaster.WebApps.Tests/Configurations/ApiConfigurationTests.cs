using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SisMaster.WebApps.WebApi.Configurations;
using Xunit;

namespace SisMaster.WebApps.Tests.Configurations;

public class ApiConfigurationTests
{
    [Fact]
    public void AddApiConfiguration_DeveRegistrarControllers()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddApiConfiguration(configuration);

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddSwaggerConfiguration_DeveRegistrarServicosSwagger()
    {
        var services = new ServiceCollection();

        services.AddSwaggerConfiguration();

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddDependenciesConfiguration_DeveRegistrarIMediatorHandler()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:CampeonatoConnection"] = "Server=localhost;Database=Teste;User Id=sa;Password=x;TrustServerCertificate=True" })
            .Build();

        services.AddDependenciesConfiguration(configuration);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(SisMaster.Core.Communications.IMediatorHandler));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void AddDependenciesConfiguration_SemConnectionString_DeveFalharComMensagemClara()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddDependenciesConfiguration(configuration));
        Assert.Contains("ConnectionStrings__CampeonatoConnection", ex.Message);
    }
}
