using Budgie.Modules.Finances.Infrastructure;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests;

public class FinancesModuleTests
{
    [Fact]
    public void AddFinancesModule_ShouldReturnServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddFinancesModule();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddFinancesModule_ShouldRegisterCurrencyUsageChecker()
    {
        var services = new ServiceCollection();
        services.AddFinancesModule();

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ICurrencyUsageChecker));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be(typeof(CurrencyUsageChecker));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddFinancesModule_ShouldRegisterRepositories()
    {
        var services = new ServiceCollection();
        services.AddFinancesModule();

        services.FirstOrDefault(s => s.ServiceType == typeof(ICurrencyUsageChecker)).Should().NotBeNull();
        services.FirstOrDefault(s => s.ServiceType == typeof(Budgie.Modules.Finances.Application.IAccountRepository)).Should().NotBeNull();
        services.FirstOrDefault(s => s.ServiceType == typeof(Budgie.Modules.Finances.Application.ITransactionRepository)).Should().NotBeNull();
    }

    [Fact]
    public void MapFinancesEndpoints_ShouldReturnEndpointBuilder()
    {
        var app = WebApplication.Create();

        var result = app.MapFinancesEndpoints();

        result.Should().Be(app);
    }
}