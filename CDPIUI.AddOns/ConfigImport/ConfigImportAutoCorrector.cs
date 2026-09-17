using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Helpers;
using CDPIUI.Core.JSON;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Finds a safe existing replacement for an unavailable imported resource.
/// The lookup reuses the legacy Autocorrector without copying any files.
/// </summary>
public sealed class ConfigImportAutoCorrector
{
    public bool ShouldSuggestEmptyFile(string missingPath)
    {
        string fileName = Path.GetFileName(missingPath);
        return fileName.Contains("user", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("debug", StringComparison.OrdinalIgnoreCase);
    }

    public string? FindReplacement(ConfigImportResult result, string missingPath)
    {
        string fullMissingPath = Path.GetFullPath(missingPath);
        string? sourceCandidate = FindInSourceDirectory(result.SourcePath, fullMissingPath);
        if (sourceCandidate != null)
            return sourceCandidate;

        try
        {
            ConfigItem? config = result.Config == null
                ? null
                : JSONConvertor.DeserializeObject<ConfigItem>(JSONConvertor.SerializeObject(result.Config));
            if (config != null)
                config.packId = Path.GetDirectoryName(result.SourcePath);

            string candidate = Autocorrector.FindAutoCorrectPath(
                fullMissingPath,
                config!,
                result.SourcePath);
            if (File.Exists(candidate) &&
                !Path.GetFullPath(candidate).Equals(fullMissingPath, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(candidate);
            }
        }
        catch
        {
            // No suggestion is preferable to blocking the import page.
        }

        return null;
    }

    private static string? FindInSourceDirectory(string sourcePath, string missingPath)
    {
        try
        {
            string sourceDirectory = Path.GetDirectoryName(sourcePath)!;
            string fileName = Path.GetFileName(missingPath);
            return Directory
                .EnumerateFiles(sourceDirectory, fileName, SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .FirstOrDefault(path => !path.Equals(missingPath, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }
}
