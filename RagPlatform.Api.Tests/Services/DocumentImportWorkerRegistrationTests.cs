using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
public sealed class DocumentImportWorkerRegistrationTests
{
    [TestMethod]
    public void WorkerDisabledByDefault_IsNotRegistered()
    {
        using var factory = new WebApplicationFactory<Program>();

        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var hostedServices = factory.Services.GetServices<IHostedService>();

        Assert.IsFalse(configuration.GetValue<bool>("ServiceBus:DocumentImportWorkerEnabled"));
        Assert.IsFalse(hostedServices.Any(service => service is DocumentImportBackgroundService));
    }

    [TestMethod]
    public void WorkerExplicitlyDisabled_IsNotRegistered()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ServiceBus:DocumentImportWorkerEnabled"] = "false"
                })));

        var hostedServices = factory.Services.GetServices<IHostedService>();

        Assert.IsFalse(hostedServices.Any(service => service is DocumentImportBackgroundService));
    }
}
