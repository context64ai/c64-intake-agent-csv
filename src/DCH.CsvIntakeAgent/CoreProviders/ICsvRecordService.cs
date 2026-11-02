using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.Domain;
using DCH.IntakeAgentCore.Abstractions.DataProviders;
using Microsoft.AspNetCore.Components.Forms;

namespace DCH.CsvIntakeAgent.CoreProviders;

public interface ICsvRecordService
{
    Task EnsureTableExistsAsync();
    IAsyncEnumerable<SourceDataBatch> GetCsvRecordsBatchedAsync(
        Guid csvRecordId,
        string entityName,
        IEnumerable<string> propertiesToRetrieve = null,
        int? limit = null,
        int? batchSize = null);
    Task<(Guid Id, string[] Header)> SaveCsvRecordAsync(IBrowserFile file);
    Task DeleteCsvRecordAsync(Guid fileId);
    Task<List<CsvRecord>> GetAllCsvRecordsAsync();
}