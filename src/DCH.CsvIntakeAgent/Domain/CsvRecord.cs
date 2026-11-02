using System;

namespace DCH.CsvIntakeAgent.Domain;

public class CsvRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; }
    public byte[] Data { get; init; }
    public long Size { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    public int RowCount { get; init; }
    public string[] Header { get; init; }
    public char Delimiter { get; init; }
}