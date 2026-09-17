using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.AddOns.ConfigImport;

public enum ZapretConfigGeneration
{
    Winws1,
    Winws2,
}

public enum ZapretConfigImportIssueSeverity
{
    Warning,
    Error,
}

public sealed record ZapretConfigImportIssue(
    ZapretConfigImportIssueSeverity Severity,
    string Code,
    string Message,
    string? SourcePath = null,
    int? LineNumber = null);

public sealed record ZapretConfigImportVariable(
    string Name,
    string Value,
    IReadOnlyList<string> Alternatives);

public sealed record ZapretConfigImportGeneratedFile(
    string RelativePath,
    string Content);

public sealed class ZapretConfigImportOptions
{
    public const string CurrentVersionPlaceholder = "%CURRENT%";

    /// <summary>
    /// Allows a caller to supply the installed component version without coupling
    /// the parser to application initialization. Returning null or an empty string
    /// makes the importer use <see cref="CurrentVersionPlaceholder"/>.
    /// </summary>
    public Func<ZapretConfigGeneration, string?>? CurrentVersionResolver { get; init; }

    /// <summary>
    /// Values that should be available while statically resolving a batch file.
    /// They are useful for variables populated by an external launcher.
    /// </summary>
    public IReadOnlyDictionary<string, string> PredefinedVariables { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Direct text configs without generation-specific options are treated as
    /// winws2 configs by default. Set this to null to require strict detection.
    /// </summary>
    public ZapretConfigGeneration? DefaultTextConfigGeneration { get; init; } =
        ZapretConfigGeneration.Winws2;

    public int MaxIncludeDepth { get; init; } = 8;
}

public sealed class ZapretConfigImportResult
{
    public ConfigItem? Config { get; init; }

    public ZapretConfigGeneration? Generation { get; init; }

    public required IReadOnlyList<ZapretConfigImportIssue> Issues { get; init; }

    public required IReadOnlyList<string> SourceFiles { get; init; }

    /// <summary>
    /// Existing BIN/TXT resources referenced by the resulting startup string.
    /// These files can be copied into the imported config pack.
    /// </summary>
    public required IReadOnlyList<string> ReferencedFiles { get; init; }

    /// <summary>
    /// Referenced resources that were not present next to the source Config.
    /// Import and test callers can materialize empty placeholders for them.
    /// </summary>
    public required IReadOnlyList<string> MissingReferencedFiles { get; init; }

    /// <summary>
    /// Files that the source batch creates before launching winws (for example,
    /// user lists from service.bat). The importer describes them but never writes
    /// into the source config directory.
    /// </summary>
    public required IReadOnlyList<ZapretConfigImportGeneratedFile> GeneratedFiles { get; init; }

    public required IReadOnlyList<ZapretConfigImportVariable> Variables { get; init; }

    public bool IsSuccessful =>
        Config != null && Issues.All(issue => issue.Severity != ZapretConfigImportIssueSeverity.Error);
}
