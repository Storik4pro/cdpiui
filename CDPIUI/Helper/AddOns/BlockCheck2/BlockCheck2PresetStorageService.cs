using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared;
using CDPIUI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unidecode.NET;

namespace CDPIUI.Helper.AddOns.BlockCheck2;

public sealed class BlockCheck2PresetStorageService
{
    private const string StorageFolderName = "BlockCheck2";

    public Task<BlockCheck2PresetStorageResult> SaveAsync(
        string presetName,
        BlockCheck2PresetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        PresetSnapshot snapshot = new(
            draft.EffectiveArguments,
            draft.CanUseConfig,
            draft.Files.Select(file => new PresetFileSnapshot(file.Path, file.Kind)).ToArray());
        return Task.Run(() => SaveCoreAsync(presetName, snapshot));
    }

    private static async Task<BlockCheck2PresetStorageResult> SaveCoreAsync(
        string presetName,
        PresetSnapshot draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        string normalizedName = (presetName ?? string.Empty).Trim();
        if (normalizedName.Length == 0)
        {
            return BlockCheck2PresetStorageResult.Failed("NAME_EMPTY");
        }
        if (!draft.CanUseConfig)
        {
            return BlockCheck2PresetStorageResult.Failed("PRESET_EMPTY");
        }

        string componentId = HardcodedItemIds.ComponentIds[Components.Zapret2];
        ComponentHelper component = ComponentItemsLoaderHelper.Instance
            .GetComponentHelperFromId(componentId);
        string executablePath = component?.GetExecutablePath();
        string componentDirectory = string.IsNullOrWhiteSpace(executablePath)
            ? null
            : Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(componentDirectory) || !Directory.Exists(componentDirectory))
        {
            return BlockCheck2PresetStorageResult.Failed("COMPONENT_UNAVAILABLE");
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
            List<FileReplacement> replacements = [];
            Dictionary<string, string> copiedFiles = new(StringComparer.OrdinalIgnoreCase);
            int copiedFileCount = 0;

            foreach (PresetFileSnapshot file in draft.Files)
            {
                string sourcePath = ResolveSourcePath(
                    file.Path,
                    componentDirectory,
                    userStorage);
                if (sourcePath == null)
                {
                    Cleanup(createdRoots);
                    return BlockCheck2PresetStorageResult.Failed(
                        "FILE_MISSING",
                        file.Path);
                }

                if (file.Kind != BlockCheck2PresetFileKind.SiteList &&
                    IsPathInside(sourcePath, componentDirectory))
                {
                    string componentRelativePath = Path.GetRelativePath(
                            componentDirectory,
                            sourcePath)
                        .Replace('\\', '/');
                    replacements.Add(new FileReplacement(
                        file.Kind,
                        file.Path,
                        componentRelativePath));
                    continue;
                }

                string destinationRoot = file.Kind == BlockCheck2PresetFileKind.SiteList
                    ? listRoot
                    : binRoot;
                if (!Directory.Exists(destinationRoot))
                {
                    Directory.CreateDirectory(destinationRoot);
                    createdRoots.Add(destinationRoot);
                }

                string copyKey = $"{file.Kind}|{sourcePath}";
                if (!copiedFiles.TryGetValue(copyKey, out string destinationPath))
                {
                    destinationPath = GetUniqueDestinationPath(
                        destinationRoot,
                        Path.GetFileName(sourcePath));
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                    copiedFiles[copyKey] = destinationPath;
                    copiedFileCount++;
                }

                string category = file.Kind == BlockCheck2PresetFileKind.SiteList
                    ? SharedConstants.LocalUserItemSiteListsFolder
                    : SharedConstants.LocalUserItemBinsFolder;
                string storedReference = string.Join('/',
                    "$GETCURRENTDIR()",
                    category,
                    StorageFolderName,
                    storageId,
                    Path.GetFileName(destinationPath));
                replacements.Add(new FileReplacement(
                    file.Kind,
                    file.Path,
                    storedReference));
            }

            string startupArguments = RewriteFileReferences(
                draft.EffectiveArguments,
                replacements);
            string configFileName = $"{storageId}.json";
            ConfigItem config = CreateConfigPageHelper.CreateConfigItem(
                SharedConstants.LocalUserItemsId,
                normalizedName,
                componentId,
                [],
                [],
                [],
                [],
                startupArguments);
            string errorCode = await ConfigurationService.SaveConfigItem(
                configFileName,
                SharedConstants.LocalUserItemsId,
                config).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                Cleanup(createdRoots);
                return BlockCheck2PresetStorageResult.Failed("SAVE_FAILED", errorCode);
            }

            return new BlockCheck2PresetStorageResult
            {
                Success = true,
                PresetName = normalizedName,
                ConfigFileName = configFileName,
                CopiedFileCount = copiedFileCount,
            };
        }
        catch (Exception exception)
        {
            Cleanup(createdRoots);
            return BlockCheck2PresetStorageResult.Failed(
                "UNEXPECTED",
                exception.Message);
        }
    }

    private static string RewriteFileReferences(
        string arguments,
        IReadOnlyList<FileReplacement> replacements)
    {
        IReadOnlyList<string> tokens = ComponentCommandLineFormatter.Tokenize(arguments);
        List<string> output = [];
        for (int index = 0; index < tokens.Count; index++)
        {
            string token = tokens[index];
            int equalsIndex = token.IndexOf('=');
            string option = equalsIndex >= 0 ? token[..equalsIndex] : token;
            string value = equalsIndex >= 0 ? token[(equalsIndex + 1)..] : string.Empty;
            bool usesSeparateValue = equalsIndex < 0 && index + 1 < tokens.Count;

            BlockCheck2PresetFileKind? kind = option.Equals("--hostlist", StringComparison.OrdinalIgnoreCase) ||
                                               option.Equals("--hostlist-exclude", StringComparison.OrdinalIgnoreCase)
                ? BlockCheck2PresetFileKind.SiteList
                : option.Equals("--lua-init", StringComparison.OrdinalIgnoreCase)
                    ? BlockCheck2PresetFileKind.Library
                    : option.Equals("--blob", StringComparison.OrdinalIgnoreCase)
                        ? BlockCheck2PresetFileKind.Payload
                        : null;
            if (kind == null)
            {
                output.Add(token);
                continue;
            }

            if (usesSeparateValue && !tokens[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                value = tokens[++index];
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                output.Add(token);
                continue;
            }

            string prefix = string.Empty;
            string reference = value;
            if (kind == BlockCheck2PresetFileKind.Payload)
            {
                string unquoted = value.Trim().Trim('"', '\'');
                int separatorIndex = unquoted.IndexOf(':');
                if (separatorIndex < 0)
                {
                    output.Add(token);
                    continue;
                }
                prefix = unquoted[..(separatorIndex + 1)];
                reference = unquoted[(separatorIndex + 1)..];
            }

            char sourceMarker = GetSourceMarker(reference);
            string normalizedReference = NormalizeReference(reference);
            FileReplacement replacement = replacements.FirstOrDefault(candidate =>
                candidate.Kind == kind &&
                string.Equals(
                    NormalizeReference(candidate.OriginalReference),
                    normalizedReference,
                    StringComparison.OrdinalIgnoreCase));
            if (replacement == null)
            {
                output.Add(token);
                if (usesSeparateValue)
                {
                    output.Add(value);
                }
                continue;
            }

            string marker = kind == BlockCheck2PresetFileKind.SiteList
                ? string.Empty
                : sourceMarker == '\0'
                    ? kind == BlockCheck2PresetFileKind.Library ? "@" : string.Empty
                    : sourceMarker.ToString();
            string rewrittenValue = prefix + marker + replacement.ReplacementReference;
            output.Add($"{option}=\"{rewrittenValue.Replace("\"", "\\\"")}\"");
        }
        return string.Join(' ', output);
    }

    private static char GetSourceMarker(string reference)
    {
        string value = reference.Trim().Trim('"', '\'');
        if (value.StartsWith("$GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase))
        {
            return '\0';
        }
        return value.Length > 0 && (value[0] == '@' || value[0] == '$')
            ? value[0]
            : '\0';
    }

    private static string NormalizeReference(string reference)
    {
        string value = (reference ?? string.Empty).Trim().Trim('"', '\'');
        if (value.StartsWith('@'))
        {
            value = value[1..].Trim().Trim('"', '\'');
        }
        if (value.StartsWith("$GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase))
        {
            value = value[1..];
        }
        else if (value.StartsWith('$'))
        {
            value = value[1..].Trim().Trim('"', '\'');
        }
        return value.Replace('\\', '/');
    }

    private static string ResolveSourcePath(
        string reference,
        string componentDirectory,
        string userStorage)
    {
        string normalized = NormalizeReference(reference);
        if (normalized.StartsWith("GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase))
        {
            string suffix = normalized["GETCURRENTDIR()".Length..]
                .TrimStart('/', '\\');
            string expanded = Path.GetFullPath(Path.Combine(
                userStorage,
                suffix.Replace('/', Path.DirectorySeparatorChar)));
            return File.Exists(expanded) ? expanded : null;
        }

        string value = Environment.ExpandEnvironmentVariables(normalized)
            .Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathFullyQualified(value))
        {
            string fullPath = Path.GetFullPath(value);
            return File.Exists(fullPath) ? fullPath : null;
        }

        string[] candidates =
        [
            Path.Combine(componentDirectory, value),
            Path.Combine(Directories.CurrentDirectory, value),
            Path.Combine(userStorage, value),
        ];
        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
    }

    private static bool IsPathInside(string path, string root)
    {
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(
                   fullRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string GetUniqueDestinationPath(string directory, string fileName)
    {
        string safeName = CreateAsciiFileName(fileName);
        string candidate = Path.Combine(directory, safeName);
        string stem = Path.GetFileNameWithoutExtension(safeName);
        string extension = Path.GetExtension(safeName);
        int suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{stem}_{suffix++}{extension}");
        }
        return candidate;
    }

    private static string CreateStorageId(string presetName, string userStorage)
    {
        string slug = CreateAsciiSlug(presetName, 48);
        if (slug.Length == 0)
        {
            slug = "preset";
        }

        string baseId = $"{slug}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        string candidate = baseId;
        int suffix = 2;
        while (StorageIdExists(userStorage, candidate))
        {
            candidate = $"{baseId}_{suffix++}";
        }
        return candidate;
    }

    private static bool StorageIdExists(string userStorage, string storageId)
    {
        return Directory.Exists(Path.Combine(
                   userStorage,
                   SharedConstants.LocalUserItemSiteListsFolder,
                   StorageFolderName,
                   storageId)) ||
               Directory.Exists(Path.Combine(
                   userStorage,
                   SharedConstants.LocalUserItemBinsFolder,
                   StorageFolderName,
                   storageId)) ||
               File.Exists(Path.Combine(userStorage, $"{storageId}.json"));
    }

    private static string CreateAsciiFileName(string fileName)
    {
        string sourceName = string.IsNullOrWhiteSpace(fileName)
            ? "dependency.bin"
            : Path.GetFileName(fileName);
        string stem = CreateAsciiSlug(Path.GetFileNameWithoutExtension(sourceName), 120);
        if (stem.Length == 0)
        {
            stem = "dependency";
        }

        string extension = Path.GetExtension(sourceName).Unidecode();
        extension = Regex.Replace(extension, @"[^A-Za-z0-9.]", string.Empty);
        return stem + extension;
    }

    private static string CreateAsciiSlug(string value, int maxLength)
    {
        string slug = (value ?? string.Empty).Unidecode();
        slug = Regex.Replace(slug, @"\s+", "_");
        slug = Regex.Replace(slug, @"[^A-Za-z0-9_-]", "_");
        slug = Regex.Replace(slug, @"_+", "_").Trim('_', '-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('_', '-');
        }
        return slug;
    }

    private static void Cleanup(IEnumerable<string> roots)
    {
        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
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
                // pass
            }
        }
    }

    private sealed record FileReplacement(
        BlockCheck2PresetFileKind Kind,
        string OriginalReference,
        string ReplacementReference);

    private sealed record PresetFileSnapshot(
        string Path,
        BlockCheck2PresetFileKind Kind);

    private sealed record PresetSnapshot(
        string EffectiveArguments,
        bool CanUseConfig,
        IReadOnlyList<PresetFileSnapshot> Files);
}

public sealed class BlockCheck2PresetStorageResult
{
    public bool Success { get; init; }
    public string PresetName { get; init; } = string.Empty;
    public string ConfigFileName { get; init; } = string.Empty;
    public int CopiedFileCount { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorDetails { get; init; } = string.Empty;

    public static BlockCheck2PresetStorageResult Failed(
        string errorCode,
        string errorDetails = null) => new()
        {
            ErrorCode = errorCode,
            ErrorDetails = errorDetails ?? string.Empty,
        };
}
