using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;
using CDPIUI.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Installs an analyzed Config into the local user kit. Dependencies are kept in
/// a per-import directory so imports cannot overwrite each other's files.
/// </summary>
public sealed partial class ConfigImportInstaller
{
    private const string ConvertedDirectoryName = "Converted";
    private const string LuaDirectoryName = "Lua";

    public async Task<ConfigImportInstallResult> InstallAsync(
        ConfigImportResult result,
        string displayName)
    {
        if (!result.IsSuccessful || result.Config == null)
            return Failure("IMPORT_RESULT_INVALID");
        if (string.IsNullOrWhiteSpace(displayName))
            return Failure("IMPORT_NAME_REQUIRED");
        if (!HasAllMissingFileResolutions(result))
            return Failure("IMPORT_MISSING_FILE_RESOLUTION_REQUIRED");

        if (result.SharedPackage != null)
        {
            try
            {
                var installed = await new ConfigShare.ConfigShareService().InstallAsync(
                    result.SharedPackage, displayName, editedConfig: result.Config);
                return new ConfigImportInstallResult { ConfigFileName = installed.ConfigFileName, PackId = installed.PackId };
            }
            catch (ConfigShare.ConfigShareException exception)
            {
                return Failure($"{exception.Code}: {exception.Message}");
            }
        }

        string sourceDirectory = Path.GetDirectoryName(result.SourcePath)!;
        string localPackDirectory = ConfigurationService.GetItemFolderFromPackId(SharedConstants.LocalUserItemsId);
        string importId = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..24];
        List<ImportResource> resources = BuildResources(result, sourceDirectory, importId);
        var directories = resources
            .Select(resource => resource.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(category => new ImportDirectory(
                category,
                Path.Combine(localPackDirectory, category, ConvertedDirectoryName),
                Path.Combine(localPackDirectory, category, ConvertedDirectoryName, $".{importId}.tmp"),
                Path.Combine(localPackDirectory, category, ConvertedDirectoryName, importId)))
            .ToList();

        try
        {
            foreach (ImportDirectory directory in directories)
            {
                Directory.CreateDirectory(directory.Root);
                Directory.CreateDirectory(directory.Staging);
            }

            foreach (ImportResource resource in resources)
            {
                ImportDirectory directory = directories.First(item =>
                    item.Category.Equals(resource.Category, StringComparison.OrdinalIgnoreCase));
                string destinationPath = GetSafeDestination(directory.Staging, resource.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                if (resource.GeneratedContent != null)
                {
                    File.WriteAllText(destinationPath, resource.GeneratedContent, new UTF8Encoding(false));
                }
                else if (!string.IsNullOrWhiteSpace(resource.SourcePath))
                {
                    File.Copy(resource.SourcePath, destinationPath, overwrite: false);
                }
                else
                {
                    File.WriteAllBytes(destinationPath, []);
                }
            }

            ConfigItem config = PrepareConfig(result, displayName, resources);

            foreach (ImportDirectory directory in directories)
                Directory.Move(directory.Staging, directory.Destination);
            string fileName = GetUniqueConfigFileName(localPackDirectory, displayName);
            string errorCode = await ConfigurationService.SaveConfigItem(
                fileName,
                SharedConstants.LocalUserItemsId,
                config);
            if (!string.IsNullOrEmpty(errorCode))
            {
                foreach (ImportDirectory directory in directories)
                    DeleteImportDirectory(directory.Destination, directory.Root);
                DeleteConfigFile(Path.Combine(localPackDirectory, fileName), localPackDirectory);
                return Failure(errorCode);
            }

            return new ConfigImportInstallResult
            {
                ConfigFileName = fileName,
                PackId = SharedConstants.LocalUserItemsId,
                ResourceDirectory = directories.FirstOrDefault()?.Destination,
            };
        }
        catch (Exception exception)
        {
            foreach (ImportDirectory directory in directories)
            {
                DeleteImportDirectory(directory.Staging, directory.Root);
                DeleteImportDirectory(directory.Destination, directory.Root);
            }
            return Failure($"IMPORT_INSTALL_FAILED: {exception.Message}");
        }
    }

    public ConfigItem PrepareConfig(
        ConfigImportResult result,
        string displayName,
        string importId)
    {
        if (!result.IsSuccessful || result.Config == null)
            throw new InvalidOperationException("The import result is not successful.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("A Config name is required.", nameof(displayName));
        if (!HasAllMissingFileResolutions(result))
            throw new InvalidOperationException("Every missing file must have an explicit resolution.");

        List<ImportResource> resources = BuildResources(
            result,
            Path.GetDirectoryName(result.SourcePath)!,
            importId);
        return PrepareConfig(result, displayName, resources);
    }

    private static ConfigItem PrepareConfig(
        ConfigImportResult result,
        string displayName,
        IReadOnlyList<ImportResource> resources)
    {
        ConfigItem config = CloneConfig(result.Config!);
        config.name = displayName.Trim();
        config.not_converted_name = config.name;
        RewriteResourcePaths(config, resources);
        return config;
    }

    private static ConfigItem CloneConfig(ConfigItem config) =>
        JSONConvertor.DeserializeObject<ConfigItem>(JSONConvertor.SerializeObject(config))
        ?? throw new InvalidOperationException("Cannot clone imported Config.");

    private static void RewriteResourcePaths(
        ConfigItem config,
        IReadOnlyList<ImportResource> resources)
    {
        config.startup_string = RewriteResources(config.startup_string, resources);
        if (config.variables != null)
        {
            for (int index = 0; index < config.variables.Count; index++)
                config.variables[index] = RewriteResources(config.variables[index], resources)!;
        }
        if (config.commaVars != null)
        {
            foreach (string key in config.commaVars.Keys.ToList())
                config.commaVars[key] = RewriteResources(config.commaVars[key], resources)!;
        }
        if (config.availableCommaVarsValues != null)
        {
            foreach (AvailableVarValues values in config.availableCommaVarsValues)
            {
                if (values.Values == null)
                    continue;
                for (int index = 0; index < values.Values.Count; index++)
                    values.Values[index] = RewriteResources(values.Values[index], resources)!;
            }
        }
    }

    private static string? RewriteResources(
        string? value,
        IReadOnlyList<ImportResource> resources)
    {
        if (value == null)
            return null;

        string result = value;
        foreach (ResourceDirectoryMapping mapping in BuildDirectoryMappings(resources))
            result = RewriteResourceDirectory(result, mapping);

        foreach (ImportResource resource in resources)
        {
            string normalizedRelativePath = resource.SourceRelativePath.Replace('\\', '/').TrimStart('/');
            string replacement = $"$GETCURRENTDIR()/{resource.SavedRelativePath}";
            result = ReplacePath(result, resource.ExpectedPath, replacement);

            string pathPattern = CreatePathPattern(normalizedRelativePath);
            result = Regex.Replace(
                result,
                $@"\$GETCURRENTDIR\(\)[\\/]+{pathPattern}",
                _ => replacement,
                RegexOptions.IgnoreCase);
            result = Regex.Replace(
                result,
                $@"(?<![A-Za-z0-9_./\\$])(?<at>@?)(?:\.[\\/])?{pathPattern}(?![A-Za-z0-9_./\\])",
                match => $"{match.Groups["at"].Value}{replacement}",
                RegexOptions.IgnoreCase);
        }
        return result;
    }

    private static IReadOnlyList<ResourceDirectoryMapping> BuildDirectoryMappings(
        IReadOnlyList<ImportResource> resources)
    {
        return resources
            .Select(resource => new
            {
                SourceDirectory = NormalizeRelativeDirectory(resource.SourceRelativePath),
                AbsoluteDirectory = Path.GetDirectoryName(resource.ExpectedPath),
                SavedDirectory = NormalizeRelativeDirectory(resource.SavedRelativePath),
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceDirectory) &&
                           !string.IsNullOrWhiteSpace(item.SavedDirectory))
            .GroupBy(item => item.SourceDirectory, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                string[] savedDirectories = group
                    .Select(item => item.SavedDirectory)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (savedDirectories.Length != 1)
                    return null;

                return new ResourceDirectoryMapping(
                    group.Key,
                    group.Select(item => item.AbsoluteDirectory)
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Select(path => Path.GetFullPath(path!))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    savedDirectories[0]);
            })
            .Where(mapping => mapping != null)
            .Cast<ResourceDirectoryMapping>()
            .ToArray();
    }

    private static string RewriteResourceDirectory(
        string value,
        ResourceDirectoryMapping mapping)
    {
        string sourcePattern = CreatePathPattern(mapping.SourceDirectory);
        string replacement = $"$GETCURRENTDIR()/{mapping.SavedDirectory}";
        string result = Regex.Replace(
            value,
            $@"\$GETCURRENTDIR\(\)[\\/]+{sourcePattern}(?=[\\/]|$)",
            _ => replacement,
            RegexOptions.IgnoreCase);
        result = Regex.Replace(
            result,
            $@"%~dp0{sourcePattern}(?=[\\/]|$)",
            _ => replacement,
            RegexOptions.IgnoreCase);

        foreach (string absoluteDirectory in mapping.AbsoluteDirectories)
        {
            result = Regex.Replace(
                result,
                $@"{CreatePathPattern(absoluteDirectory)}(?=[\\/]|$)",
                _ => replacement,
                RegexOptions.IgnoreCase);
        }

        return Regex.Replace(
            result,
            $@"(?<![A-Za-z0-9_./\\$])(?:\.[\\/])?{sourcePattern}(?=[\\/])",
            _ => replacement,
            RegexOptions.IgnoreCase);
    }

    private static string NormalizeRelativeDirectory(string path)
    {
        string normalized = path.Replace('\\', '/').Trim('/');
        int separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex < 0 ? string.Empty : normalized[..separatorIndex];
    }

    private static List<ImportResource> BuildResources(
        ConfigImportResult result,
        string sourceDirectory,
        string importId)
    {
        var resolutions = result.MissingFileResolutions.ToDictionary(
            resolution => Path.GetFullPath(resolution.MissingPath),
            resolution => resolution,
            StringComparer.OrdinalIgnoreCase);
        var resources = new List<ImportResource>();
        var expectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var savedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddResource(string expectedPath, string? sourcePath, string? generatedContent, string? relativePath = null)
        {
            string fullExpectedPath = Path.GetFullPath(expectedPath);
            if (!expectedPaths.Add(fullExpectedPath))
                return;

            string sourceRelativePath = relativePath ?? GetSourceRelativePath(sourceDirectory, fullExpectedPath);
            string category = GetResourceCategory(fullExpectedPath);
            string organizedRelativePath = GetOrganizedRelativePath(sourceRelativePath, category);
            string savedRelativePath = $"{category}/{ConvertedDirectoryName}/{importId}/{organizedRelativePath.Replace('\\', '/')}";
            if (!savedPaths.Add(savedRelativePath))
            {
                organizedRelativePath = AddPathHash(organizedRelativePath, fullExpectedPath);
                savedRelativePath = $"{category}/{ConvertedDirectoryName}/{importId}/{organizedRelativePath.Replace('\\', '/')}";
                savedPaths.Add(savedRelativePath);
            }

            resources.Add(new ImportResource(
                fullExpectedPath,
                sourceRelativePath,
                category,
                organizedRelativePath,
                savedRelativePath,
                sourcePath,
                generatedContent));
        }

        foreach (string referencedFile in result.ReferencedFiles)
            AddResource(referencedFile, Path.GetFullPath(referencedFile), null);

        foreach (ConfigImportGeneratedFile generatedFile in result.GeneratedFiles)
        {
            AddResource(
                Path.Combine(sourceDirectory, generatedFile.RelativePath),
                null,
                generatedFile.Content,
                generatedFile.RelativePath);
        }

        foreach (string missingFile in result.MissingReferencedFiles)
        {
            string fullMissingPath = Path.GetFullPath(missingFile);
            string? replacementPath = resolutions.TryGetValue(fullMissingPath, out ConfigImportMissingFileResolution? resolution)
                ? resolution.ReplacementPath
                : null;
            AddResource(fullMissingPath, replacementPath, null);
        }

        return resources;
    }

    private static string GetSourceRelativePath(string sourceDirectory, string path)
    {
        try
        {
            return GetSafeRelativePath(sourceDirectory, path);
        }
        catch (InvalidOperationException)
        {
            return Path.GetFileName(path);
        }
    }

    private static string GetResourceCategory(string path)
    {
        string extension = Path.GetExtension(path);
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return SharedConstants.LocalUserItemSiteListsFolder;
        if (extension.Equals(".lua", StringComparison.OrdinalIgnoreCase))
            return LuaDirectoryName;
        return SharedConstants.LocalUserItemBinsFolder;
    }

    private static string GetOrganizedRelativePath(string relativePath, string category)
    {
        string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        string[] parts = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "empty.bin";

        bool stripFirst = category.Equals(SharedConstants.LocalUserItemSiteListsFolder, StringComparison.OrdinalIgnoreCase)
            ? parts[0].Equals("list", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("lists", StringComparison.OrdinalIgnoreCase)
            : category.Equals(LuaDirectoryName, StringComparison.OrdinalIgnoreCase)
                ? parts[0].Equals("lua", StringComparison.OrdinalIgnoreCase)
                : parts[0].Equals("bin", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("fake", StringComparison.OrdinalIgnoreCase);
        string result = string.Join(Path.DirectorySeparatorChar, stripFirst ? parts.Skip(1) : parts);
        return string.IsNullOrWhiteSpace(result) ? Path.GetFileName(normalized) : result;
    }

    private static string AddPathHash(string relativePath, string expectedPath)
    {
        string directory = Path.GetDirectoryName(relativePath) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(relativePath);
        string extension = Path.GetExtension(relativePath);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedPath)))[..8];
        return Path.Combine(directory, $"{fileName}_{hash}{extension}");
    }

    private static string ReplacePath(string value, string path, string replacement)
    {
        if (string.IsNullOrWhiteSpace(path))
            return value;
        return Regex.Replace(
            value,
            CreatePathPattern(path),
            _ => replacement,
            RegexOptions.IgnoreCase);
    }

    private static string CreatePathPattern(string path) =>
        Regex.Escape(path.Replace('\\', '/')).Replace("/", @"[\\/]+");

    private static string GetSafeRelativePath(string sourceRoot, string sourcePath)
    {
        string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
        if (Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Referenced file is outside the imported Config directory: {sourcePath}");
        }
        return relativePath;
    }

    private static string GetSafeDestination(string root, string relativePath)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string destination = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!destination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsafe imported file path: {relativePath}");
        return destination;
    }

    private static string GetUniqueConfigFileName(string localPackDirectory, string displayName)
    {
        string safeName = InvalidFileNameRegex().Replace(displayName.Trim(), "_");
        safeName = WhitespaceRegex().Replace(safeName, "_").Trim('_', '.');
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "Imported_Config";

        string candidate = $"{safeName}.json";
        for (int suffix = 2; File.Exists(Path.Combine(localPackDirectory, candidate)); suffix++)
            candidate = $"{safeName}_{suffix}.json";
        return candidate;
    }

    private static bool HasAllMissingFileResolutions(ConfigImportResult result)
    {
        var resolvedPaths = result.MissingFileResolutions
            .Select(resolution => Path.GetFullPath(resolution.MissingPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return result.MissingReferencedFiles.All(path =>
            resolvedPaths.Contains(Path.GetFullPath(path)));
    }

    private static void DeleteImportDirectory(string path, string resourcesRoot)
    {
        try
        {
            string fullRoot = Path.GetFullPath(resourcesRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);
        }
        catch
        {
            // Best-effort rollback; the original error is more useful to the caller.
        }
    }

    private static void DeleteConfigFile(string path, string localPackDirectory)
    {
        try
        {
            string fullRoot = Path.GetFullPath(localPackDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch
        {
            // Best-effort rollback.
        }
    }

    private static ConfigImportInstallResult Failure(string errorCode) => new() { ErrorCode = errorCode };

    [GeneratedRegex(@"[^\p{L}\p{Nd}_.-]")]
    private static partial Regex InvalidFileNameRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record ImportResource(
        string ExpectedPath,
        string SourceRelativePath,
        string Category,
        string RelativePath,
        string SavedRelativePath,
        string? SourcePath,
        string? GeneratedContent);

    private sealed record ImportDirectory(
        string Category,
        string Root,
        string Staging,
        string Destination);

    private sealed record ResourceDirectoryMapping(
        string SourceDirectory,
        IReadOnlyList<string> AbsoluteDirectories,
        string SavedDirectory);
}
