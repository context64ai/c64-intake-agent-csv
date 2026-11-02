using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.CoreProviders;
using DCH.CsvIntakeAgent.Domain;
using DCH.IntakeAgentCore.Abstractions.DataProviders;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace DCH.CsvIntakeAgent.Tests.CoreProviders;

public class DataProviderTests : IAsyncLifetime
{
    private const string TestDatabaseName = "testdb";
    private const string PostgresUser = "testuser";
    private const string PostgresPassword = "testpass";

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:17")
        .WithDatabase(TestDatabaseName)
        .WithUsername(PostgresUser)
        .WithPassword(PostgresPassword)
        .Build();

    private AgentTestAppFactory _factory;
    private DataProvider _dataProvider;
    private ICsvRecordService _csvRecordService;
    private IDataSourceConfigurationInfoService _configService;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        
        _factory = new AgentTestAppFactory(
            _postgresContainer.Hostname,
            _postgresContainer.GetMappedPublicPort(5432).ToString(),
            PostgresUser,
            PostgresPassword,
            TestDatabaseName);

        _csvRecordService = _factory.Services.GetRequiredService<ICsvRecordService>();
        _configService = _factory.Services.GetRequiredService<IDataSourceConfigurationInfoService>();
        _dataProvider = _factory.Services.GetRequiredService<DataProvider>();

        // Initialize database
        await _csvRecordService.EnsureTableExistsAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }
    
    private const string TestCsvContent = "Name,Age,City\nJohn,25,New York\nJane,30,London\nBob,35,Paris";

    private static MockBrowserFile CreateMockCsvFile(string content, string fileName = "test.csv")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MockBrowserFile(fileName, bytes);
    }

    private static DataSourceConfigurationInfo CreateTestConfiguration(
        string name = "TestConfig",
        string slug = "test-config",
        bool isEnabled = true,
        Guid? csvRecordId = null)
    {
        return new DataSourceConfigurationInfo
        {
            Id = Guid.NewGuid(),
            CsvRecordId = csvRecordId ?? Guid.NewGuid(),
            Name = name,
            Slug = slug,
            IsEnabled = isEnabled,
            ImportJobBatchSize = 100,
            Entities =
            [
                new SourceEntityInfo
                {
                    Name = "Users",
                    Properties =
                    [
                        new SourcePropertyInfo { Name = "Name" },
                        new SourcePropertyInfo { Name = "Age" },
                        new SourcePropertyInfo { Name = "City" }
                    ]
                },
                new SourceEntityInfo
                {
                    Name = "UserProfiles",
                    Properties =
                    [
                        new SourcePropertyInfo { Name = "Name" },
                        new SourcePropertyInfo { Name = "City" }
                    ]
                }
            ]
        };
    }

    private static DataFetchParameters CreateDataFetchParameters(
        string dsConfigNameOrSlug,
        string entity = "Users",
        IEnumerable<string> properties = null,
        int? limit = null,
        int? batchSize = null)
    {
        return new DataFetchParameters
        {
            DsConfigNameOrSlug = dsConfigNameOrSlug,
            Entity = entity,
            Properties = properties,
            Limit = limit,
            BatchSize = batchSize
        };
    }
    
    [Fact]
    public async Task GetConfiguration_WithExistingName_ShouldReturnConfiguration()
    {
        // Arrange
        var config = CreateTestConfiguration("TestConfig", "test-slug");
        await _configService.AddOrUpdate(config);

        // Act
        var result = await _dataProvider.GetConfiguration("TestConfig");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("TestConfig");
        result.Slug.Should().Be("test-slug");
        result.Entities.Should().HaveCount(2);
        result.ImportJobBatchSize.Should().Be(100);
    }

    [Fact]
    public async Task GetConfiguration_WithExistingSlug_ShouldReturnConfiguration()
    {
        // Arrange
        var config = CreateTestConfiguration("TestConfig", "test-slug");
        await _configService.AddOrUpdate(config);

        // Act
        var result = await _dataProvider.GetConfiguration("test-slug");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("TestConfig");
        result.Slug.Should().Be("test-slug");
    }

    [Fact]
    public async Task GetConfiguration_WithNonExistentNameOrSlug_ShouldReturnNull()
    {
        // Act
        var result = await _dataProvider.GetConfiguration("NonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetConfiguration_WithMultipleConfigs_ShouldReturnFirst()
    {
        // Arrange
        var config1 = CreateTestConfiguration("SameName", "slug1");
        var config2 = CreateTestConfiguration("SameName", "slug2");

        await _configService.AddOrUpdate(config1);
        await _configService.AddOrUpdate(config2);

        // Act
        var result = await _dataProvider.GetConfiguration("SameName");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("SameName");
        // Should return the first one found
        result.Slug.Should().BeOneOf("slug1", "slug2");
    }



    [Fact]
    public async Task GetConfigurations_WithNoConfigurations_ShouldReturnEmptyList()
    {
        // Act
        var result = await _dataProvider.GetConfigurations();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConfigurations_WithEnabledConfigurations_ShouldReturnOnlyEnabled()
    {
        // Arrange
        var enabledConfig1 = CreateTestConfiguration("Enabled1", "enabled1", isEnabled: true);
        var enabledConfig2 = CreateTestConfiguration("Enabled2", "enabled2", isEnabled: true);
        var disabledConfig = CreateTestConfiguration("Disabled", "disabled", isEnabled: false);

        await _configService.AddOrUpdate(enabledConfig1);
        await _configService.AddOrUpdate(enabledConfig2);
        await _configService.AddOrUpdate(disabledConfig);

        // Act
        var resultEnum = await _dataProvider.GetConfigurations();
        var result = resultEnum.ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(c => c.Name == "Enabled1" || c.Name == "Enabled2");
        result.Should().NotContain(c => c.Name == "Disabled");
    }

    [Fact]
    public async Task GetConfigurations_WithAllDisabledConfigurations_ShouldReturnEmptyList()
    {
        // Arrange
        var disabledConfig1 = CreateTestConfiguration("Disabled1", "disabled1", isEnabled: false);
        var disabledConfig2 = CreateTestConfiguration("Disabled2", "disabled2", isEnabled: false);

        await _configService.AddOrUpdate(disabledConfig1);
        await _configService.AddOrUpdate(disabledConfig2);

        // Act
        var result = await _dataProvider.GetConfigurations();

        // Assert
        result.Should().BeEmpty();
    }



    [Fact]
    public async Task GetData_WithValidConfiguration_ShouldReturnData()
    {
        // Arrange
        var csvFile = CreateMockCsvFile(TestCsvContent, "users.csv");
        var (csvRecordId, _) = await _csvRecordService.SaveCsvRecordAsync(csvFile);

        var config = CreateTestConfiguration("UserData", "user-data", csvRecordId: csvRecordId);
        await _configService.AddOrUpdate(config);

        var parameters = CreateDataFetchParameters("UserData");

        // Act
        var batches = new List<SourceDataBatch>();
        await foreach (var batch in _dataProvider.GetData(parameters))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().NotBeEmpty();
        var totalRows = batches.SelectMany(b => b.Data).Count();
        totalRows.Should().Be(3); // John, Jane, Bob

        var firstRow = batches[0].Data.First();
        firstRow.Should().ContainKey("Name");
        firstRow.Should().ContainKey("Age");
        firstRow.Should().ContainKey("City");
    }

    [Fact]
    public async Task GetData_WithSpecificProperties_ShouldReturnOnlyRequestedProperties()
    {
        // Arrange
        var csvFile = CreateMockCsvFile(TestCsvContent, "users.csv");
        var (csvRecordId, _) = await _csvRecordService.SaveCsvRecordAsync(csvFile);

        var config = CreateTestConfiguration("UserData", "user-data", csvRecordId: csvRecordId);
        await _configService.AddOrUpdate(config);

        var parameters = CreateDataFetchParameters("UserData", properties: ["Name", "City"]);

        // Act
        var batches = new List<SourceDataBatch>();
        await foreach (var batch in _dataProvider.GetData(parameters))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().NotBeEmpty();
        var firstRow = batches[0].Data.First();
        firstRow.Should().ContainKey("Name");
        firstRow.Should().ContainKey("City");
        firstRow.Should().NotContainKey("Age");
    }

    [Fact]
    public async Task GetData_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        var csvFile = CreateMockCsvFile(TestCsvContent, "users.csv");
        var (csvRecordId, _) = await _csvRecordService.SaveCsvRecordAsync(csvFile);

        var config = CreateTestConfiguration("UserData", "user-data", csvRecordId: csvRecordId);
        await _configService.AddOrUpdate(config);

        var parameters = CreateDataFetchParameters("UserData", limit: 2);

        // Act
        var batches = new List<SourceDataBatch>();
        await foreach (var batch in _dataProvider.GetData(parameters))
        {
            batches.Add(batch);
        }

        // Assert
        var totalRows = batches.SelectMany(b => b.Data).Count();
        totalRows.Should().Be(2);
    }

    [Fact]
    public async Task GetData_WithCustomBatchSize_ShouldUseBatchSize()
    {
        // Arrange
        var csvFile = CreateMockCsvFile(TestCsvContent, "users.csv");
        var (csvRecordId, _) = await _csvRecordService.SaveCsvRecordAsync(csvFile);

        var config = CreateTestConfiguration("UserData", "user-data", csvRecordId: csvRecordId);
        await _configService.AddOrUpdate(config);

        var parameters = CreateDataFetchParameters("UserData", batchSize: 1);

        // Act
        var batches = new List<SourceDataBatch>();
        await foreach (var batch in _dataProvider.GetData(parameters))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(3); // 3 rows with batch size 1 = 3 batches
        batches.Should().AllSatisfy(batch => batch.Data.Should().HaveCount(1));
    }

    [Fact]
    public async Task GetData_WithNonExistentConfiguration_ShouldThrowException()
    {
        // Arrange
        var parameters = CreateDataFetchParameters("NonExistentConfig");

        // Act & Assert
        await FluentActions.Invoking(async () =>
        {
            await foreach (var _ in _dataProvider.GetData(parameters))
            {
                // Should not reach here
            }
        })
        .Should().ThrowAsync<Exception>()
        .WithMessage("Configuration NonExistentConfig not found");
    }

    [Fact]
    public async Task GetData_WithNonExistentEntity_ShouldThrowException()
    {
        // Arrange
        var csvFile = CreateMockCsvFile(TestCsvContent, "users.csv");
        var (csvRecordId, _) = await _csvRecordService.SaveCsvRecordAsync(csvFile);

        var config = CreateTestConfiguration("UserData", "user-data", csvRecordId: csvRecordId);
        await _configService.AddOrUpdate(config);

        var parameters = CreateDataFetchParameters("UserData", "NonExistentEntity");

        // Act & Assert
        await FluentActions.Invoking(async () =>
        {
            await foreach (var _ in _dataProvider.GetData(parameters))
            {
                // Should not reach here
            }
        })
        .Should().ThrowAsync<Exception>()
        .WithMessage("Entity NonExistentEntity not found");
    }

    [Fact]
    public async Task GetData_WithDifferentEntity_ShouldReturnCorrectEntityData()
    {
        // Arrange
        var csvFile = CreateMockCsvFile(TestCsvContent, "users.csv");
        var (csvRecordId, _) = await _csvRecordService.SaveCsvRecordAsync(csvFile);

        var config = CreateTestConfiguration("UserData", "user-data", csvRecordId: csvRecordId);
        await _configService.AddOrUpdate(config);

        var parameters = CreateDataFetchParameters("UserData", "UserProfiles", properties: ["Name", "City"]);

        // Act
        var batches = new List<SourceDataBatch>();
        await foreach (var batch in _dataProvider.GetData(parameters))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().NotBeEmpty();
        var firstRow = batches[0].Data.First();
        firstRow.Should().ContainKey("Name");
        firstRow.Should().ContainKey("City");
        firstRow.Should().NotContainKey("Age"); // UserProfiles entity doesn't include Age
    }

    [Fact]
    public async Task GetData_WithConfigurationFoundBySlug_ShouldWork()
    {
        // Arrange
        var csvFile = CreateMockCsvFile(TestCsvContent, "users.csv");
        var (csvRecordId, _) = await _csvRecordService.SaveCsvRecordAsync(csvFile);

        var config = CreateTestConfiguration("UserData", "user-data-slug", csvRecordId: csvRecordId);
        await _configService.AddOrUpdate(config);

        var parameters = CreateDataFetchParameters("user-data-slug"); // Using slug

        // Act
        var batches = new List<SourceDataBatch>();
        await foreach (var batch in _dataProvider.GetData(parameters))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().NotBeEmpty();
        var totalRows = batches.SelectMany(b => b.Data).Count();
        totalRows.Should().Be(3);
    }



    [Fact]
    public async Task IntegrationTest_CompleteWorkflow_ShouldWorkEndToEnd()
    {
        // Arrange: Create CSV data
        var csvFile = CreateMockCsvFile(TestCsvContent, "integration_test.csv");
        var (csvRecordId, header) = await _csvRecordService.SaveCsvRecordAsync(csvFile);

        // Arrange: Create configuration
        var config1 = CreateTestConfiguration("Config1", "config1", isEnabled: true, csvRecordId: csvRecordId);
        var config2 = CreateTestConfiguration("Config2", "config2", isEnabled: false, csvRecordId: csvRecordId);

        await _configService.AddOrUpdate(config1);
        await _configService.AddOrUpdate(config2);

        // Act 1: Get all configurations (should only return enabled)
        var allConfigsEnum = await _dataProvider.GetConfigurations();
        var allConfigs = allConfigsEnum.ToList();

        // Act 2: Get a specific configuration by name
        var specificConfig = await _dataProvider.GetConfiguration("Config1");

        // Act 3: Get a specific configuration by slug
        var configBySlug = await _dataProvider.GetConfiguration("config1");

        // Act 4: Get data from the configuration
        var parameters = CreateDataFetchParameters("Config1", batchSize: 2);
        var batches = new List<SourceDataBatch>();
        await foreach (var batch in _dataProvider.GetData(parameters))
        {
            batches.Add(batch);
        }

        // Assert
        allConfigs.Should().HaveCount(1);
        allConfigs.First().Name.Should().Be("Config1");

        specificConfig.Should().NotBeNull();
        specificConfig.Name.Should().Be("Config1");

        configBySlug.Should().NotBeNull();
        configBySlug.Name.Should().Be("Config1");

        batches.Should().HaveCount(2); // 3 rows with batch size 2 = 2 batches
        var totalRows = batches.SelectMany(b => b.Data).Count();
        totalRows.Should().Be(3);

        // Verify CSV header matches
        header.Should().BeEquivalentTo("Name", "Age", "City");
    }

}

