using System.IO;
using DCH.IntakeAgentCore.Abstractions.DataProviders;
using DCH.IntakeAgentCore.Configuration;
using DCH.IntakeAgentCore.Util;
using DCH.CsvIntakeAgent.CoreProviders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false, true)
    .AddEnvironmentVariables();

builder.Services.AddSingleton<IDataProvider, DataProvider>();
builder.Services.AddSingleton<ICsvRecordService, CsvRecordService>();
builder.Services.AddSingleton<IDataSourceConfigurationInfoService, DataSourceConfigurationInfoService>();

var configurator = new IntakeAgentCoreConfigurator(builder.Services, builder.Configuration)
{
    AgentName = "CSV Intake Agent",
    ServiceName = "csv-intake-agent",
    Version = FileUtil.ReadVersion(),
    KeycloakClientId = "dch-intake-agent-csv",
    ThirdPartyLicenses = FileUtil.ReadThirdPartyLicenses(),
    LicenseModuleKey = "dch-intake-agent-csv"
};
configurator.AddPostgreSqlPersistence();
configurator.AddOpenTelemetry(builder);
configurator.ConfigureAdminUi(adminUiOptions =>
{
    adminUiOptions.AddAssembly(typeof(Program).Assembly);
    adminUiOptions.AddConfidentialMenuItem("Csv", "CSV Files", "./files");
});

builder.Services.AddIntakeAgentCore(configurator);

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", true);
}

app.UseIntakeAgentCore();
app.Run();

// ReSharper disable once ClassNeverInstantiated.Global
namespace DCH.CsvIntakeAgent
{
    // ReSharper disable once PartialTypeWithSinglePart
    public partial class Program;
}