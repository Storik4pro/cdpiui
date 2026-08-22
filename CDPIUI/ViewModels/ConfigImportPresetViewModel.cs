using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using WinUI3Localizer;

namespace CDPIUI.Views.ConfigImportUtil;

public sealed class ConfigImportPresetViewModel : INotifyPropertyChanged
{
    private readonly ILocalizer localizer = Localizer.Get();
    private readonly bool targetWasDetected;
    private string name;
    private DatabaseStoreItem? selectedComponent;
    private bool isSaving;
    private bool isSaved;
    private bool isTesting;
    private string? operationError;
    private readonly Dictionary<string, ConfigImportMissingFileResolution> missingFileResolutions =
        new(StringComparer.OrdinalIgnoreCase);

    public ConfigImportPresetViewModel(
        ConfigImportResult result,
        ObservableCollection<DatabaseStoreItem> components,
        DatabaseStoreItem? initialComponent,
        bool targetWasDetected)
    {
        Result = result;
        Components = components;
        this.targetWasDetected = targetWasDetected;
        name = Path.GetFileNameWithoutExtension(result.SourcePath);
        selectedComponent = initialComponent;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ConfigImportResult Result { get; }

    public ObservableCollection<DatabaseStoreItem> Components { get; }

    public string SourcePath => Result.SourcePath;

    public string FileName => Path.GetFileName(Result.SourcePath);

    public IReadOnlyCollection<ConfigImportMissingFileResolution> MissingFileResolutions =>
        missingFileResolutions.Values;

    public string Name
    {
        get => name;
        set
        {
            if (name == value)
                return;
            name = value;
            isSaved = false;
            Notify();
            NotifyActionState();
        }
    }

    public DatabaseStoreItem? SelectedComponent
    {
        get => selectedComponent;
        set
        {
            if (ReferenceEquals(selectedComponent, value))
                return;
            selectedComponent = value;
            isSaved = false;
            Notify();
            Notify(nameof(IsUnsuitable));
            Notify(nameof(SuitabilityMessage));
            NotifyActionState();
        }
    }

    public bool IsUnsuitable =>
        Result.IsSuccessful &&
        targetWasDetected &&
        selectedComponent != null &&
        !ExecutablesEqual(Result.Target.Executable, selectedComponent.Executable);

    public string SuitabilityMessage => IsUnsuitable
        ? localizer.GetLocalizedString("ConfigImportComponentMismatchMessage")
        : string.Empty;

    public string? OperationError => operationError;

    public bool HasDetails => Result.Issues.Count > 0 || !string.IsNullOrWhiteSpace(operationError);

    public Visibility DetailsVisibility => HasDetails ? Visibility.Visible : Visibility.Collapsed;

    public string StatusText => Result.IsSuccessful && string.IsNullOrWhiteSpace(operationError)
        ? Result.Issues.Count > 0
            ? localizer.GetLocalizedString("ConfigImportItemWarningStatus")
            : localizer.GetLocalizedString("ConfigImportItemSuccessStatus")
        : localizer.GetLocalizedString("ConfigImportItemFailedStatus");

    public string StatusGlyph => Result.IsSuccessful && string.IsNullOrWhiteSpace(operationError)
        ? Result.Issues.Count > 0 ? "\uE7BA" : "\uE930"
        : "\uEA39";

    public Brush StatusForeground => new SolidColorBrush(Result.IsSuccessful && string.IsNullOrWhiteSpace(operationError)
        ? Result.Issues.Count > 0 ? ColorHelper.FromArgb(255, 157, 93, 0) : ColorHelper.FromArgb(255, 16, 124, 16)
        : ColorHelper.FromArgb(255, 196, 43, 28));

    public Brush StatusBackground => new SolidColorBrush(Result.IsSuccessful && string.IsNullOrWhiteSpace(operationError)
        ? Result.Issues.Count > 0 ? ColorHelper.FromArgb(28, 255, 185, 0) : ColorHelper.FromArgb(28, 16, 124, 16)
        : ColorHelper.FromArgb(28, 196, 43, 28));

    public bool CanSave => Result.IsSuccessful &&
                           selectedComponent != null &&
                           !string.IsNullOrWhiteSpace(name) &&
                           !isSaving &&
                           !isSaved;

    public bool CanTest => Result.IsSuccessful && selectedComponent != null && !isSaving;

    public string SaveButtonText => isSaved
        ? localizer.GetLocalizedString("ConfigImportSavedButtonText")
        : localizer.GetLocalizedString("ConfigImportSaveButtonText");

    public string TestButtonText => isTesting
        ? localizer.GetLocalizedString("StopTest")
        : localizer.GetLocalizedString("ConfigImportTestButtonText");

    public string TestButtonGlyph => isTesting
        ? (SharedUtils.IsOsSupportedNewGlyph() ? "\uF8AE" : "\uE769")
        : "\uF5B0";

    public void SetSaving(bool value)
    {
        isSaving = value;
        NotifyActionState();
    }

    public void SetSaved()
    {
        isSaving = false;
        isSaved = true;
        operationError = null;
        NotifyStatus();
        NotifyActionState();
    }

    public void SetOperationError(string error)
    {
        isSaving = false;
        operationError = error;
        Notify(nameof(OperationError));
        NotifyStatus();
        NotifyActionState();
    }

    public void SetTesting(bool value)
    {
        isTesting = value;
        Notify(nameof(TestButtonText));
        Notify(nameof(TestButtonGlyph));
        Notify(nameof(CanTest));
    }

    public void SetMissingFileResolution(string missingPath, string? replacementPath)
    {
        string fullMissingPath = Path.GetFullPath(missingPath);
        missingFileResolutions[fullMissingPath] = new ConfigImportMissingFileResolution(
            fullMissingPath,
            string.IsNullOrWhiteSpace(replacementPath) ? null : Path.GetFullPath(replacementPath));
    }

    private void NotifyActionState()
    {
        Notify(nameof(CanSave));
        Notify(nameof(CanTest));
        Notify(nameof(SaveButtonText));
    }

    private void NotifyStatus()
    {
        Notify(nameof(HasDetails));
        Notify(nameof(DetailsVisibility));
        Notify(nameof(StatusText));
        Notify(nameof(StatusGlyph));
        Notify(nameof(StatusForeground));
        Notify(nameof(StatusBackground));
    }

    private static bool ExecutablesEqual(string? left, string? right) =>
        NormalizeExecutable(left).Equals(NormalizeExecutable(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeExecutable(string? executable)
    {
        string name = Path.GetFileName((executable ?? string.Empty).Trim().Trim('"'));
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
