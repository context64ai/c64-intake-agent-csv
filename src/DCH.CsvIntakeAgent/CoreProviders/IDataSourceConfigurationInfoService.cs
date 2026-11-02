using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.Domain;

namespace DCH.CsvIntakeAgent.CoreProviders;

public interface IDataSourceConfigurationInfoService
{
    Task<List<DataSourceConfigurationInfo>> GetAll(Func<DataSourceConfigurationInfo, bool> filter = null);
    Task<DataSourceConfigurationInfo> AddOrUpdate(DataSourceConfigurationInfo cfg);
    Task Delete(Guid cfgId);
}