using System.Collections.Generic;
using System.IO;
using DCH.CsvIntakeAgent.CoreProviders;
using DCH.IntakeAgentCore.Abstractions.DataProviders;
using DCH.IntakeAgentCore.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DCH.CsvIntakeAgent.Tests;

public class AgentTestAppFactory(
    string postgresHost,
    string postgresPort,
    string postgresUser,
    string postgresPassword,
    string postgresDatabase) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection([
                new KeyValuePair<string, string>(SharedEnvironmentVariables.PostgresHost, postgresHost),
                new KeyValuePair<string, string>(SharedEnvironmentVariables.PostgresPort, postgresPort),
                new KeyValuePair<string, string>(SharedEnvironmentVariables.PostgresUser, postgresUser),
                new KeyValuePair<string, string>(SharedEnvironmentVariables.PostgresPassword, postgresPassword),
                new KeyValuePair<string, string>(SharedEnvironmentVariables.PostgresDatabase, postgresDatabase),
                new KeyValuePair<string, string>(SharedEnvironmentVariables.DchKeycloakUrl, "http://localhost:8080"),
                new KeyValuePair<string, string>(SharedEnvironmentVariables.DchKeycloakRealm, "test-realm"),
            ]);
        });
    }

    protected override IHostBuilder CreateHostBuilder()
    {
        var factoryAssembly = typeof(AgentTestAppFactory).Assembly;
        var root = Path.Combine(
            Path.GetDirectoryName(factoryAssembly.Location)!,
            "..", "..", ".."
        );

        var contentRootKey = $"TEST_CONTENTROOT_{factoryAssembly.GetName().Name!.Replace(".", "_").ToUpperInvariant()}";

        return new HostBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseSetting(contentRootKey, root)
                    .UseEnvironment("Test")
                    .UseStartup<Startup>();
            });
    }
}

public class Startup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        var configurator = new IntakeAgentCoreConfigurator(services, configuration)
        {
            AgentName = "TestAgent",
            ServiceName = "agent-app",
            Version = "1.0.0",
            KeycloakClientId = "clientId"
        };
        configurator.AddPostgreSqlPersistence();

        services.AddIntakeAgentCore(configurator);
        services.AddSingleton<ICsvRecordService, CsvRecordService>();
        services.AddSingleton<IDataSourceConfigurationInfoService, DataSourceConfigurationInfoService>();
        services.AddSingleton<DataProvider>();
        services.AddSingleton<IDataProvider>(provider => provider.GetRequiredService<DataProvider>());
    }

    public static void Configure(IApplicationBuilder app, IWebHostEnvironment env) {}
}
