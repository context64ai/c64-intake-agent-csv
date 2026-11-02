using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.Domain;
using DCH.CsvIntakeAgent.Exceptions;
using DCH.IntakeAgentCore.Abstractions.DataProviders;
using DCH.IntakeAgentCore.Persistence;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DCH.CsvIntakeAgent.CoreProviders;

public class CsvRecordService : ICsvRecordService
{
    private readonly ILogger<CsvRecordService> _logger;
    private readonly string _connectionString;

    public CsvRecordService(IConfiguration configuration, ILogger<CsvRecordService> logger)
    {
        _logger = logger;
        _connectionString = $"Host={configuration["POSTGRES_HOST"]};Port={configuration["POSTGRES_PORT"]};" +
                            $"Username={configuration["POSTGRES_USER"]};Password={configuration["POSTGRES_PASSWORD"]};" +
                            $"Database={configuration["POSTGRES_DATABASE"]}";
        
        PostgreSqlDatabaseInitializer.EnsureDatabaseExists(_connectionString, logger: _logger);
        EnsureTableExistsAsync().GetAwaiter().GetResult();
    }

    public async Task EnsureTableExistsAsync()
    {
        const string createTableSql = $"""
                                      CREATE TABLE IF NOT EXISTS "{Constants.CsvRecordTableName}" (
                                          "Id" UUID PRIMARY KEY,
                                          "Name" TEXT NOT NULL,
                                          "Data" BYTEA,
                                          "Size" BIGINT NOT NULL,
                                          "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
                                          "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
                                          "RowCount" INTEGER NOT NULL,
                                          "Header" TEXT[] NOT NULL,
                                          "Delimiter" TEXT NOT NULL
                                      )
                                      """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new NpgsqlCommand(createTableSql, connection);
        
        try
        {
            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("CsvRecord table created or already exists");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating CsvRecord table");
            throw;
        }
    }

    public async IAsyncEnumerable<SourceDataBatch> GetCsvRecordsBatchedAsync(
         Guid csvRecordId,
         string entityName,
         IEnumerable<string> propertiesToRetrieve = null,
         int? limit = null, 
         int? batchSize = null) 
    {
         _logger.LogInformation("Get CSV data in batches for entity: {entityName}, Limit: {limit}, BatchSize: {batchSize}", 
             entityName, limit, batchSize);
         
         var csvRecord = await GetCsvRecordAsync(csvRecordId);
         if (csvRecord == null)
         {
             _logger.LogWarning("No CSV record found with name: {entityName}", entityName);
             yield break;
         }

         if (csvRecord.Data == null || csvRecord.Data.Length == 0)
         {
             _logger.LogWarning("CSV record {entityName} has no data", entityName);
             yield break;
         }

         var effectiveBatchSize = batchSize ?? Constants.DefaultBatchSize;
         var propertiesToRetrieveList = propertiesToRetrieve?.ToList() ?? csvRecord.Header?.ToList();
         
         // Get Header
         using var memoryStream = new MemoryStream(csvRecord.Data);
         var header = await Helpers.CsvParser.ExtractHeader(memoryStream);
         if (header == null)
         {
             _logger.LogWarning("CSV record {entityName} has no header", entityName);
             yield break;
         }
         
         // Filter header based on propertiesToRetrieve
         var columnIndices = Helpers.CsvParser.FilterHeader(header.ToArray(), propertiesToRetrieveList);
         
         var data = Helpers.CsvParser.GetCsvDataBatchesAsync(csvRecord, columnIndices, limit, effectiveBatchSize);
         await foreach (var batch in data)
         {
             yield return new SourceDataBatch { Data = batch };
         } 
    }
    
    public async Task<(Guid Id, string[] Header)> SaveCsvRecordAsync(IBrowserFile file)
    {
        _logger.LogInformation("Save CSV file {file} to Database", file.Name);
        var csvRecord = await ToCsvRecord(file);
        await WriteCsvRecord(csvRecord);
        return (csvRecord.Id, csvRecord.Header);
    }

    public async Task DeleteCsvRecordAsync(Guid fileId)
    {
        const string sql = $"DELETE FROM \"{Constants.CsvRecordTableName}\" WHERE \"Id\" = @id";
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
    
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", fileId);
        
        await command.ExecuteNonQueryAsync();
    }

    private async Task<CsvRecord> GetCsvRecordAsync(Guid fileId)
    {
        var query = $"SELECT * FROM \"{Constants.CsvRecordTableName}\" WHERE \"Id\" = '{fileId}'";
        
        var dataTable = await ExecuteReadQuery(_connectionString, query);
        var csvRecord = dataTable.Rows.Count > 0 
            ? ToCsvRecord(dataTable.Rows[0]) 
            : null;
        
        return csvRecord ?? throw new CsvIntakeAgentException($"No Record of CSV File with Id {fileId} found.");
    }

    public async Task<List<CsvRecord>> GetAllCsvRecordsAsync()
    {
        const string command = $"SELECT * FROM \"{Constants.CsvRecordTableName}\"";
        
        var dataTable = await ExecuteReadQuery(_connectionString, command);
        var csvRecords = dataTable.Rows.Cast<DataRow>().Select(ToCsvRecord).ToList();
        
        return csvRecords;
    }

    private async Task WriteCsvRecord(CsvRecord csvRecord)
    {
        const string sql = $"""
                           INSERT INTO "{Constants.CsvRecordTableName}" ("Id", "Name", "Data", "Size", "CreatedAt", "UpdatedAt", "RowCount", "Header", "Delimiter")
                           VALUES (@id, @name, @data, @size, @createdAt, @updatedAt, @rowCount, @header, @delimiter)
                           """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
    
        await using var command = new NpgsqlCommand(sql, connection);
    
        // Add parameters
        command.Parameters.AddWithValue("@id", csvRecord.Id);
        command.Parameters.AddWithValue("@name", csvRecord.Name);
        command.Parameters.AddWithValue("@data", csvRecord.Data ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@size", csvRecord.Size);
        command.Parameters.AddWithValue("@createdAt", csvRecord.CreatedAt);
        command.Parameters.AddWithValue("@updatedAt", csvRecord.UpdatedAt);
        command.Parameters.AddWithValue("@rowCount", csvRecord.RowCount);
        command.Parameters.AddWithValue("@header", csvRecord.Header);
        command.Parameters.AddWithValue("@delimiter", csvRecord.Delimiter);

        try
        {
            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                throw new CsvIntakeAgentException($"Failed to insert CsvRecord with Id {csvRecord.Id}");
            }
        
            _logger.LogInformation("Successfully saved CsvRecord {FileId} with name {FileName}", csvRecord.Id, csvRecord.Name);
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "PostgreSQL error while saving CsvRecord {FileId}: {ErrorMessage}", 
                csvRecord.Id, ex.MessageText);
            throw new CsvIntakeAgentException($"Database error while saving file: {ex.MessageText}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while saving CsvRecord {FileId}", csvRecord.Id);
            throw;
        }
    }
    
    private async Task<DataTable> ExecuteReadQuery(string connectionString, string query)
    {
        var dataTable = new DataTable();
        await using var connection = new NpgsqlConnection(connectionString);
        await using var command = new NpgsqlCommand(query, connection);
        using var adapter = new NpgsqlDataAdapter(command);

        await connection.OpenAsync();
        
        try
        {
            adapter.Fill(dataTable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query {query}", query);
            throw;
        }
        
        return dataTable;
    }
    
    private static CsvRecord ToCsvRecord(DataRow row)
    {
        return new CsvRecord
        {
            Id = (Guid)row["Id"],
            Name = (string)row["Name"],
            Data = row["Data"] as byte[],
            Size = (long)row["Size"],
            CreatedAt = (DateTime)row["CreatedAt"],
            UpdatedAt = (DateTime)row["UpdatedAt"],
            RowCount = (int)row["RowCount"],
            Header = (string[])row["Header"],
            Delimiter = ((string)row["Delimiter"]).FirstOrDefault(Constants.DefaultDelimiter)
        };
    }
    
    private static async Task<CsvRecord> ToCsvRecord(IBrowserFile browserFile) 
    {
        var csvData = await Helpers.CsvParser.ExtractCsvDataAsync(browserFile);
         
         return new CsvRecord
         {
             Id = Guid.NewGuid(),
             Name = browserFile.Name,
             Data = csvData.Content,
             Size = browserFile.Size,
             CreatedAt = DateTime.UtcNow,
             UpdatedAt = DateTime.UtcNow,
             RowCount = csvData.RowCount,
             Header = csvData.Header,
             Delimiter = csvData.Delimiter
         };
    }
}