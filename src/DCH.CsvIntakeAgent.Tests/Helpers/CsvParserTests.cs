using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;
using CsvParser = DCH.CsvIntakeAgent.Helpers.CsvParser;

namespace DCH.CsvIntakeAgent.Tests.Helpers;

public class CsvParserTests
{

    private const string CommaDelimitedCsv = "Name,Age,City\nJohn,25,New York\nJane,30,London\nBob,35,Paris";
    private const string SemicolonDelimitedCsv = "Name;Age;City\nJohn;25;New York\nJane;30;London\nBob;35;Paris";
    private const string NoDataRowsCsv = "Name,Age,City\n";
    private const string SingleRowCsv = "Name,Age,City\nJohn,25,New York";

    private static MemoryStream CreateMemoryStreamFromString(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MemoryStream(bytes);
    }

    private static MockBrowserFile CreateMockBrowserFile(string content, string fileName = "test.csv")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MockBrowserFile(fileName, bytes);
    }



    [Fact]
    public async Task ToMemoryStream_ShouldConvertBrowserFileToMemoryStream()
    {
        // Arrange
        var file = CreateMockBrowserFile(CommaDelimitedCsv);

        // Act
        var result = await CsvParser.ToMemoryStream(file);

        // Assert
        result.Should().NotBeNull();
        result.Position.Should().Be(0);

        var content = Encoding.UTF8.GetString(result.ToArray());
        content.Should().Be(CommaDelimitedCsv);
    }

    [Fact]
    public async Task ToMemoryStream_WithEmptyFile_ShouldReturnEmptyMemoryStream()
    {
        // Arrange
        var file = CreateMockBrowserFile("");

        // Act
        var result = await CsvParser.ToMemoryStream(file);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(0);
    }



    [Fact]
    public async Task Validate_WithValidCsv_ShouldNotThrow()
    {
        // Act & Assert
        await FluentActions.Invoking(() =>
            {
                using var stream = CreateMemoryStreamFromString(CommaDelimitedCsv);
                return CsvParser.Validate(stream);
            })
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task Validate_WithNoDataRows_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        await FluentActions.Invoking(() =>
            {
                using var stream = CreateMemoryStreamFromString(NoDataRowsCsv);
                return CsvParser.Validate(stream);
            })
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("CSV file contains no data rows");
    }

    [Fact]
    public async Task Validate_WithSemicolonDelimiter_ShouldNotThrow()
    {
        // Act & Assert
        await FluentActions.Invoking(() =>
            {
                using var stream = CreateMemoryStreamFromString(SemicolonDelimitedCsv);
                return CsvParser.Validate(stream);
            })
            .Should().NotThrowAsync();
    }



    [Fact]
    public void FilterHeader_WithSpecificProperties_ShouldReturnMatchingColumns()
    {
        // Arrange
        var header = new[] { "Name", "Age", "City", "Country" };
        var propertiesToRetrieve = new List<string> { "Name", "City" };

        // Act
        var result = CsvParser.FilterHeader(header, propertiesToRetrieve);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain((0, "Name"));
        result.Should().Contain((2, "City"));
    }

    [Fact]
    public void FilterHeader_WithNullProperties_ShouldReturnAllColumns()
    {
        // Arrange
        var header = new[] { "Name", "Age", "City" };

        // Act
        var result = CsvParser.FilterHeader(header, null);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain((0, "Name"));
        result.Should().Contain((1, "Age"));
        result.Should().Contain((2, "City"));
    }

    [Fact]
    public void FilterHeader_WithNonExistentProperties_ShouldReturnEmptyList()
    {
        // Arrange
        var header = new[] { "Name", "Age", "City" };
        var propertiesToRetrieve = new List<string> { "NonExistent1", "NonExistent2" };

        // Act
        var result = CsvParser.FilterHeader(header, propertiesToRetrieve);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterHeader_WithEmptyProperties_ShouldReturnEmptyList()
    {
        // Arrange
        var header = new[] { "Name", "Age", "City" };
        var propertiesToRetrieve = new List<string>();

        // Act
        var result = CsvParser.FilterHeader(header, propertiesToRetrieve);

        // Assert
        result.Should().BeEmpty();
    }



    [Fact]
    public async Task GetCsvDataBatchesAsync_WithValidData_ShouldReturnCorrectBatches()
    {
        // Arrange
        var csvRecord = new CsvRecord
        {
            Data = Encoding.UTF8.GetBytes(CommaDelimitedCsv),
            Header = ["Name", "Age", "City"]
        };
        var columnIndices = new List<(int index, string columnName)> { (0, "Name"), (1, "Age") };

        // Act
        var batches = new List<List<Dictionary<string, string>>>();
        await foreach (var batch in CsvParser.GetCsvDataBatchesAsync(csvRecord, columnIndices, null, 2))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(2);
        batches[0].Should().HaveCount(2);
        batches[1].Should().HaveCount(1);

        batches[0][0]["Name"].Should().Be("John");
        batches[0][0]["Age"].Should().Be("25");
        batches[0][1]["Name"].Should().Be("Jane");
        batches[0][1]["Age"].Should().Be("30");
        batches[1][0]["Name"].Should().Be("Bob");
        batches[1][0]["Age"].Should().Be("35");
    }

    [Fact]
    public async Task GetCsvDataBatchesAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        var csvRecord = new CsvRecord
        {
            Data = Encoding.UTF8.GetBytes(CommaDelimitedCsv),
            Header = ["Name", "Age", "City"]
        };
        var columnIndices = new List<(int index, string columnName)> { (0, "Name") };

        // Act
        var batches = new List<List<Dictionary<string, string>>>();
        await foreach (var batch in CsvParser.GetCsvDataBatchesAsync(csvRecord, columnIndices, 2, 10))
        {
            batches.Add(batch);
        }

        // Assert
        var totalRows = batches.SelectMany(b => b).Count();
        totalRows.Should().Be(2);
    }

    [Fact]
    public async Task GetCsvDataBatchesAsync_WithEmptyData_ShouldReturnEmptyBatches()
    {
        // Arrange
        var csvRecord = new CsvRecord
        {
            Data = Encoding.UTF8.GetBytes(NoDataRowsCsv),
            Header = ["Name", "Age", "City"]
        };
        var columnIndices = new List<(int index, string columnName)> { (0, "Name") };

        // Act
        var batches = new List<List<Dictionary<string, string>>>();
        await foreach (var batch in CsvParser.GetCsvDataBatchesAsync(csvRecord, columnIndices, null, 10))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().BeEmpty();
    }



    [Fact]
    public async Task ConvertCsvRecordToPreviewData_WithValidData_ShouldReturnPreviewData()
    {
        // Arrange
        var csvRecord = new CsvRecord
        {
            Data = Encoding.UTF8.GetBytes(CommaDelimitedCsv),
            Header = ["Name", "Age", "City"]
        };

        // Act
        var result = await CsvParser.ConvertCsvRecordToPreviewData(csvRecord);

        // Assert
        result.Should().HaveCount(3);
        result[0]["Name"].Should().Be("John");
        result[0]["Age"].Should().Be("25");
        result[0]["City"].Should().Be("New York");
        result[1]["Name"].Should().Be("Jane");
        result[2]["Name"].Should().Be("Bob");
    }

    [Fact]
    public async Task ConvertCsvRecordToPreviewData_WithNullData_ShouldReturnEmptyList()
    {
        // Arrange
        var csvRecord = new CsvRecord
        {
            Data = null,
            Header = ["Name", "Age", "City"]
        };

        // Act
        var result = await CsvParser.ConvertCsvRecordToPreviewData(csvRecord);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ConvertCsvRecordToPreviewData_WithNullHeader_ShouldReturnEmptyList()
    {
        // Arrange
        var csvRecord = new CsvRecord
        {
            Data = Encoding.UTF8.GetBytes(CommaDelimitedCsv),
            Header = null
        };

        // Act
        var result = await CsvParser.ConvertCsvRecordToPreviewData(csvRecord);

        // Assert
        result.Should().BeEmpty();
    }



    [Fact]
    public async Task ExtractCsvDataAsync_WithValidFile_ShouldReturnCorrectData()
    {
        // Arrange
        var file = CreateMockBrowserFile(CommaDelimitedCsv);

        // Act
        var (content, rowCount, header, delimiter) = await CsvParser.ExtractCsvDataAsync(file);

        // Assert
        content.Should().NotBeNull();
        content.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(CommaDelimitedCsv));
        rowCount.Should().Be(3);
        header.Should().BeEquivalentTo("Name", "Age", "City");
        delimiter.Should().Be(',');
    }

    [Fact]
    public async Task ExtractCsvDataAsync_WithSemicolonDelimiter_ShouldDetectCorrectDelimiter()
    {
        // Arrange
        var file = CreateMockBrowserFile(SemicolonDelimitedCsv);

        // Act
        var (_, _, _, delimiter) = await CsvParser.ExtractCsvDataAsync(file);

        // Assert
        delimiter.Should().Be(';');
    }

    [Fact]
    public async Task ExtractCsvDataAsync_WithSingleRow_ShouldReturnCorrectRowCount()
    {
        // Arrange
        var file = CreateMockBrowserFile(SingleRowCsv);

        // Act
        var (_, rowCount, _, _) = await CsvParser.ExtractCsvDataAsync(file);

        // Assert
        rowCount.Should().Be(1);
    }



    [Fact]
    public async Task ExtractHeader_FromBrowserFile_ShouldReturnCorrectHeaders()
    {
        // Arrange
        var file = CreateMockBrowserFile(CommaDelimitedCsv);

        // Act
        var headers = await CsvParser.ExtractHeader(file);

        // Assert
        headers.Should().BeEquivalentTo("Name", "Age", "City");
    }

    [Fact]
    public async Task ExtractHeader_FromMemoryStream_ShouldReturnCorrectHeaders()
    {
        // Arrange
        using var stream = CreateMemoryStreamFromString(CommaDelimitedCsv);

        // Act
        var headers = await CsvParser.ExtractHeader(stream);

        // Assert
        headers.Should().BeEquivalentTo("Name", "Age", "City");
    }

    [Fact]
    public async Task ExtractHeader_WithSemicolonDelimiter_ShouldReturnCorrectHeaders()
    {
        // Arrange
        using var stream = CreateMemoryStreamFromString(SemicolonDelimitedCsv);

        // Act
        var headers = await CsvParser.ExtractHeader(stream);

        // Assert
        headers.Should().BeEquivalentTo("Name", "Age", "City");
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