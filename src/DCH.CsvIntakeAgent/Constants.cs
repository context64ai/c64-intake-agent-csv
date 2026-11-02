namespace DCH.CsvIntakeAgent;

public static class Constants
{
    /// <summary>
    /// Maximum allowed file size for CSV uploads (100 MB)
    /// </summary>
    public const long MaxAllowedCsvFileSize = 100 * 1024 * 1024;

    /// <summary>
    /// Allowed file extensions for CSV upload
    /// </summary>
    public const string AllowedCsvExtension = ".csv";

    /// <summary>
    /// Maximum number of rows to preview in the UI
    /// </summary>
    public const int MaxPreviewRows = 10;
    
    /// <summary>
    /// The default batch size for import jobs.
    /// </summary>
    public const int DefaultBatchSize = 10000;
    
    /// <summary>
    /// Table name for CSV records.
    /// </summary>
    public const string CsvRecordTableName = "CsvRecord";
    
    /// <summary>
    /// Default delimiter for CSV files.
    /// </summary>
    public const char DefaultDelimiter = ','; 
}