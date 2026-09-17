using CDPIUI.Views.ConfigImportUtil;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using WinUI3Localizer;

namespace CDPIUI.ViewModels;

public sealed class ConfigImportMissingFileViewModel : INotifyPropertyChanged
{
    private readonly ConfigImportPresetViewModel owner;
    private readonly ILocalizer localizer = Localizer.Get();
    private bool isResolved;
    private string? replacementPath;
    private string resolutionText;

    public ConfigImportMissingFileViewModel(
        ConfigImportPresetViewModel owner,
        string missingPath,
        string? suggestedPath,
        bool suggestEmptyFile)
    {
        this.owner = owner;
        MissingPath = Path.GetFullPath(missingPath);
        SuggestedPath = string.IsNullOrWhiteSpace(suggestedPath) ? null : Path.GetFullPath(suggestedPath);
        SuggestEmptyFile = suggestEmptyFile;
        resolutionText = localizer.GetLocalizedString("ConfigImportMissingFileUnresolved");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ConfigName => owner.Name;

    public string MissingPath { get; }

    public string? SuggestedPath { get; }

    public bool SuggestEmptyFile { get; }

    public string SuggestionText => SuggestEmptyFile
        ? localizer.GetLocalizedString("ConfigImportMissingFileEmptySuggestion")
        : SuggestedPath ?? string.Empty;

    public bool HasSuggestion => SuggestEmptyFile || !string.IsNullOrWhiteSpace(SuggestedPath);

    public bool IsResolved => isResolved;

    public string ResolutionText => resolutionText;

    public void UseSuggestion()
    {
        if (SuggestEmptyFile)
        {
            UseEmptyFile();
            return;
        }

        if (!string.IsNullOrWhiteSpace(SuggestedPath) && File.Exists(SuggestedPath))
            UseReplacement(SuggestedPath);
    }

    public void UseReplacement(string path)
    {
        replacementPath = Path.GetFullPath(path);
        resolutionText = string.Format(
            localizer.GetLocalizedString("ConfigImportMissingFileReplacementSelected"),
            replacementPath);
        CompleteResolution();
    }

    public void UseEmptyFile()
    {
        replacementPath = null;
        resolutionText = localizer.GetLocalizedString("ConfigImportMissingFileEmptySelected");
        CompleteResolution();
    }

    private void CompleteResolution()
    {
        isResolved = true;
        owner.SetMissingFileResolution(MissingPath, replacementPath);
        Notify(nameof(IsResolved));
        Notify(nameof(ResolutionText));
    }

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
