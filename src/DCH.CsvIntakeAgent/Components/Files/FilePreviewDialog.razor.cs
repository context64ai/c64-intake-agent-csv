using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DCH.CsvIntakeAgent.Components.Files;

// TODO Add tests! Due to wrong coverage package missing coverage on this class went unnoticed when it was initially created.
public partial class FilePreviewDialog : ComponentBase
{
    [Inject] private ILogger<FilePreviewDialog> Logger { get; set; }
    
    [Parameter] public CsvRecord CsvRecord { get; set; }
    
    private List<Dictionary<string, object>> _previewData = [];
    private bool _isLoading = true;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        if (CsvRecord != null)
        {
            await PreviewData();
        }
    }

    private async Task PreviewData()
    {
        _isLoading = true;
        _errorMessage = string.Empty;
        StateHasChanged();

        try
        {
            _previewData = await Helpers.CsvParser.ConvertCsvRecordToPreviewData(CsvRecord);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading preview data for file record {FileId}", CsvRecord.Id);
            _errorMessage = "Failed to load preview data. The file record might be corrupted.";
            _previewData = [];
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}