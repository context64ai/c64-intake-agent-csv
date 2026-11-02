using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using DCH.CsvIntakeAgent.CoreProviders;
using DCH.CsvIntakeAgent.Domain;
using DCH.IntakeAgentCore.Blazor;
using DCH.IntakeAgentCore.Util;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;

namespace DCH.CsvIntakeAgent.Components.Files;

// TODO Add tests! Due to wrong coverage package missing coverage on this class went unnoticed when it was initially created.
[ExcludeFromCodeCoverage]
public partial class EditDialog : ComponentBase
{
    [Inject] private IDataSourceConfigurationInfoService ConfigService { get; set; }
    [Inject] private DialogService DialogService { get; set; }
    [Inject] private NotificationService NotificationService { get; set; }
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; }

    [Parameter] public CsvRecord CsvRecord { get; set; }
    private CsvRecordInfo _recordInfo = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadCsvRecordInfo();
    }

    private async Task LoadCsvRecordInfo()
    {
        if (CsvRecord != null)
        {
            var csvConfigs = await ConfigService.GetAll(c => c.CsvRecordId == CsvRecord.Id);
            if (csvConfigs == null || csvConfigs.Count == 0)
            {
                return;
            }

            var csvConfig = csvConfigs.First();
            _recordInfo = new CsvRecordInfo
            {
                FileName = CsvRecord.Name,
                ConfigName = csvConfig.Name,
                ConfigSlug = csvConfig.Slug,
                BatchSize = csvConfig.ImportJobBatchSize ?? Constants.DefaultBatchSize,
                IsEnabled = csvConfig.IsEnabled
            };
        }
    }

    private async Task SaveChanges()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();

        try
        {
            var csvConfigs = await ConfigService.GetAll(c => c.CsvRecordId == CsvRecord.Id);
            var csvConfig = csvConfigs.FirstOrDefault();
            if (csvConfig == null)
            {
                NotificationService.NotifyError("Failed to save changes",
                    $"No DataSourceConfiguration found for CsvRecord Id {CsvRecord.Id}");
                return;
            }

            csvConfig.Name = _recordInfo.ConfigName;
            csvConfig.UpdatedBy = ClaimUtil.GetPreferredUsername(authState.User);
            csvConfig.Slug = _recordInfo.ConfigSlug;
            csvConfig.ImportJobBatchSize = _recordInfo.BatchSize;
            csvConfig.IsEnabled = _recordInfo.IsEnabled;

            var result = await ConfigService.AddOrUpdate(csvConfig);
            if (result == null)
            {
                NotificationService.NotifyError("Failed to save changes",
                    $"Name '{csvConfig.Name}' or slug '{csvConfig.Slug}' may already exist or slug format is invalid.");
                return;
            }

            DialogService.Close(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Success",
                Detail = "Changes saved successfully"
            });
        }
        catch (Exception ex)
        {
            NotificationService.NotifyException(ex);
        }
    }

    private void Cancel()
    {
        DialogService.Close();
    }
}
