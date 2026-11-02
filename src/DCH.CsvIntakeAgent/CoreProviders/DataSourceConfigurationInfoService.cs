using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.Domain;
using DCH.IntakeAgentCore.Abstractions.DataProviders;
using DCH.IntakeAgentCore.Util;
using Microsoft.Extensions.Logging;

namespace DCH.CsvIntakeAgent.CoreProviders;

public class DataSourceConfigurationInfoService(
    IRepository<DataSourceConfigurationInfo> repository,
    ILogger<DataSourceConfigurationInfoService> logger) : IDataSourceConfigurationInfoService
{
    public async Task<List<DataSourceConfigurationInfo>> GetAll(Func<DataSourceConfigurationInfo, bool> filter = null)
    {
        return await repository.GetAll(filter);
    }

    public async Task<DataSourceConfigurationInfo> AddOrUpdate(DataSourceConfigurationInfo cfg)
    {
        var configsWithSameName = await GetAll(c => c.Name == cfg.Name
                                                   && c.Id != cfg.Id);
        if (configsWithSameName.Count != 0)
        {
            logger.LogError("Configuration with same name already exists. Id {config}.", configsWithSameName.FirstOrDefault()?.Id);
            return null;
        }

        if (!SlugUtil.IsValidSlug(cfg.Slug))
        {
            logger.LogError("Invalid slug '{Slug}'.", cfg.Slug);
            return null;
        }

        var configsWithSameSlug = await GetAll(c => c.Slug == cfg.Slug
                                                   && c.Id != cfg.Id);
        if (configsWithSameSlug.Count != 0)
        {
            logger.LogError("Configuration with same slug already exists. Id {config}", configsWithSameSlug.FirstOrDefault()?.Id);
            return null;
        }

        var updated = await repository.AddOrUpdate(
            c => c.Id == cfg.Id,
            () =>
            {
                if (cfg.Id == Guid.Empty)
                {
                    cfg.Id = Guid.NewGuid();
                }
                return cfg;
            },
            c => c.UpdateFrom(cfg));
        return updated;
    }

    public async Task Delete(Guid cfgId)
    {
        await repository.Delete(cfgId);
    }
}