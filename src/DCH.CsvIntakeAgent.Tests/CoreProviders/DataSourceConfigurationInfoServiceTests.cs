using System;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.CoreProviders;
using DCH.CsvIntakeAgent.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace DCH.CsvIntakeAgent.Tests.CoreProviders;

public class DataSourceConfigurationInfoServiceTests : IAsyncLifetime
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
    private IDataSourceConfigurationInfoService _service;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        
        _factory = new AgentTestAppFactory(
            _postgresContainer.Hostname,
            _postgresContainer.GetMappedPublicPort(5432).ToString(),
            PostgresUser,
            PostgresPassword,
            TestDatabaseName);

        _service = _factory.Services.GetRequiredService<IDataSourceConfigurationInfoService>();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }


    private static DataSourceConfigurationInfo CreateTestConfig(string name = "TestConfig", string slug = "test-slug")
    {
        return new DataSourceConfigurationInfo
        {
            Id = Guid.NewGuid(),
            CsvRecordId = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            IsEnabled = true,
            ImportJobBatchSize = 100,
            Entities =
            [
                new SourceEntityInfo
                {
                    Name = "Entity1",
                    Properties =
                    [
                        new SourcePropertyInfo { Name = "Property1" },
                        new SourcePropertyInfo { Name = "Property2" }
                    ]
                }
            ]
        };
    }



    [Fact]
    public async Task GetAll_WithEmptyRepository_ShouldReturnEmptyList()
    {
        // Act
        var result = await _service.GetAll();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_WithConfigurations_ShouldReturnAllConfigurations()
    {
        // Arrange
        var config1 = CreateTestConfig("Config1", "slug1");
        var config2 = CreateTestConfig("Config2", "slug2");

        await _service.AddOrUpdate(config1);
        await _service.AddOrUpdate(config2);

        // Act
        var result = await _service.GetAll();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Name == "Config1");
        result.Should().Contain(c => c.Name == "Config2");
    }

    [Fact]
    public async Task GetAll_WithFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var config1 = CreateTestConfig("EnabledConfig", "enabled-slug");
        config1.IsEnabled = true;

        var config2 = CreateTestConfig("DisabledConfig", "disabled-slug");
        config2.IsEnabled = false;

        await _service.AddOrUpdate(config1);
        await _service.AddOrUpdate(config2);

        // Act
        var result = await _service.GetAll(c => c.IsEnabled);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("EnabledConfig");
    }



    [Fact]
    public async Task AddOrUpdate_WithNewConfiguration_ShouldAddSuccessfully()
    {
        // Arrange
        var config = CreateTestConfig("NewConfig", "new-slug");
        config.Id = Guid.Empty; // Simulate new configuration

        // Act
        var result = await _service.AddOrUpdate(config);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("NewConfig");
        result.Slug.Should().Be("new-slug");

        // Verify it was saved
        var allConfigs = await _service.GetAll();
        allConfigs.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddOrUpdate_WithExistingConfiguration_ShouldUpdateSuccessfully()
    {
        // Arrange
        var originalConfig = CreateTestConfig("OriginalConfig", "original-slug");
        await _service.AddOrUpdate(originalConfig);

        var updatedConfig = CreateTestConfig("UpdatedConfig", "updated-slug");
        updatedConfig.Id = originalConfig.Id;

        // Act
        var result = await _service.AddOrUpdate(updatedConfig);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(originalConfig.Id);
        result.Name.Should().Be("UpdatedConfig");
        result.Slug.Should().Be("updated-slug");

        // Verify only one configuration exists
        var allConfigs = await _service.GetAll();
        allConfigs.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddOrUpdate_WithDuplicateName_ShouldReturnNull()
    {
        // Arrange
        var existingConfig = CreateTestConfig("DuplicateName", "slug1");
        await _service.AddOrUpdate(existingConfig);

        var newConfig = CreateTestConfig("DuplicateName", "slug2");
        newConfig.Id = Guid.NewGuid(); // Different ID

        // Act
        var result = await _service.AddOrUpdate(newConfig);

        // Assert
        result.Should().BeNull();

        // Verify original configuration still exists
        var allConfigs = await _service.GetAll();
        allConfigs.Should().HaveCount(1);
        allConfigs[0].Slug.Should().Be("slug1");
    }

    [Fact]
    public async Task AddOrUpdate_WithDuplicateSlug_ShouldReturnNull()
    {
        // Arrange
        var existingConfig = CreateTestConfig("Name1", "duplicate-slug");
        await _service.AddOrUpdate(existingConfig);

        var newConfig = CreateTestConfig("Name2", "duplicate-slug");
        newConfig.Id = Guid.NewGuid(); // Different ID

        // Act
        var result = await _service.AddOrUpdate(newConfig);

        // Assert
        result.Should().BeNull();

        // Verify original configuration still exists
        var allConfigs = await _service.GetAll();
        allConfigs.Should().HaveCount(1);
        allConfigs[0].Name.Should().Be("Name1");
    }

    [Fact]
    public async Task AddOrUpdate_WithSameIdButDifferentNameAndSlug_ShouldUpdateSuccessfully()
    {
        // Arrange
        var originalConfig = CreateTestConfig("OriginalName", "original-slug");
        await _service.AddOrUpdate(originalConfig);

        var updatedConfig = CreateTestConfig("UpdatedName", "updated-slug");
        updatedConfig.Id = originalConfig.Id; // Same ID, so this should be an update

        // Act
        var result = await _service.AddOrUpdate(updatedConfig);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(originalConfig.Id);
        result.Name.Should().Be("UpdatedName");
        result.Slug.Should().Be("updated-slug");
    }

    [Fact]
    public async Task Delete_WithExistingConfiguration_ShouldDeleteSuccessfully()
    {
        // Arrange
        var config = CreateTestConfig("ToDelete", "delete-slug");
        await _service.AddOrUpdate(config);

        // Verify it exists
        var configsBefore = await _service.GetAll();
        configsBefore.Should().HaveCount(1);

        // Act
        await _service.Delete(config.Id);

        // Assert
        var configsAfter = await _service.GetAll();
        configsAfter.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_WithNonExistentConfiguration_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await FluentActions.Invoking(() => _service.Delete(nonExistentId))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task IntegrationTest_CompleteWorkflow_ShouldWorkEndToEnd()
    {
        // Arrange
        var config1 = CreateTestConfig("Config1", "config1");
        var config2 = CreateTestConfig("Config2", "config2");

        // Act 1: Add configurations
        var savedConfig1 = await _service.AddOrUpdate(config1);
        var savedConfig2 = await _service.AddOrUpdate(config2);

        // Act 2: Get all configurations
        var allConfigs = await _service.GetAll();

        // Act 3: Update a configuration
        savedConfig1.Name = "UpdatedConfig1";
        savedConfig1.ImportJobBatchSize = 200;
        var updatedConfig = await _service.AddOrUpdate(savedConfig1);

        // Act 4: Delete a configuration
        await _service.Delete(savedConfig2.Id);

        // Act 5: Final verification
        var finalConfigs = await _service.GetAll();

        // Assert
        savedConfig1.Should().NotBeNull();
        savedConfig2.Should().NotBeNull();

        allConfigs.Should().HaveCount(2);

        updatedConfig.Should().NotBeNull();
        updatedConfig.Name.Should().Be("UpdatedConfig1");
        updatedConfig.ImportJobBatchSize.Should().Be(200);

        finalConfigs.Should().HaveCount(1);
        finalConfigs[0].Name.Should().Be("UpdatedConfig1");
    }

}