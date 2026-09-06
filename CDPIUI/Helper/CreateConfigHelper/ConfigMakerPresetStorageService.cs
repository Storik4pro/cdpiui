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
    public string ConfigFileName { get; init; } = string.Empty;
    public int CopiedFileCount { get; init; }

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
        return Task.Run(() => SaveCoreAsync(presetName, snapshot));
    }

    private static async Task<ConfigMakerPresetSaveResult> SaveCoreAsync(
        string presetName,
        ConfigMakerPresetDocument document)
    {
        string normalizedName = (presetName ?? string.Empty).Trim();
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

        string componentDirectory = DatabaseHelper.Instance.GetItemById(document.ComponentId)?.Directory ?? string.Empty;
        if (string.IsNullOrWhiteSpace(componentDirectory) || !Directory.Exists(componentDirectory))
        {
            return ConfigMakerPresetSaveResult.Failed("COMPONENT_UNAVAILABLE");
        }

        string userStorage = Directories.StoreLocalUserItemDirectory;
        Directory.CreateDirectory(userStorage);
        string storageId = CreateStorageId(normalizedName, userStorage);
        string listRoot = Path.Combine(
            userStorage,
            SharedConstants.LocalUserItemSiteListsFolder,
            StorageFolderName,
            storageId);
        string binRoot = Path.Combine(
            userStorage,
            SharedConstants.LocalUserItemBinsFolder,
            StorageFolderName,
            storageId);
        List<string> createdRoots = [];

        try
        {
            Dictionary<string, string> replacements = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> copiedFiles = new(StringComparer.OrdinalIgnoreCase);
            List<ConfigMakerResourceMetadata> storedResources = [];
            int copiedFileCount = 0;

            foreach (ConfigMakerPresetResource resource in document.Resources)
            {
                string? sourcePath = ResolveResourcePath(
                    resource.Path,
                    componentDirectory,
                    userStorage);
                if (sourcePath == null)
                {
                    Cleanup(createdRoots);
                    return ConfigMakerPresetSaveResult.Failed("FILE_MISSING", resource.Path);
                }

                string storedReference;
                bool keepComponentReference = resource.IsBuiltIn && IsPathInside(sourcePath, componentDirectory);
                if (keepComponentReference)
                {
                    storedReference = "/" + Path.GetRelativePath(componentDirectory, sourcePath).Replace('\\', '/');
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
                SharedConstants.LocalUserItemsId,
                normalizedName);
            config.target =
            [
                document.ComponentId,
                DatabaseHelper.Instance.GetItemById(document.ComponentId)?.CurrentVersion ?? "%CURRENT%",
            ];
            config.RewritePresetReferences(replacements);
            config.configMaker ??= new ConfigMakerPresetMetadata();
            config.configMaker.resources = storedResources;

            string configFileName = $"{storageId}.json";
            string errorCode = await ConfigurationService.SaveConfigItem(
                configFileName,
                SharedConstants.LocalUserItemsId,
                config);
            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                Cleanup(createdRoots);
                return ConfigMakerPresetSaveResult.Failed("SAVE_FAILED", errorCode);
            }

            TrySaveVariableDescriptions(document);

            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(document.ComponentId)?.ReInitConfigs();

            return new ConfigMakerPresetSaveResult
            {
                Success = true,
                ConfigFileName = configFileName,
                CopiedFileCount = copiedFileCount,
            };
        }
        catch (Exception exception)
        {
            Cleanup(createdRoots);
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
        Dictionary<string, string> replacements = new(StringComparer.OrdinalIgnoreCase);
        foreach (ConfigMakerPresetResource resource in document.Resources)
        {
            string? fullPath = ResolveResourcePath(resource.Path, componentDirectory, userStorage);
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
        string userStorage)
    {
        string normalized = (sourcePath ?? string.Empty).Trim().Trim('"', '\'');
        if (normalized.StartsWith("$GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase))
        {
            string suffix = normalized["$GETCURRENTDIR()".Length..].TrimStart('/', '\\');
            string candidate = Path.GetFullPath(Path.Combine(
                userStorage,
                suffix.Replace('/', Path.DirectorySeparatorChar)));
            return File.Exists(candidate) ? candidate : null;
        }
        if (Path.IsPathFullyQualified(normalized))
        {
            string candidate = Path.GetFullPath(normalized);
            return File.Exists(candidate) ? candidate : null;
        }
        string relative = normalized.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        string[] candidates =
        [
            Path.Combine(componentDirectory, relative),
            Path.Combine(userStorage, relative),
            Path.Combine(Directories.CurrentDirectory, relative),
        ];
        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
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

    private static void TrySaveVariableDescriptions(ConfigMakerPresetDocument document)
    {
        try
        {
            string localePath = ConfigurationService.GetDefaultLocalePath(SharedConstants.LocalUserItemsId);
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

    private static void Cleanup(IEnumerable<string> roots)
    {
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
