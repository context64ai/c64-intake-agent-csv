using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.CoreProviders;
using DCH.CsvIntakeAgent.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace DCH.CsvIntakeAgent.Tests.CoreProviders;

public class CsvRecordServiceTests : IAsyncLifetime
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
    private ICsvRecordService _csvRecordService;

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

        // Initialize database
        await _csvRecordService.EnsureTableExistsAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }


    private const string TestCsvContent = "Name,Age,City\nJohn,25,New York\nJane,30,London\nBob,35,Paris";
    private const string TestCsvWithSemicolons = "Name;Age;City\nAlice;28;Berlin\nCharlie;32;Madrid";

    private static MockBrowserFile CreateMockCsvFile(string content, string fileName = "test.csv")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MockBrowserFile(fileName, bytes);
    }



    [Fact]
    public async Task EnsureTableExistsAsync_ShouldCreateTableSuccessfully()
    {
        // Act & Assert - Should not throw
        await FluentActions.Invoking(() => _csvRecordService.EnsureTableExistsAsync())
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureTableExistsAsync_CalledMultipleTimes_ShouldNotFail()
    {
        // Act & Assert - Should not throw even when called multiple times
        await _csvRecordService.EnsureTableExistsAsync();
        await FluentActions.Invoking(() => _csvRecordService.EnsureTableExistsAsync())
            .Should().NotThrowAsync();
    }



    [Fact]
    public async Task SaveCsvRecordAsync_WithValidFile_ShouldSaveSuccessfully()
    {
        // Arrange
        var file = CreateMockCsvFile(TestCsvContent, "sample.csv");

        // Act
        var (id, header) = await _csvRecordService.SaveCsvRecordAsync(file);

        // Assert
        id.Should().NotBeEmpty();
        header.Should().BeEquivalentTo("Name", "Age", "City");

        // Verify the record was saved
        var allRecords = await _csvRecordService.GetAllCsvRecordsAsync();
        allRecords.Should().HaveCount(1);
        allRecords[0].Id.Should().Be(id);
        allRecords[0].Name.Should().Be("sample.csv");
        allRecords[0].Header.Should().BeEquivalentTo(header);
    }

    [Fact]
    public async Task SaveCsvRecordAsync_WithSemicolonDelimitedFile_ShouldDetectDelimiterCorrectly()
    {
        // Arrange
        var file = CreateMockCsvFile(TestCsvWithSemicolons, "semicolon.csv");

        // Act
        var (id, header) = await _csvRecordService.SaveCsvRecordAsync(file);

        // Assert
        id.Should().NotBeEmpty();
        header.Should().BeEquivalentTo("Name", "Age", "City");

        // Verify delimiter was detected correctly
        var allRecords = await _csvRecordService.GetAllCsvRecordsAsync();
        var savedRecord = allRecords.First(r => r.Id == id);
        savedRecord.Delimiter.Should().Be(';');
    }

    [Fact]
    public async Task SaveCsvRecordAsync_WithMultipleFiles_ShouldSaveAllDistinctly()
    {
        // Arrange
        var file1 = CreateMockCsvFile(TestCsvContent, "file1.csv");
        var file2 = CreateMockCsvFile(TestCsvWithSemicolons, "file2.csv");

        // Act
        var (id1, _) = await _csvRecordService.SaveCsvRecordAsync(file1);
        var (id2, _) = await _csvRecordService.SaveCsvRecordAsync(file2);

        // Assert
        id1.Should().NotBe(id2);

        var allRecords = await _csvRecordService.GetAllCsvRecordsAsync();
        allRecords.Should().HaveCount(2);
        allRecords.Should().Contain(r => r.Id == id1 && r.Name == "file1.csv");
        allRecords.Should().Contain(r => r.Id == id2 && r.Name == "file2.csv");
    }



    [Fact]
    public async Task GetAllCsvRecordsAsync_WithEmptyDatabase_ShouldReturnEmptyList()
    {
        // Act
        var records = await _csvRecordService.GetAllCsvRecordsAsync();

        // Assert
        records.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllCsvRecordsAsync_WithExistingRecords_ShouldReturnAllRecords()
    {
        // Arrange
        var file1 = CreateMockCsvFile(TestCsvContent, "file1.csv");
        var file2 = CreateMockCsvFile(TestCsvWithSemicolons, "file2.csv");

        await _csvRecordService.SaveCsvRecordAsync(file1);
        await _csvRecordService.SaveCsvRecordAsync(file2);

        // Act
        var records = await _csvRecordService.GetAllCsvRecordsAsync();

        // Assert
        records.Should().HaveCount(2);
        records.Should().Contain(r => r.Name == "file1.csv");
        records.Should().Contain(r => r.Name == "file2.csv");
    }



    [Fact]
    public async Task DeleteCsvRecordAsync_WithExistingRecord_ShouldDeleteSuccessfully()
    {
        // Arrange
        var file = CreateMockCsvFile(TestCsvContent, "todelete.csv");
        var (id, _) = await _csvRecordService.SaveCsvRecordAsync(file);

        // Verify record exists
        var recordsBefore = await _csvRecordService.GetAllCsvRecordsAsync();
        recordsBefore.Should().HaveCount(1);

        // Act
        await _csvRecordService.DeleteCsvRecordAsync(id);

        // Assert
        var recordsAfter = await _csvRecordService.GetAllCsvRecordsAsync();
        recordsAfter.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCsvRecordAsync_WithNonExistentRecord_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await FluentActions.Invoking(() => _csvRecordService.DeleteCsvRecordAsync(nonExistentId))
            .Should().NotThrowAsync();
    }



    [Fact]
    public async Task GetCsvRecordsBatchedAsync_WithValidRecord_ShouldReturnDataInBatches()
    {
        // Arrange
        var file = CreateMockCsvFile(TestCsvContent, "batch_test.csv");
        var (id, _) = await _csvRecordService.SaveCsvRecordAsync(file);

        // Act
        var batches = new List<IntakeAgentCore.Abstractions.DataProviders.SourceDataBatch>();
        await foreach (var batch in _csvRecordService.GetCsvRecordsBatchedAsync(id, "test_entity", null, null, 2))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(2); // 3 rows with batch size 2 = 2 batches

        var firstBatch = batches[0];
        var firstBatchData = firstBatch.Data.ToList();
        firstBatchData.Should().HaveCount(2);
        firstBatchData[0]["Name"].Should().Be("John");
        firstBatchData[1]["Name"].Should().Be("Jane");

        var secondBatch = batches[1];
        var secondBatchData = secondBatch.Data.ToList();
        secondBatchData.Should().HaveCount(1);
        secondBatchData[0]["Name"].Should().Be("Bob");
    }

    [Fact]
    public async Task GetCsvRecordsBatchedAsync_WithSpecificProperties_ShouldReturnOnlyRequestedColumns()
    {
        // Arrange
        var file = CreateMockCsvFile(TestCsvContent, "property_filter_test.csv");
        var (id, _) = await _csvRecordService.SaveCsvRecordAsync(file);
        var propertiesToRetrieve = new[] { "Name", "City" };

        // Act
        var batches = new List<IntakeAgentCore.Abstractions.DataProviders.SourceDataBatch>();
        await foreach (var batch in _csvRecordService.GetCsvRecordsBatchedAsync(id, "test_entity", propertiesToRetrieve, null, 10))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        var batchData = batches[0].Data.ToList();

        foreach (var row in batchData)
        {
            row.Should().HaveCount(2);
            row.Should().ContainKey("Name");
            row.Should().ContainKey("City");
            row.Should().NotContainKey("Age");
        }
    }

    [Fact]
    public async Task GetCsvRecordsBatchedAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        var file = CreateMockCsvFile(TestCsvContent, "limit_test.csv");
        var (id, _) = await _csvRecordService.SaveCsvRecordAsync(file);

        // Act
        var batches = new List<IntakeAgentCore.Abstractions.DataProviders.SourceDataBatch>();
        await foreach (var batch in _csvRecordService.GetCsvRecordsBatchedAsync(id, "test_entity", null, 2, 10))
        {
            batches.Add(batch);
        }

        // Assert
        var totalRows = batches.SelectMany(b => b.Data).Count();
        totalRows.Should().Be(2);
    }

    [Fact]
    public async Task GetCsvRecordsBatchedAsync_WithNonExistentRecord_ShouldThrowException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await FluentActions.Invoking(async () =>
        {
            await foreach (var _ in _csvRecordService.GetCsvRecordsBatchedAsync(nonExistentId, "test_entity"))
            {
                // Should not reach here
            }
        })
        .Should().ThrowAsync<CsvIntakeAgentException>()
        .WithMessage($"No Record of CSV File with Id {nonExistentId} found.");
    }

    [Fact]
    public async Task GetCsvRecordsBatchedAsync_WithCustomBatchSize_ShouldUseBatchSizeCorrectly()
    {
        // Arrange
        var file = CreateMockCsvFile(TestCsvContent, "batch_size_test.csv");
        var (id, _) = await _csvRecordService.SaveCsvRecordAsync(file);

        // Act
        var batches = new List<IntakeAgentCore.Abstractions.DataProviders.SourceDataBatch>();
        await foreach (var batch in _csvRecordService.GetCsvRecordsBatchedAsync(id, "test_entity", null, null, 1))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(3); // 3 rows with batch size 1 = 3 batches
        batches.Should().AllSatisfy(batch => batch.Data.Should().HaveCount(1));
    }



    [Fact]
    public async Task IntegrationTest_CompleteWorkflow_ShouldWorkEndToEnd()
    {
        // Arrange
        var originalFile = CreateMockCsvFile(TestCsvContent, "integration_test.csv");

        // Act 1: Save CSV record
        var (savedId, savedHeader) = await _csvRecordService.SaveCsvRecordAsync(originalFile);

        // Act 2: Retrieve all records
        var allRecords = await _csvRecordService.GetAllCsvRecordsAsync();

        // Act 3: Get data in batches
        var batches = new List<IntakeAgentCore.Abstractions.DataProviders.SourceDataBatch>();
        await foreach (var batch in _csvRecordService.GetCsvRecordsBatchedAsync(savedId, "integration_test", null, null, 2))
        {
            batches.Add(batch);
        }

        // Act 4: Delete the record
        await _csvRecordService.DeleteCsvRecordAsync(savedId);

        // Act 5: Verify deletion
        var recordsAfterDeletion = await _csvRecordService.GetAllCsvRecordsAsync();

        // Assert
        savedId.Should().NotBeEmpty();
        savedHeader.Should().BeEquivalentTo("Name", "Age", "City");

        allRecords.Should().HaveCount(1);
        allRecords[0].Id.Should().Be(savedId);
        allRecords[0].Name.Should().Be("integration_test.csv");

        batches.Should().HaveCount(2);
        var totalRows = batches.SelectMany(b => b.Data).Count();
        totalRows.Should().Be(3);

        recordsAfterDeletion.Should().BeEmpty();
    }

}

// Mock implementation of IBrowserFile for testing
public class MockBrowserFile(string name, byte[] content) : IBrowserFile
{
    public string Name { get; } = name;
    public DateTimeOffset LastModified { get; } = DateTimeOffset.Now;
    public long Size { get; } = content.Length;
    public string ContentType => "text/csv";

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        return Size > maxAllowedSize 
            ? throw new IOException($"The file size {Size} exceeds the maximum allowed size {maxAllowedSize}.") 
            : new MemoryStream(content);
    }
}