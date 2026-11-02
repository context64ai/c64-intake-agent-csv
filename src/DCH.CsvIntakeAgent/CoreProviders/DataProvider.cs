using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DCH.IntakeAgentCore.Abstractions.DataProviders;

namespace DCH.CsvIntakeAgent.CoreProviders;

public class DataProvider(
    ICsvRecordService csvRecordService,
    IDataSourceConfigurationInfoService configService) : IDataProvider
{
    public async Task<DataSourceConfiguration> GetConfiguration(string nameOrSlug)
    {
        var allConfigs = await configService.GetAll(c => c.Name == nameOrSlug || c.Slug == nameOrSlug);
        var cfg = allConfigs.FirstOrDefault();
        return cfg?.ToDataSourceConfiguration();
    }

    public async Task<IEnumerable<DataSourceConfiguration>> GetConfigurations()
    {
        var allConfigs = await configService.GetAll();
        var active = allConfigs.Where(c => c.IsEnabled).ToList();
        return active.Select(c => c.ToDataSourceConfiguration()).ToList();
    }

    public async IAsyncEnumerable<SourceDataBatch> GetData(DataFetchParameters parameters)
    {
        var configs = await configService
            .GetAll(c => c.Name == parameters.DsConfigNameOrSlug || c.Slug == parameters.DsConfigNameOrSlug);

        var config = configs.FirstOrDefault();
        if (config == null)
        {
            throw new Exception($"Configuration {parameters.DsConfigNameOrSlug} not found");
        }

        var entity = config.Entities.SingleOrDefault(e => e.Name == parameters.Entity);
        if (entity == null)
        {
            throw new Exception($"Entity {parameters.Entity} not found");
        }

        var data = csvRecordService.GetCsvRecordsBatchedAsync(config.CsvRecordId, entity.Name,
            parameters.Properties, parameters.Limit, parameters.BatchSize);
        await foreach (var batch in data)
        {
            yield return batch;
        }
    }
}