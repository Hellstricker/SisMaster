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
        var configuration = new ConfigurationBuilder().Build();

        services.AddDependenciesConfiguration(configuration);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(SisMaster.Core.Communications.IMediatorHandler));
        Assert.NotNull(descriptor);
    }
}
