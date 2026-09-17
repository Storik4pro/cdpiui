#nullable enable

using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared;
using CDPIUI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unidecode.NET;
using CDPIUI.Core.ComponentServices.Helpers;

namespace CDPIUI.Helper.CreateConfigHelper;

public sealed class ConfigMakerPresetSaveResult
{
    public bool Success { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorDetails { get; init; } = string.Empty;
    public string PackId { get; init; } = string.Empty;
    public string ConfigFileName { get; init; } = string.Empty;
    public int CopiedFileCount { get; init; }
    public IReadOnlyList<ConfigMakerResourceMetadata> StoredResources { get; init; } = [];

    public static ConfigMakerPresetSaveResult Failed(string code, string details = "") => new()
    {
        ErrorCode = code,
        ErrorDetails = details,
    };
}

public sealed class ConfigMakerPresetStorageService
{
    private const string StorageFolderName = "ConfigMaker";

    public Task<ConfigMakerPresetSaveResult> SaveAsync(
        string presetName,
        ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ConfigMakerPresetDocument snapshot = CloneDocument(document);
        return Task.Run(() => SaveCoreAsync(presetName, snapshot, overwrite: false));
    }

    public Task<ConfigMakerPresetSaveResult> OverwriteAsync(
        ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ConfigMakerPresetDocument snapshot = CloneDocument(document);
        return Task.Run(() => SaveCoreAsync(snapshot.Name, snapshot, overwrite: true));
    }

    private static async Task<ConfigMakerPresetSaveResult> SaveCoreAsync(
        string presetName,
        ConfigMakerPresetDocument document,
        bool overwrite)
    {
        string normalizedName = NormalizeDisplayName(presetName);
        if (normalizedName.Length == 0)
        {
            return ConfigMakerPresetSaveResult.Failed("NAME_EMPTY");
        }
        if (string.IsNullOrWhiteSpace(document.ComponentId))
        {
            return ConfigMakerPresetSaveResult.Failed("COMPONENT_EMPTY");
        }
        if (string.IsNullOrWhiteSpace(document.CommandText))
        {
            return ConfigMakerPresetSaveResult.Failed("COMMAND_EMPTY");
        }

        string destinationPackId;
        string configFileName;
        if (overwrite)
        {
            destinationPackId = document.PackId?.Trim() ?? string.Empty;
            configFileName = document.FileName?.Trim() ?? string.Empty;
            if (destinationPackId.Length == 0 ||
                configFileName.Length == 0 ||
                !string.Equals(configFileName, Path.GetFileName(configFileName), StringComparison.Ordinal))
            {
                return ConfigMakerPresetSaveResult.Failed("OVERWRITE_TARGET_MISSING");
            }
        }
        else
        {
            destinationPackId = SharedConstants.LocalUserItemsId;
            configFileName = string.Empty;
        }

        string componentDirectory = DatabaseHelper.Instance.GetItemById(document.ComponentId)?.Directory ?? string.Empty;
        if (string.IsNullOrWhiteSpace(componentDirectory) || !Directory.Exists(componentDirectory))
        {
            return ConfigMakerPresetSaveResult.Failed("COMPONENT_UNAVAILABLE");
        }

        string userStorage = Directories.StoreLocalUserItemDirectory;
        Directory.CreateDirectory(userStorage);
        string destinationPresetDirectory = ConfigurationService.GetItemFolderFromPackId(destinationPackId);
        if (overwrite && !Directory.Exists(destinationPresetDirectory))
        {
            return ConfigMakerPresetSaveResult.Failed("PACK_UNAVAILABLE", destinationPackId);
        }
        Directory.CreateDirectory(destinationPresetDirectory);

        string sourcePresetDirectory = GetSourcePresetDirectory(document, userStorage);
        string storageId = overwrite
            ? CreateOverwriteStorageId(configFileName)
            : CreateStorageId(normalizedName, destinationPresetDirectory);
        if (!overwrite)
        {
            configFileName = $"{storageId}.json";
        }
        string listRoot = Path.Combine(
            destinationPresetDirectory,
            SharedConstants.LocalUserItemSiteListsFolder,
            StorageFolderName,
            storageId);
        string binRoot = Path.Combine(
            destinationPresetDirectory,
            SharedConstants.LocalUserItemBinsFolder,
            StorageFolderName,
            storageId);
        List<string> createdRoots = [];
        List<string> createdFiles = [];

        try
        {
            Dictionary<string, string> replacements = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> copiedFiles = new(StringComparer.OrdinalIgnoreCase);
            List<ConfigMakerResourceMetadata> storedResources = [];
            int copiedFileCount = 0;
            ConfigItem sourceConfig = document.ToConfigItem(
                document.PackId ?? destinationPackId,
                normalizedName);

            foreach (ConfigMakerPresetResource resource in document.Resources)
            {
                string? sourcePath = ResolveResourcePath(
                    resource.Path,
                    componentDirectory,
                    sourcePresetDirectory,
                    sourceConfig);
                if (sourcePath == null)
                {
                    Cleanup(createdFiles, createdRoots);
                    return ConfigMakerPresetSaveResult.Failed("FILE_MISSING", resource.Path);
                }

                string storedReference;
                bool keepComponentReference = resource.IsBuiltIn && IsPathInside(sourcePath, componentDirectory);
                if (keepComponentReference)
                {
                    storedReference = "/" + Path.GetRelativePath(componentDirectory, sourcePath).Replace('\\', '/');
                }
                else if (IsPathInside(sourcePath, destinationPresetDirectory))
                {
                    storedReference = string.Join(
                        '/',
                        "$GETCURRENTDIR()",
                        Path.GetRelativePath(destinationPresetDirectory, sourcePath).Replace('\\', '/'));
                }
                else
                {
                    string destinationRoot = resource.Kind == ConfigMakerResourceKind.SiteList
                        ? listRoot
                        : binRoot;
                    if (!Directory.Exists(destinationRoot))
                    {
                        Directory.CreateDirectory(destinationRoot);
                        createdRoots.Add(destinationRoot);
                    }
                    string copyKey = $"{resource.Kind}|{sourcePath}";
                    if (!copiedFiles.TryGetValue(copyKey, out string? destinationPath))
                    {
                        destinationPath = GetUniqueDestinationPath(
                            destinationRoot,
                            Path.GetFileName(sourcePath));
                        File.Copy(sourcePath, destinationPath, overwrite: false);
                        copiedFiles[copyKey] = destinationPath;
                        createdFiles.Add(destinationPath);
                        copiedFileCount++;
                    }
                    string category = resource.Kind == ConfigMakerResourceKind.SiteList
                        ? SharedConstants.LocalUserItemSiteListsFolder
                        : SharedConstants.LocalUserItemBinsFolder;
                    storedReference = string.Join(
                        '/',
                        "$GETCURRENTDIR()",
                        category,
                        StorageFolderName,
                        storageId,
                        Path.GetFileName(destinationPath));
                }

                replacements[resource.Alias] = storedReference;
                storedResources.Add(new ConfigMakerResourceMetadata
                {
                    alias = resource.Alias,
                    path = storedReference,
                    kind = resource.Kind.ToString(),
                    isBuiltIn = keepComponentReference,
                });
            }

            ConfigItem config = document.ToConfigItem(
                destinationPackId,
                normalizedName);
            string targetVersion = !string.IsNullOrWhiteSpace(document.TargetVersion)
                ? document.TargetVersion
                : DatabaseHelper.Instance.GetItemById(document.ComponentId)?.CurrentVersion;
            config.target =
            [
                document.ComponentId,
                string.IsNullOrWhiteSpace(targetVersion) ? "%CURRENT%" : targetVersion,
            ];
            config.RewritePresetReferences(replacements);
            if (config.configMaker != null || storedResources.Count > 0)
            {
                config.configMaker ??= new ConfigMakerPresetMetadata();
                config.configMaker.resources = storedResources.Count == 0 ? null : storedResources;
            }
            config.NormalizeForStorage();

            ConfigUsedFile? missingFile = config.UsedFiles.FirstOrDefault(file =>
            {
                try
                {
                    return !File.Exists(config.ResolveFilePath(
                        file.Path,
                        componentDirectory,
                        destinationPresetDirectory));
                }
                catch
                {
                    return true;
                }
            });
            if (missingFile != null)
            {
                Cleanup(createdFiles, createdRoots);
                return ConfigMakerPresetSaveResult.Failed("FILE_MISSING", missingFile.ExpandedPath);
            }

            string errorCode = await ConfigurationService.SaveConfigItem(
                configFileName,
                destinationPackId,
                config);
            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                Cleanup(createdFiles, createdRoots);
                return ConfigMakerPresetSaveResult.Failed("SAVE_FAILED", errorCode);
            }

            TrySaveVariableDescriptions(document, destinationPackId);

            try
            {
                ComponentItemsLoaderHelper.Instance
                    .GetComponentHelperFromId(document.ComponentId)?
                    .ReInitConfigs();
            }
            catch
            {
                // The config and its resources are already safely stored. A later reload will pick them up.
            }

            return new ConfigMakerPresetSaveResult
            {
                Success = true,
                PackId = destinationPackId,
                ConfigFileName = configFileName,
                CopiedFileCount = copiedFileCount,
                StoredResources = storedResources,
            };
        }
        catch (Exception exception)
        {
            Cleanup(createdFiles, createdRoots);
            return ConfigMakerPresetSaveResult.Failed("UNEXPECTED", exception.Message);
        }
    }

    public static string CreateResolvedCommandForTest(ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Dictionary<string, string> replacements = CreateResourceReplacements(
            document,
            keepPortableComponentPaths: false);

        ConfigItem runtimeConfig = document.ToConfigItem(
            SharedConstants.LocalUserItemsId,
            document.Name);
        runtimeConfig.RewritePresetReferences(replacements);
        return ConfigurationService.GetStartupParametersByConfigItem(runtimeConfig);
    }

    public static string CreateResolvedCommandForTextExport(ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.HasVariables)
        {
            throw new InvalidOperationException("VARIABLES_NOT_SUPPORTED");
        }
        Dictionary<string, string> replacements = CreateResourceReplacements(
            document,
            keepPortableComponentPaths: true);
        return RewritePresetReferences(document.CommandText, replacements);
    }

    private static Dictionary<string, string> CreateResourceReplacements(
        ConfigMakerPresetDocument document,
        bool keepPortableComponentPaths)
    {
        string componentDirectory = DatabaseHelper.Instance.GetItemById(document.ComponentId)?.Directory ?? string.Empty;
        string userStorage = Directories.StoreLocalUserItemDirectory;
        string presetDirectory = GetSourcePresetDirectory(document, userStorage);
        ConfigItem sourceConfig = document.ToConfigItem(
            document.PackId ?? SharedConstants.LocalUserItemsId,
            document.Name);
        Dictionary<string, string> replacements = new(StringComparer.OrdinalIgnoreCase);
        foreach (ConfigMakerPresetResource resource in document.Resources)
        {
            string? fullPath = ResolveResourcePath(
                resource.Path,
                componentDirectory,
                presetDirectory,
                sourceConfig);
            if (fullPath == null)
            {
                throw new FileNotFoundException(null, resource.Path);
            }
            replacements[resource.Alias] = keepPortableComponentPaths &&
                resource.IsBuiltIn &&
                !string.IsNullOrWhiteSpace(componentDirectory) &&
                IsPathInside(fullPath, componentDirectory)
                    ? "/" + Path.GetRelativePath(componentDirectory, fullPath).Replace('\\', '/')
                    : fullPath;
        }
        return replacements;
    }

    public static string RewritePresetReferences(
        string commandText,
        IReadOnlyDictionary<string, string> replacements) =>
        ConfigFileReferences.RewritePresetReferences(commandText, replacements);

    private static string? ResolveResourcePath(
        string sourcePath,
        string componentDirectory,
        string presetDirectory,
        ConfigItem sourceConfig)
    {
        try
        {
            string candidate = sourceConfig.ResolveFilePath(
                sourcePath,
                componentDirectory,
                presetDirectory);
            return File.Exists(candidate) ? candidate : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetSourcePresetDirectory(
        ConfigMakerPresetDocument document,
        string fallbackDirectory)
    {
        if (string.IsNullOrWhiteSpace(document.PackId))
        {
            return fallbackDirectory;
        }
        string directory = ConfigurationService.GetItemFolderFromPackId(document.PackId);
        return Directory.Exists(directory) ? directory : fallbackDirectory;
    }

    private static bool IsPathInside(string path, string root)
    {
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateStorageId(string presetName, string userStorage)
    {
        string slug = presetName.Unidecode();
        slug = Regex.Replace(slug, @"\s+", "_");
        slug = Regex.Replace(slug, @"[^A-Za-z0-9_.-]", string.Empty);
        slug = Regex.Replace(slug, "_+", "_").Trim('_');
        if (slug.Length == 0)
        {
            slug = "preset";
        }
        if (slug.Length > 48)
        {
            slug = slug[..48].TrimEnd('_', '.', '-');
        }
        string baseId = $"{slug}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        string candidate = baseId;
        int suffix = 2;
        while (File.Exists(Path.Combine(userStorage, $"{candidate}.json")) ||
               Directory.Exists(Path.Combine(
                   userStorage,
                   SharedConstants.LocalUserItemSiteListsFolder,
                   StorageFolderName,
                   candidate)) ||
               Directory.Exists(Path.Combine(
                   userStorage,
                   SharedConstants.LocalUserItemBinsFolder,
                   StorageFolderName,
                   candidate)))
        {
            candidate = $"{baseId}_{suffix++}";
        }
        return candidate;
    }

    private static string CreateOverwriteStorageId(string configFileName)
    {
        string storageId = Path.GetFileNameWithoutExtension(configFileName).Unidecode();
        storageId = Regex.Replace(storageId, @"\s+", "_");
        storageId = Regex.Replace(storageId, @"[^A-Za-z0-9_.-]", string.Empty);
        storageId = Regex.Replace(storageId, "_+", "_").Trim('_', '.', '-');
        return storageId.Length == 0 ? "preset" : storageId;
    }

    private static string GetUniqueDestinationPath(string directory, string sourceFileName)
    {
        string asciiName = sourceFileName.Unidecode();
        asciiName = Regex.Replace(asciiName, @"[^A-Za-z0-9_.-]", "_");
        asciiName = Regex.Replace(asciiName, "_+", "_").Trim('_');
        if (asciiName.Length == 0)
        {
            asciiName = "resource.bin";
        }
        string stem = Path.GetFileNameWithoutExtension(asciiName);
        string extension = Path.GetExtension(asciiName);
        string candidate = Path.Combine(directory, asciiName);
        int suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{stem}_{suffix++}{extension}");
        }
        return candidate;
    }

    private static string NormalizeDisplayName(string? value) =>
        Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    private static void TrySaveVariableDescriptions(
        ConfigMakerPresetDocument document,
        string packId)
    {
        try
        {
            string localePath = ConfigurationService.GetDefaultLocalePath(packId);
            if (string.IsNullOrWhiteSpace(localePath))
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(localePath)!);
            Dictionary<string, string> values = File.Exists(localePath)
                ? JSONConvertor.LoadJson<Dictionary<string, string>>(localePath) ?? []
                : [];
            bool changed = false;
            foreach (ConfigMakerVariableDefinition variable in document.Variables.Where(variable =>
                         variable.Kind == ConfigMakerVariableKind.Switch &&
                         !string.IsNullOrWhiteSpace(variable.InternalParameterName)))
            {
                values[variable.InternalParameterName] = string.IsNullOrWhiteSpace(variable.Description)
                    ? variable.Name
                    : variable.Description;
                changed = true;
            }
            if (changed)
            {
                File.WriteAllText(localePath, System.Text.Json.JsonSerializer.Serialize(values));
            }
        }
        catch
        {
            // Variable localization is editor metadata. It must not make the preset unusable.
        }
    }

    private static ConfigMakerPresetDocument CloneDocument(ConfigMakerPresetDocument source)
    {
        ConfigMakerPresetDocument result = new()
        {
            PackId = source.PackId,
            FileName = source.FileName,
            Meta = source.Meta,
            TargetVersion = source.TargetVersion,
            ComponentId = source.ComponentId,
            Name = source.Name,
            CommandText = source.CommandText,
        };
        foreach (ConfigMakerVariableDefinition sourceVariable in source.Variables)
        {
            ConfigMakerVariableDefinition variable = new()
            {
                Id = sourceVariable.Id,
                Name = sourceVariable.Name,
                Kind = sourceVariable.Kind,
                StorageKind = sourceVariable.StorageKind,
                Value = sourceVariable.Value,
                Description = sourceVariable.Description,
                OnValue = sourceVariable.OnValue,
                OffValue = sourceVariable.OffValue,
                InternalParameterName = sourceVariable.InternalParameterName,
                IsSwitchEnabled = sourceVariable.IsSwitchEnabled,
            };
            foreach (string value in sourceVariable.Values)
            {
                variable.Values.Add(value);
            }
            result.Variables.Add(variable);
        }
        foreach (ConfigMakerPresetResource sourceResource in source.Resources)
        {
            result.Resources.Add(new ConfigMakerPresetResource
            {
                Alias = sourceResource.Alias,
                Path = sourceResource.Path,
                Kind = sourceResource.Kind,
                IsBuiltIn = sourceResource.IsBuiltIn,
            });
        }
        return result;
    }

    private static void Cleanup(
        IEnumerable<string> files,
        IEnumerable<string> roots)
    {
        foreach (string file in files)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
            }
        }
        foreach (string root in roots.OrderByDescending(path => path.Length))
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
