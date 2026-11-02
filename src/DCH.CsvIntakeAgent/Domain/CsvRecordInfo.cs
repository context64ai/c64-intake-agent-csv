using System.Diagnostics.CodeAnalysis;

namespace DCH.CsvIntakeAgent.Domain;

[ExcludeFromCodeCoverage]
public class CsvRecordInfo
{
    public string FileName { get; set; }
    public string ConfigName { get; set; }
    public string ConfigSlug { get; set; }
    public int BatchSize { get; set; }
    public bool IsEnabled { get; set; }
}