using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.CoreProviders;
using DCH.CsvIntakeAgent.Domain;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace DCH.CsvIntakeAgent.Components.Files;

// ReSharper disable once ClassNeverInstantiated.Global
// TODO Add tests! Due to wrong coverage package missing coverage on this class went unnoticed when it was initially created.
public partial class Files : ComponentBase
{
    [Inject]
    private ICsvRecordService CsvRecordService { get; set; }

    [Inject]
    private IDataSourceConfigurationInfoService ConfigService { get; set; }

    private RadzenDataGrid<CsvRecord> _filesGrid;
    private List<CsvRecord> _uploadedFiles = [];
    private List<DataSourceConfigurationInfo> _configs = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadFiles();
    }

    private async Task OpenUploadDialog()
    {
        var result = await DialogService.OpenAsync<UploadDialog>("Upload CSV Files",
            new Dictionary<string, object>(),
            new DialogOptions
            {
                Width = "1200px",
                Height = "700px",
                Resizable = true,
                Draggable = true
            });

        if (result is true)
        {
            await RefreshFiles();
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Success",
                Detail = "Files uploaded successfully"
            });
        }
    }

    private async Task RefreshFiles()
    {
        await LoadFiles();
        if (_filesGrid != null)
        {
            await _filesGrid.Reload();
        }

        StateHasChanged();
    }

    private async Task LoadFiles()
    {
        _uploadedFiles = await CsvRecordService.GetAllCsvRecordsAsync();
        _configs = await ConfigService.GetAll();
    }

    private async Task PreviewFile(CsvRecord csvRecord)
    {
        await DialogService.OpenAsync<FilePreviewDialog>($"Preview: {csvRecord.Name}",
            new Dictionary<string, object> { { "CsvRecord", csvRecord } },
            new DialogOptions
            {
                Width = "90%",
                Height = "850px",
                Resizable = true,
                Draggable = true
            });
    }

    private async Task EditCsvRecord(CsvRecord csv)
    {
        var result = await DialogService.OpenAsync<EditDialog>("Edit", new Dictionary<string, object> { { "CsvRecord", csv } });
        if (result is NotificationMessage message)
        {
            NotificationService.Notify(message);
        }

        await RefreshFiles();
    }

    private async Task DeleteCsvRecord(CsvRecord csv)
    {
        var confirm = await DialogService.Confirm(
            $"Are you sure you want to delete '{csv.Name}'?",
            "Delete File",
            new ConfirmOptions { OkButtonText = "Yes", CancelButtonText = "No" });

        if (confirm == true)
        {
            _uploadedFiles.Remove(csv);
            await CsvRecordService.DeleteCsvRecordAsync(csv.Id);
            var configs = await ConfigService.GetAll(filter => filter.CsvRecordId == csv.Id);
            await ConfigService.Delete(configs.FirstOrDefault()?.Id ?? Guid.Empty);
            await RefreshFiles();

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Deleted",
                Detail = $"File '{csv.Name}' has been deleted",
                Duration = 3000
            });
        }
    }
}