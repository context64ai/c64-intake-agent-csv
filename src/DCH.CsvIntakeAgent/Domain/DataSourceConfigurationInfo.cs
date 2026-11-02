using System;
using System.Collections.Generic;
using System.Linq;
using DCH.IntakeAgentCore.Abstractions.DataProviders;

namespace DCH.CsvIntakeAgent.Domain;

public class DataSourceConfigurationInfo : IPersistedEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string UpdatedBy { get; set; }

    public Guid CsvRecordId { get; init; }

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }

    public List<SourceEntityInfo> Entities { get; set; } = [];

    public int? ImportJobBatchSize { get; set; }

    public DataSourceConfiguration ToDataSourceConfiguration()
    {
        return new DataSourceConfiguration
        {
            Name = Name,
            Slug = Slug,
            Entities = Entities.Select(e =>
            {
                return new SourceEntity
                {
                    Title = e.Name,
                    Properties = e.Properties.Select(p => new Property
                    {
                        Title = p.Name
                    }).ToList()
                };
            }).ToList(),
            ImportJobBatchSize = ImportJobBatchSize,
        };
    }

    public void UpdateFrom(DataSourceConfigurationInfo cfgToSave)
    {
        Name = cfgToSave.Name;
        Slug = cfgToSave.Slug;
        Entities = cfgToSave.Entities;
        ImportJobBatchSize = cfgToSave.ImportJobBatchSize;
        IsEnabled = cfgToSave.IsEnabled;
    }
}

// ReSharper disable PropertyCanBeMadeInitOnly.Global
public class SourceEntityInfo
{
    public string Name { get; set; } = string.Empty;

    public List<SourcePropertyInfo> Properties { get; set; } = [];
}

public class SourcePropertyInfo
{
    public string Name { get; init; } = string.Empty;
}