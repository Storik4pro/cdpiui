using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.AddOns.ConfigImport;

public enum ConfigImportIssueSeverity
{
    Warning,
    Error,
}

public sealed record ConfigImportIssue(
    ConfigImportIssueSeverity Severity,
    string Code,
    string Message,
    string? SourcePath = null,
    int? LineNumber = null);

public sealed record ConfigImportGeneratedFile(
    string RelativePath,
    string Content);

/// <summary>
/// Describes the user's decision for an unavailable referenced file.
/// A null replacement means that an empty file should be used.
/// </summary>
public sealed record ConfigImportMissingFileResolution(
    string MissingPath,
    string? ReplacementPath);

public sealed record ConfigImportTarget(
    string ComponentId,
    string DisplayName,
    string Executable,
    string? Version,
    string? Directory = null);

public sealed class ConfigImportResult
{
    // The import view owns this temporary extraction until save, reset, or close.
    public ConfigShare.ConfigSharePackage? SharedPackage { get; init; }

    public ConfigItem? Config { get; init; }

    public required ConfigImportTarget Target { get; init; }

    public required string SourcePath { get; init; }

    public required IReadOnlyList<ConfigImportIssue> Issues { get; init; }

    public required IReadOnlyList<string> SourceFiles { get; init; }

    public required IReadOnlyList<string> ReferencedFiles { get; init; }

    public required IReadOnlyList<string> MissingReferencedFiles { get; init; }

    public required IReadOnlyList<ConfigImportMissingFileResolution> MissingFileResolutions { get; init; }

    public required IReadOnlyList<ConfigImportGeneratedFile> GeneratedFiles { get; init; }

    public bool IsSuccessful =>
        Config != null && Issues.All(issue => issue.Severity != ConfigImportIssueSeverity.Error);
}

public sealed class ConfigImportInstallResult
{
    public string? ConfigFileName { get; init; }

    public string? PackId { get; init; }

    public string? ResourceDirectory { get; init; }

    public string? ErrorCode { get; init; }

    public bool IsSuccessful => string.IsNullOrEmpty(ErrorCode) && !string.IsNullOrEmpty(ConfigFileName);
}
