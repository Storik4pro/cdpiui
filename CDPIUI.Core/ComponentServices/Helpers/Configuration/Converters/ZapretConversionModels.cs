namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

public enum ZapretConversionIssueSeverity
{
    Warning,
    Error
}

public sealed record ZapretConversionIssue(
    ZapretConversionIssueSeverity Severity,
    string Code,
    string Message,
    string? OptionName = null,
    int? ProfileIndex = null);

public enum ZapretConversionFileAccess
{
    Read,
    Write,
    ReadWrite
}

public sealed record ZapretConversionFileReference(
    string OptionName,
    string SourcePath,
    string ConvertedPath,
    long Offset,
    ZapretConversionFileAccess Access,
    string? BlobName = null);

public sealed class ZapretConversionOptions
{
    public string ZapretLibraryPath { get; init; } = "lua/zapret-lib.lua";

    public string ZapretAntiDpiLibraryPath { get; init; } = "lua/zapret-antidpi.lua";

    /// <summary>
    /// Maps a source file used by the legacy config to its path in the converted environment.
    /// The cache layer can use this to point blobs at __ConvertedZPRT/&lt;pack id&gt;/...
    /// </summary>
    public Func<string, string>? FilePathMapper { get; init; }

    /// <summary>
    /// The legacy implementation preserves the packet ordering of nfqws1 for reassembled payloads.
    /// </summary>
    public bool PreferLegacyMultidisorder { get; init; } = true;
}

public sealed class ZapretConversionResult
{
    public required string StartupString { get; init; }

    public required IReadOnlyList<ZapretConversionIssue> Issues { get; init; }

    public required IReadOnlyList<ZapretConversionFileReference> ReferencedFiles { get; init; }

    public bool IsSuccessful => Issues.All(issue => issue.Severity != ZapretConversionIssueSeverity.Error);
}
