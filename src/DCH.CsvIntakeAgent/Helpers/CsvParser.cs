using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using DCH.CsvIntakeAgent.Domain;
using Microsoft.AspNetCore.Components.Forms;

namespace DCH.CsvIntakeAgent.Helpers;

public static class CsvParser
{
    private static char DetectDelimiter(MemoryStream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        
        // Read first few lines to detect delimiter
        const int linesToCheck = 3;
        var lines = new List<string>();
        
        for (var i = 0; i < linesToCheck && !reader.EndOfStream; i++)
        {
            var line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }
        stream.Position = 0;
        
        if (lines.Count == 0) return Constants.DefaultDelimiter;
        
        // Count occurrences of potential delimiters
        var commaCount = lines.Sum(line => line.Count(c => c == ','));
        var semicolonCount = lines.Sum(line => line.Count(c => c == ';'));
        
        // Return the delimiter that appears more frequently
        return semicolonCount > commaCount 
            ? ';' 
            : ',';
    }
    
    private static CsvConfiguration GetCsvConfiguration(MemoryStream stream)
    {
        var delimiter = DetectDelimiter(stream);
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter.ToString()
        };
    }
    
    public static async Task<MemoryStream> ToMemoryStream(IBrowserFile file)
    {
        await using var stream = file.OpenReadStream(maxAllowedSize: Constants.MaxAllowedCsvFileSize);
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        
        return memoryStream;
    }

    public static async Task Validate(MemoryStream fileStream)
    {
        using var reader = new StreamReader(fileStream);
        using var csvReader = new CsvReader(reader, GetCsvConfiguration(fileStream));
        
        // Read headers to validate CSV structure
        await csvReader.ReadAsync();
        csvReader.ReadHeader();
        var headers = csvReader.HeaderRecord;

        if (headers == null || headers.Length == 0)
        {
            throw new InvalidOperationException("CSV file must contain a Header");
        }

        // Count rows for validation
        var rowCount = 0;
        while (await csvReader.ReadAsync())
        {
            rowCount++;
        }

        if (rowCount == 0)
        {
            throw new InvalidOperationException("CSV file contains no data rows");
        }
    }
    
    public static List<(int index, string columnName)> FilterHeader(string[] header, List<string> propertiesToRetrieve)
    {
        // Filter headers to only include requested properties
        var columnIndices = new List<(int index, string columnName)>();
        if (propertiesToRetrieve != null)
        {
            foreach (var property in propertiesToRetrieve)
            {
                var index = Array.IndexOf(header, property);
                if (index >= 0)
                {
                    columnIndices.Add((index, property));
                }
            }
        }
        else
        {
            // include all columns
            columnIndices.AddRange(header.Select((colName, index) => (index, colName)));
        }
        
        return columnIndices;
    }

    public static async IAsyncEnumerable<List<Dictionary<string, string>>> GetCsvDataBatchesAsync(CsvRecord csvRecord, 
        List<(int index, string columnName)> columnIndices, int? limit, int batchSize)
    {
        using var memoryStream = new MemoryStream(csvRecord.Data);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        using var csvReader = new CsvReader(reader, GetCsvConfiguration(memoryStream));
        
        // Skip the header line
        await csvReader.ReadAsync();
        csvReader.ReadHeader();
        
        var batch = new List<Dictionary<string, string>>();

        // Read data rows
        var totalFetched = 0;
        while (await csvReader.ReadAsync())
        {
            if (totalFetched >= limit)
            {
                break;
            }

            var row = new Dictionary<string, string>();
            foreach (var (index, columnName) in columnIndices)
            {
                var value = csvReader.GetField(index);
                row[columnName] = value ?? string.Empty;
            }
            
            batch.Add(row);
            totalFetched++;

            // Yield batch when it reaches the desired size
            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = [];
            }
        }

        // Yield remaining records in the last batch
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }
    
    public static async Task<List<Dictionary<string, object>>> ConvertCsvRecordToPreviewData(CsvRecord csvRecord)
    {
        if (csvRecord.Data == null || csvRecord.Data.Length == 0 || csvRecord.Header == null)
        {
            return [];
        }

        var previewData = new List<Dictionary<string, object>>();
        var header = csvRecord.Header;

        using var memoryStream = new MemoryStream(csvRecord.Data);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        using var csv = new CsvReader(reader, GetCsvConfiguration(memoryStream));

        // Skip the header line since we already have headers from CsvRecord
        await csv.ReadAsync();
        csv.ReadHeader();

        var rowCount = 0;
        while (await csv.ReadAsync() && rowCount < Constants.MaxPreviewRows)
        {
            var row = new Dictionary<string, object>();
            
            for (var i = 0; i < header.Length && i < csv.Parser.Count; i++)
            {
                var value = csv.GetField(i);
                row[header[i]] = value ?? string.Empty;
            }
            
            previewData.Add(row);
            rowCount++;
        }

        return previewData;
    }
    
    public static async Task<(byte[] Content, int RowCount, string[] Header, char Delimiter)> ExtractCsvDataAsync(IBrowserFile file)
    {
        // Get file content as a byte array
        await using var stream = file.OpenReadStream(maxAllowedSize: Constants.MaxAllowedCsvFileSize); // 100MB limit
        await using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var content = memoryStream.ToArray();

        // Reset stream for CSV parsing
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        using var csv = new CsvReader(reader, GetCsvConfiguration(memoryStream));

        // Read headers
        await csv.ReadAsync();
        csv.ReadHeader();
        var header = csv.HeaderRecord ?? throw new InvalidOperationException("CSV file must contain a header");

        // Count rows
        var rowCount = 0;
        while (await csv.ReadAsync())
        {
            rowCount++;
        }
        
        var delimiter = DetectDelimiter(memoryStream);

        return (content, rowCount, header, delimiter);
    }
    
    public static async Task<IEnumerable<string>> ExtractHeader(IBrowserFile file)
    {
        // Get file content as a byte array
        await using var stream = file.OpenReadStream(maxAllowedSize: Constants.MaxAllowedCsvFileSize); // 100MB limit
        await using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);

        var header = await ExtractHeader(memoryStream);
        return header;
    }
    
    public static async Task<IEnumerable<string>> ExtractHeader(MemoryStream memoryStream)
    {
        // Reset stream for CSV parsing
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        using var csv = new CsvReader(reader, GetCsvConfiguration(memoryStream));

        // Read headers
        await csv.ReadAsync();
        csv.ReadHeader();
        var header = csv.HeaderRecord ?? throw new InvalidOperationException("CSV file must contain a header");

        return header;
    }
}