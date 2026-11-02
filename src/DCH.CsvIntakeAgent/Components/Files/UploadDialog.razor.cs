using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using DCH.CsvIntakeAgent.CoreProviders;
using DCH.CsvIntakeAgent.Domain;
using DCH.IntakeAgentCore.Blazor;
using DCH.IntakeAgentCore.Util;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;
using CsvParser = DCH.CsvIntakeAgent.Helpers.CsvParser;

namespace DCH.CsvIntakeAgent.Components.Files;

[ExcludeFromCodeCoverage]
public class SelectedFile
{
    public IBrowserFile File { get; init; }
    public string ConfigName { get; set; }
    public string ConfigSlug { get; set; }
}

// ReSharper disable once ClassNeverInstantiated.Global
// TODO Add tests! Due to wrong coverage package missing coverage on this class went unnoticed when it was initially created.
public partial class UploadDialog : ComponentBase
{
    [Inject]
    private ICsvRecordService CsvRecordService { get; set; }

    [Inject]
    private IDataSourceConfigurationInfoService ConfigService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; }

    private List<SelectedFile> _selectedFiles = [];
    private bool _isUploading;
    private double _uploadProgress;
    private string _uploadStatusText = string.Empty;
    private List<string> _uploadErrors = [];

    private bool CanUpload => _selectedFiles?.Count > 0 && !_isUploading;

    private async Task OnFilesChanged(InputFileChangeEventArgs e)
    {
        await Task.CompletedTask;
        var files = e.GetMultipleFiles().ToList();
        _selectedFiles = files.Select(f => new SelectedFile
        {
            File = f,
            ConfigName = $"Config_{Path.GetFileNameWithoutExtension(f.Name)}",
            ConfigSlug = SlugUtil.ToSlug(Path.GetFileNameWithoutExtension(f.Name))
        }).ToList();

        _uploadErrors?.Clear();

        if (_selectedFiles == null || _selectedFiles.Count == 0)
        {
            StateHasChanged();
            return;
        }

        // Validate files
        var errors = new List<string>();

        foreach (var file in _selectedFiles)
        {
            if (file.File.Size > Constants.MaxAllowedCsvFileSize)
            {
                errors.Add($"{file.File.Name}: File size exceeds 100MB limit");
            }

            // Check file extension
            if (!file.File.Name.EndsWith(Constants.AllowedCsvExtension, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{file.File.Name}: Only CSV files are allowed");
            }

            StateHasChanged();
        }

        _uploadErrors = errors.Count != 0 ? errors : null;
    }

    private void RemoveFile(SelectedFile fileToRemove)
    {
        if (_selectedFiles != null)
        {
            var filesList = _selectedFiles.ToList();
            filesList.Remove(fileToRemove);
            _selectedFiles = filesList;
            StateHasChanged();
        }
    }

    private async Task UploadFiles()
    {
        if (_selectedFiles == null || _selectedFiles.Count == 0)
        {
            return;
        }

        _isUploading = true;
        _uploadProgress = 0;
        _uploadErrors?.Clear();
        var errors = new List<string>();

        try
        {
            var i = 0;
            foreach (var file in _selectedFiles)
            {
                var amountOfFileProgress = (double)(i + 1) / _selectedFiles.Count * 100;
                _uploadStatusText = $"Processing {file.File.Name}...";
                StateHasChanged();

                try
                {
                    // Validate name/slug before saving the CSV record to avoid orphan records
                    var existingByName = await ConfigService.GetAll(c => c.Name == file.ConfigName);
                    if (existingByName.Count > 0)
                    {
                        throw new InvalidOperationException($"Configuration with name '{file.ConfigName}' already exists.");
                    }

                    if (!SlugUtil.IsValidSlug(file.ConfigSlug))
                    {
                        throw new InvalidOperationException($"Invalid slug '{file.ConfigSlug}'. Slug must contain only lowercase letters, digits, and hyphens [a-z0-9-].");
                    }

                    var existingBySlug = await ConfigService.GetAll(c => c.Slug == file.ConfigSlug);
                    if (existingBySlug.Count > 0)
                    {
                        throw new InvalidOperationException($"Configuration with slug '{file.ConfigSlug}' already exists.");
                    }

                    var memoryStream = await CsvParser.ToMemoryStream(file.File);
                    ReportProgress(Math.Round(_uploadProgress + amountOfFileProgress * 0.3, 0));
                    await CsvParser.Validate(memoryStream);
                    ReportProgress(Math.Round(_uploadProgress + amountOfFileProgress * 0.2, 0));
                    // TODO: CsvRecord and DataSourceConfiguration are saved as separate operations.
                    // Best-effort cleanup on failure, but orphan records are possible on crash/pod restart.
                    // Consider atomic SaveWithConfiguration or periodic orphan cleanup.
                    var csvRecordInfo = await CsvRecordService.SaveCsvRecordAsync(file.File);
                    try
                    {
                        await CreateDataSourceConfiguration(file, csvRecordInfo);
                    }
                    catch
                    {
                        await CsvRecordService.DeleteCsvRecordAsync(csvRecordInfo.Id);
                        throw;
                    }
                    ReportProgress(Math.Round(_uploadProgress + amountOfFileProgress * 0.5, 0));
                }
                catch (BadDataException ex)
                {
                    errors.Add($"{file.File.Name}: {ex.Message}");
                    NotificationService.NotifyError("Bad data",
                        $"{file.File.Name}: Check the file and try again.");
                }
                catch (Exception ex)
                {
                    errors.Add($"{file.File.Name}: {ex.Message}");
                    NotificationService.NotifyError(file.File.Name, ex.Message);
                }

                _uploadProgress = Math.Round(amountOfFileProgress, 0);
                i++;
                StateHasChanged();
            }

            _uploadErrors = errors.Count != 0 ? errors : null;

            if (errors.Count == 0)
            {
                _uploadStatusText = "All files uploaded successfully!";
                DialogService.Close(true);
            }
            else
            {
                _uploadStatusText = "Upload completed with errors";
            }
        }
        catch (Exception ex)
        {
            _uploadErrors = [$"Upload failed: {ex.Message}"];
            _uploadStatusText = "Upload failed";
        }
        finally
        {
            _isUploading = false;
            StateHasChanged();
        }
    }

    private void ReportProgress(double progress)
    {
        _uploadProgress = progress;
        StateHasChanged();
    }

    private async Task<string> GetUsername()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var username = ClaimUtil.GetPreferredUsername(authState.User);
        return username;
    }

    private async Task CreateDataSourceConfiguration(SelectedFile file, (Guid Id, string[] Header) csvRecordInfo)
    {
        var username = await GetUsername();

        var result = await ConfigService.AddOrUpdate(new DataSourceConfigurationInfo
        {
            Id = Guid.NewGuid(),
            CreatedBy = username,
            UpdatedBy = username,
            CsvRecordId = csvRecordInfo.Id,
            Name = file.ConfigName,
            Slug = file.ConfigSlug,
            IsEnabled = true,
            ImportJobBatchSize = 10000,
            Entities =
            [
                new SourceEntityInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file.File.Name),
                    Properties = [..csvRecordInfo.Header.Select(c => new SourcePropertyInfo { Name = c })]
                }
            ]
        });

        if (result == null)
        {
            throw new InvalidOperationException(
                $"Failed to create configuration for '{file.File.Name}'. Name '{file.ConfigName}' or slug '{file.ConfigSlug}' may already exist.");
        }
    }

    private void Cancel()
    {
        DialogService.Close(false);
    }
}