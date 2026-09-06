namespace CDPIUI.AddOns.ConfigImport;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Redirects unavailable and batch-generated resources to short-lived temporary
/// files for a test run. No source files are changed and no executable is sandboxed.
/// </summary>
public sealed class ConfigImportTestPlaceholder : IDisposable
{
    private readonly string? _placeholderDirectory;

    private ConfigImportTestPlaceholder(string arguments, string? placeholderDirectory)
    {
        Arguments = arguments;
        _placeholderDirectory = placeholderDirectory;
    }

    public string Arguments { get; }

    public static ConfigImportTestPlaceholder Create(
        string arguments,
        string sourcePath,
        IReadOnlyList<string> missingReferencedFiles,
        IReadOnlyList<ConfigImportMissingFileResolution>? resolutions = null,
        IReadOnlyList<ConfigImportGeneratedFile>? generatedFiles = null)
    {
        generatedFiles ??= [];
        if (missingReferencedFiles.Count == 0 && generatedFiles.Count == 0)
            return new ConfigImportTestPlaceholder(arguments, null);

        var resolutionMap = (resolutions ?? [])
            .ToDictionary(
                resolution => Path.GetFullPath(resolution.MissingPath),
                resolution => resolution.ReplacementPath,
                StringComparer.OrdinalIgnoreCase);
        bool needsPlaceholder = missingReferencedFiles.Any(path =>
            !resolutionMap.TryGetValue(Path.GetFullPath(path), out string? replacement) ||
            string.IsNullOrWhiteSpace(replacement));

        bool needsTemporaryDirectory = needsPlaceholder || generatedFiles.Count > 0;
        string? placeholderDirectory = null;
        string? placeholderPath = null;
        if (needsTemporaryDirectory)
        {
            placeholderDirectory = Path.Combine(
                Path.GetTempPath(),
                "CDPIUI",
                "ConfigImportPlaceholders",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(placeholderDirectory);
        }
        if (needsPlaceholder)
        {
            placeholderPath = Path.Combine(placeholderDirectory!, "empty.bin");
            File.WriteAllBytes(placeholderPath, []);
        }

        string sourceDirectory = Path.GetDirectoryName(sourcePath)!;
        string rewritten = arguments;
        foreach (string missingFile in missingReferencedFiles)
        {
            string absolutePath = Path.GetFullPath(missingFile);
            string replacementPath = resolutionMap.TryGetValue(absolutePath, out string? selectedPath) &&
                                     !string.IsNullOrWhiteSpace(selectedPath)
                ? Path.GetFullPath(selectedPath)
                : placeholderPath!;
            rewritten = RewriteReferencedPath(
                rewritten,
                sourceDirectory,
                absolutePath,
                replacementPath);
        }

        if (generatedFiles.Count > 0)
        {
            string generatedDirectory = Path.Combine(placeholderDirectory!, "generated");
            Directory.CreateDirectory(generatedDirectory);
            for (int index = 0; index < generatedFiles.Count; index++)
            {
                ConfigImportGeneratedFile generatedFile = generatedFiles[index];
                string expectedPath = Path.GetFullPath(Path.Combine(sourceDirectory, generatedFile.RelativePath));
                string generatedPath = Path.Combine(
                    generatedDirectory,
                    $"{index:D4}_{Path.GetFileName(expectedPath)}");
                File.WriteAllText(generatedPath, generatedFile.Content, new UTF8Encoding(false));
                rewritten = RewriteReferencedPath(
                    rewritten,
                    sourceDirectory,
                    expectedPath,
                    generatedPath);
            }
        }

        return new ConfigImportTestPlaceholder(rewritten, placeholderDirectory);
    }

    private static string RewriteReferencedPath(
        string arguments,
        string sourceDirectory,
        string absolutePath,
        string replacementPath)
    {
        string rewritten = ReplacePath(arguments, absolutePath, replacementPath);
        string relativePath = Path.GetRelativePath(sourceDirectory, absolutePath);
        if (IsOutsideDirectory(relativePath))
            return rewritten;

        rewritten = ReplacePath(rewritten, $"$GETCURRENTDIR()/{relativePath}", replacementPath);
        return rewritten;
    }

    public void Dispose()
    {
        if (string.IsNullOrWhiteSpace(_placeholderDirectory))
            return;

        try
        {
            string allowedRoot = Path.GetFullPath(Path.Combine(
                    Path.GetTempPath(),
                    "CDPIUI",
                    "ConfigImportPlaceholders"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(_placeholderDirectory);
            if (fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup. A stale empty file is harmless.
        }
    }

    private static string ReplacePath(string value, string path, string replacement)
    {
        if (string.IsNullOrWhiteSpace(path))
            return value;

        string pattern = Regex.Escape(path.Replace('\\', '/'))
            .Replace("/", @"[\\/]+");
        return Regex.Replace(
            value,
            pattern,
            _ => replacement,
            RegexOptions.IgnoreCase);
    }

    private static bool IsOutsideDirectory(string relativePath) =>
        Path.IsPathRooted(relativePath) ||
        relativePath.Equals("..", StringComparison.Ordinal) ||
        relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
}
