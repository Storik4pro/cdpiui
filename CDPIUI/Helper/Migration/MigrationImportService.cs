using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Features;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CDPIUI.Helper.Migration;

internal sealed class MigrationImportService
{
    private const long MaximumPresetSize = 8L * 1024L * 1024L;
    private readonly MigrationArchiveInspectionService inspectionService = new();

    public static bool IsAlreadyCompleted(VerifiedMigrationPackage package)
    {
        try
        {
            string marker = Path.Combine(
                Directories.DataDirectory,
                "Migration", "GoodbyeDPIUI", "Completed",
                package.Request.MigrationId.ToString("N") + ".json");
            if (!File.Exists(marker) ||
                !Directory.Exists(Path.Combine(
                    Directories.StoreItemsDirectory,
                    SharedConstants.MigratedGoodbyeDpiUiStoreItemId)) ||
                !DatabaseHelper.Instance.IsItemInstalled(SharedConstants.MigratedGoodbyeDpiUiStoreItemId))
                return false;
            JObject data = JObject.Parse(File.ReadAllText(marker));
            return data["migrationId"]?.Value<string>() == package.Request.MigrationId.ToString("D") &&
                data["archiveSha256"]?.Value<string>() == package.Request.ArchiveSha256;
        }
        catch
        {
            return false;
        }
    }

    public MigrationImportResult Import(
        VerifiedMigrationPackage suppliedPackage,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(suppliedPackage);
        progress?.Report(2);
        VerifiedMigrationPackage package = inspectionService.InspectAndStage(
            suppliedPackage.Request, cancellationToken);
        progress?.Report(12);

        string storeRoot = Path.GetFullPath(Directories.StoreItemsDirectory);
        Directory.CreateDirectory(storeRoot);
        string targetDirectory = Path.Combine(storeRoot, SharedConstants.MigratedGoodbyeDpiUiStoreItemId);
        string stageDirectory = Path.Combine(
            storeRoot,
            $"{SharedConstants.MigratedGoodbyeDpiUiStoreItemId}.importing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stageDirectory);

        string? backupDirectory = null;
        bool stageCommitted = false;
        List<MigrationImportIssue> issues = [];
        try
        {
            ImportBuildResult build = BuildCollection(
                package, stageDirectory, issues, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(72);

            backupDirectory = CommitCollection(stageDirectory, targetDirectory, package);
            stageCommitted = true;
            progress?.Report(82);

            int importedSettings = ApplySettings(
                package.Data, build.PresetFilesBySource, issues, cancellationToken);
            progress?.Report(94);
            WriteReport(targetDirectory, package, build, importedSettings, backupDirectory, issues);
            WriteCompletionMarker(package, build, importedSettings, issues);
            progress?.Report(100);

            return new MigrationImportResult
            {
                ImportedPresetCount = build.PresetCount,
                ImportedResourceCount = build.ResourceCount,
                ImportedSettingCount = importedSettings,
                ReviewRequiredCount = build.ReviewRequiredCount,
                BackupDirectory = backupDirectory,
                Issues = issues
            };
        }
        finally
        {
            if (!stageCommitted && Directory.Exists(stageDirectory))
                DeleteKnownStage(stageDirectory, storeRoot);
        }
    }

    private static ImportBuildResult BuildCollection(
        VerifiedMigrationPackage package,
        string stageDirectory,
        ICollection<MigrationImportIssue> issues,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        string listDirectory = Path.Combine(stageDirectory, SharedConstants.LocalUserItemSiteListsFolder);
        string binDirectory = Path.Combine(stageDirectory, SharedConstants.LocalUserItemBinsFolder);
        string localeDirectory = Path.Combine(stageDirectory, SharedConstants.LocalUserItemLocFolder);
        string metadataDirectory = Path.Combine(stageDirectory, "Metadata");
        Directory.CreateDirectory(listDirectory);
        Directory.CreateDirectory(binDirectory);
        Directory.CreateDirectory(localeDirectory);
        Directory.CreateDirectory(metadataDirectory);

        File.WriteAllText(Path.Combine(stageDirectory, "init.json"),
            JsonConvert.SerializeObject(new
            {
                toggleListAvailable = Array.Empty<string>(),
                localized_strings_directory = new Dictionary<string, string>
                {
                    ["EN"] = $"{SharedConstants.LocalUserItemLocFolder}/strings.json"
                }
            }));
        File.WriteAllText(Path.Combine(localeDirectory, "strings.json"), "{}");

        using FileStream archiveStream = new(
            package.StagedArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.SequentialScan);
        using ZipArchive archive = new(archiveStream, ZipArchiveMode.Read);
        Dictionary<string, ZipArchiveEntry> entries = archive.Entries.ToDictionary(
            item => item.FullName, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, StoredResource> storedResources = new(StringComparer.Ordinal);
        int resourceIndex = 0;
        int importedResourceCount = 0;
        foreach (MigrationResource resource in package.Data.Resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                bool isBinary = resource.Kind == "binary";
                string extension = isBinary ? ".bin" : ".txt";
                string fileName = resource.Id + extension;
                string destination = Path.Combine(isBinary ? binDirectory : listDirectory, fileName);
                CopyVerifiedEntry(entries[resource.PayloadPath!], destination, resource.Size, resource.Sha256!, cancellationToken);
                string storedReference = string.Join('/',
                    "$GETCURRENTDIR()",
                    isBinary ? SharedConstants.LocalUserItemBinsFolder : SharedConstants.LocalUserItemSiteListsFolder,
                    fileName);
                storedResources.Add(resource.Id!, new StoredResource(destination, storedReference));
                importedResourceCount++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                AddIssue(issues, "resource", resource.SourceRelativePath, exception);
            }
            finally
            {
                resourceIndex++;
                progress?.Report(12 + 25d * resourceIndex / Math.Max(1, package.Data.Resources.Count));
            }
        }

        Dictionary<string, string> presetFilesBySource = new(StringComparer.OrdinalIgnoreCase);
        int presetIndex = 0;
        int importedPresetCount = 0;
        int reviewCount = 0;
        foreach (MigrationPreset preset in package.Data.Presets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                JObject config = CreateConfig(entries[preset.PayloadPath!], preset);
                RewriteResourceReferences(config, preset, storedResources);
                MigrationComponentRequirement component = GoodbyeDpiComponentMapper.Map(preset);
                config["meta"] = "pUC:v1.0";
                config["target"] = new JArray(component.ConfigTargetId, "%CURRENT%");
                config["name"] = preset.Name;
                config["jparams"] ??= new JObject();
                config["variables"] ??= new JArray();
                config["startup_string"] ??= string.Empty;
                RemoveRuntimeOnlyProperties(config);

                string fileName = preset.Id + ".json";
                File.WriteAllText(
                    Path.Combine(stageDirectory, fileName),
                    config.ToString(Formatting.None),
                    new UTF8Encoding(false));
                presetFilesBySource[preset.SourceRelativePath!] = fileName;
                presetFilesBySource[Path.GetFileNameWithoutExtension(preset.SourceRelativePath)] = fileName;
                presetFilesBySource[preset.Name!] = fileName;
                importedPresetCount++;
                if (preset.RequiresReview || preset.Resources.Any(link => !link.IsResolved))
                    reviewCount++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                AddIssue(issues, "preset", preset.SourceRelativePath ?? preset.Name, exception);
            }
            finally
            {
                presetIndex++;
                progress?.Report(37 + 30d * presetIndex / Math.Max(1, package.Data.Presets.Count));
            }
        }

        return new ImportBuildResult(
            importedPresetCount,
            importedResourceCount,
            reviewCount,
            presetFilesBySource);
    }

    private static JObject CreateConfig(ZipArchiveEntry entry, MigrationPreset preset)
    {
        if (entry.Length < 0 || entry.Length > MaximumPresetSize)
            throw new InvalidDataException($"Preset '{preset.Name}' exceeds the import size limit.");
        string json;
        using (StreamReader reader = new(entry.Open(), new UTF8Encoding(false, true), true))
            json = reader.ReadToEnd();

        if (preset.ParameterMode == "custom-parameters")
        {
            return new JObject
            {
                ["startup_string"] = preset.CustomParameters ?? string.Empty
            };
        }

        try
        {
            JObject source = JObject.Parse(json);
            if (source["custom_parameters"]?.Type == JTokenType.String)
                source["startup_string"] = source["custom_parameters"]!.Value<string>();
            source.Remove("custom_parameters");
            return source;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Preset '{preset.Name}' is not valid JSON.", exception);
        }
    }

    private static void RewriteResourceReferences(
        JObject config,
        MigrationPreset preset,
        IReadOnlyDictionary<string, StoredResource> resources)
    {
        List<(string Source, string Destination)> replacements = [];
        foreach (MigrationPresetResourceLink link in preset.Resources)
        {
            if (string.IsNullOrWhiteSpace(link.OriginalReference))
                continue;
            if (!link.IsResolved)
            {
                replacements.Add((link.OriginalReference, QuotePath(link.OriginalReference)));
                continue;
            }
            if (link.ResourceId == null ||
                !resources.TryGetValue(link.ResourceId, out StoredResource? resource))
            {
                throw new InvalidDataException(
                    $"Preset '{preset.Name}' depends on a resource that could not be imported.");
            }
            replacements.Add((link.OriginalReference, QuotePath(resource.Reference)));
        }
        RewriteToken(config, replacements);
    }

    private static void RewriteToken(
        JToken token,
        IReadOnlyCollection<(string Source, string Destination)> replacements)
    {
        if (token is JValue { Type: JTokenType.String } value)
        {
            string text = value.Value<string>() ?? string.Empty;
            foreach ((string source, string destination) in replacements)
            {
                string alternate = source.Contains('\\') ? source.Replace('\\', '/') : source.Replace('/', '\\');
                foreach (string candidate in new[] { source, alternate }.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    text = ReplaceOrdinalIgnoreCase(text, $"\"{candidate}\"", destination);
                    text = ReplaceOrdinalIgnoreCase(text, $"'{candidate}'", destination);
                    text = ReplaceOrdinalIgnoreCase(text, candidate, destination);
                }
            }
            value.Value = text;
            return;
        }

        foreach (JToken child in token.Children().ToList())
            RewriteToken(child, replacements);
    }

    private static string ReplaceOrdinalIgnoreCase(string input, string source, string destination)
    {
        if (string.IsNullOrEmpty(source))
            return input;
        return Regex.Replace(
            input,
            Regex.Escape(source),
            _ => destination,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    private static string QuotePath(string path)
    {
        return "\"" + path.Trim().Trim('"') + "\"";
    }

    private static void RemoveRuntimeOnlyProperties(JObject config)
    {
        foreach (string property in new[]
        {
            "file_name", "packId", "not_converted_name", "toggle_lists",
            "IsLegacy", "MarkAsRemoved", "custom_parameters"
        })
        {
            config.Remove(property);
        }
    }

    private static void CopyVerifiedEntry(
        ZipArchiveEntry entry,
        string destination,
        long expectedSize,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        using Stream source = entry.Open();
        using FileStream target = new(
            destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.WriteThrough);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += read;
            if (total > expectedSize)
                throw new InvalidDataException($"Resource '{entry.FullName}' exceeds its declared size.");
            target.Write(buffer, 0, read);
            hash.AppendData(buffer, 0, read);
        }
        target.Flush(flushToDisk: true);
        string actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (total != expectedSize || actualHash != expectedHash)
            throw new InvalidDataException($"Resource '{entry.FullName}' failed its final integrity check.");
    }

    private static string? CommitCollection(
        string stageDirectory,
        string targetDirectory,
        VerifiedMigrationPackage package)
    {
        string? backupDirectory = null;
        if (Directory.Exists(targetDirectory))
        {
            if ((File.GetAttributes(targetDirectory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The existing migrated preset collection is a reparse point.");
            string backupRoot = Path.Combine(
                Directories.DataDirectory, "Migration", "GoodbyeDPIUI", "Backups");
            Directory.CreateDirectory(backupRoot);
            backupDirectory = Path.Combine(
                backupRoot,
                $"{package.Request.MigrationId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}");
            Directory.Move(targetDirectory, backupDirectory);
        }

        try
        {
            Directory.Move(stageDirectory, targetDirectory);
            DatabaseStoreItem collection = new()
            {
                Id = SharedConstants.MigratedGoodbyeDpiUiStoreItemId,
                Type = "configlist",
                Directory = targetDirectory,
                VersionControlType = "local",
                CurrentVersion = ApplicationInfo.Version,
                IconPath = "$STATICIMAGE(Store/empty.png)",
                Name = "Migrated from GoodbyeDPI-UI",
                ShortName = "Перенесенные из GoodbyeDPI-UI",
                Developer = "GDPIUI-Updater",
                BackgroudColor = string.Empty
            };
            if (!DatabaseHelper.Instance.AddOrUpdateItem(collection))
                throw new InvalidOperationException("CDPIUI could not register the migrated preset collection.");
            return backupDirectory;
        }
        catch
        {
            string failedRoot = Path.Combine(
                Directories.DataDirectory, "Migration", "GoodbyeDPIUI", "FailedImports");
            Directory.CreateDirectory(failedRoot);
            if (Directory.Exists(targetDirectory))
                Directory.Move(targetDirectory, Path.Combine(failedRoot, $"{package.Request.MigrationId:N}-{Guid.NewGuid():N}"));
            if (backupDirectory != null && Directory.Exists(backupDirectory) && !Directory.Exists(targetDirectory))
                Directory.Move(backupDirectory, targetDirectory);
            throw;
        }
    }

    private static int ApplySettings(
        MigrationDataDocument data,
        IReadOnlyDictionary<string, string> presetFilesBySource,
        ICollection<MigrationImportIssue> issues,
        CancellationToken cancellationToken)
    {
        Dictionary<string, MigrationSetting> candidates = data.Settings
            .Where(setting => setting.ImportPolicy == "candidate" &&
                !string.IsNullOrWhiteSpace(setting.SemanticKey) &&
                !string.Equals(
                    Path.GetFileName(setting.SourceRelativePath),
                    "FluentUI-Gallery.ini",
                    StringComparison.OrdinalIgnoreCase))
            .GroupBy(setting => setting.SemanticKey!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        int applied = 0;

        foreach ((string semanticKey, MigrationSetting setting) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                switch (semanticKey)
                {
                    case "ui.theme":
                        string theme = (setting.Value ?? string.Empty).Trim().ToLowerInvariant() switch
                        {
                            "dark" or "1" => "Dark",
                            "light" or "0" => "Light",
                            _ => "Default"
                        };
                        SettingsManager.Instance.SetValue("APPEARANCE", "Theme", theme);
                        applied++;
                        break;
                    case "updates.source":
                        string source = (setting.Value ?? string.Empty).Contains(
                            "gitlab", StringComparison.OrdinalIgnoreCase) ? "GitLab" : "GitHub";
                        SettingsManager.Instance.SetValue("STORE", "versionControlType", source);
                        applied++;
                        break;
                    case "startup.start-minimized":
                    case "startup.minimize-to-tray":
                        if (TryParseBoolean(setting.Value, out bool minimized))
                        {
                            SettingsManager.Instance.SetValue("APPEARANCE", "hideToTrayOnStartup", minimized);
                            applied++;
                        }
                        break;
                    case "startup.autostart":
                        if (TryParseBoolean(setting.Value, out bool autostart))
                        {
                            if (autostart)
                                ApplicationAutorunManager.AddToAutorun();
                            else
                                ApplicationAutorunManager.RemoveFromAutorun();
                            applied++;
                        }
                        break;
                }
            }
            catch (Exception exception)
            {
                AddIssue(issues, "setting", semanticKey, exception);
            }
        }

        try
        {
            MigrationPreset? selectedPreset = null;
            if (candidates.TryGetValue("runtime.active-preset", out MigrationSetting? presetSetting) &&
                !string.IsNullOrWhiteSpace(presetSetting.Value))
            {
                string value = presetSetting.Value.Trim();
                selectedPreset = data.Presets.FirstOrDefault(preset =>
                    string.Equals(preset.Id, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(preset.Name, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(preset.SourceRelativePath, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileNameWithoutExtension(preset.SourceRelativePath), value, StringComparison.OrdinalIgnoreCase));
            }

            MigrationComponentRequirement? activeComponent = selectedPreset == null
                ? MapLegacyComponent(candidates.GetValueOrDefault("runtime.active-component")?.Value)
                : GoodbyeDpiComponentMapper.Map(selectedPreset);
            if (activeComponent != null)
            {
                SettingsManager.Instance.SetValue("COMPONENTS", "nowUsed", activeComponent.InstallItemId);
                applied++;
            }
            if (selectedPreset != null && activeComponent != null &&
                presetFilesBySource.TryGetValue(selectedPreset.SourceRelativePath!, out string? configFile))
            {
                SettingsManager.Instance.SetValue(
                    ["CONFIGS", activeComponent.InstallItemId], "configId",
                    SharedConstants.MigratedGoodbyeDpiUiStoreItemId);
                SettingsManager.Instance.SetValue(
                    ["CONFIGS", activeComponent.InstallItemId], "configFile", configFile);
                applied++;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AddIssue(issues, "setting", "runtime.selection", exception);
        }
        return applied;
    }

    private static MigrationComponentRequirement? MapLegacyComponent(string? value)
    {
        string source = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (source.Length == 0)
            return null;
        MigrationPreset placeholder = new()
        {
            Component = source switch
            {
                var item when item.Contains("zapret") => "zapret",
                var item when item.Contains("byedpi") => "byedpi",
                var item when item.Contains("spoof") => "spoofdpi",
                var item when item.Contains("nodpi") => "nodpi",
                _ => "goodbyedpi"
            }
        };
        return GoodbyeDpiComponentMapper.Map(placeholder);
    }

    private static bool TryParseBoolean(string? value, out bool result)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is "true" or "1" or "yes" or "on")
        {
            result = true;
            return true;
        }
        if (normalized is "false" or "0" or "no" or "off")
        {
            result = false;
            return true;
        }
        result = false;
        return false;
    }

    private static void WriteReport(
        string targetDirectory,
        VerifiedMigrationPackage package,
        ImportBuildResult build,
        int importedSettings,
        string? backupDirectory,
        IReadOnlyCollection<MigrationImportIssue> issues)
    {
        try
        {
            string reportPath = Path.Combine(targetDirectory, "Metadata", "migration-report.json");
            File.WriteAllText(reportPath, JsonConvert.SerializeObject(new
            {
                schemaVersion = 1,
                migrationId = package.Request.MigrationId.ToString("D"),
                archiveSha256 = package.Request.ArchiveSha256,
                completedUtc = DateTimeOffset.UtcNow.ToString("O"),
                importedPresets = build.PresetCount,
                importedResources = build.ResourceCount,
                importedSettings,
                reviewRequired = build.ReviewRequiredCount,
                issueCount = issues.Count,
                issues,
                previousImportBackup = backupDirectory,
                sourceDiagnostics = package.Data.Diagnostics
            }, Formatting.Indented));
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(MigrationImportService),
                $"Cannot write the migration report: {exception.Message}");
        }
    }

    private static void WriteCompletionMarker(
        VerifiedMigrationPackage package,
        ImportBuildResult build,
        int importedSettings,
        IReadOnlyCollection<MigrationImportIssue> issues)
    {
        if (issues.Count > 0)
            return;
        string directory = Path.Combine(
            Directories.DataDirectory, "Migration", "GoodbyeDPIUI", "Completed");
        Directory.CreateDirectory(directory);
        string marker = Path.Combine(directory, package.Request.MigrationId.ToString("N") + ".json");
        string temporary = marker + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, JsonConvert.SerializeObject(new
        {
            schemaVersion = 1,
            migrationId = package.Request.MigrationId.ToString("D"),
            archiveSha256 = package.Request.ArchiveSha256,
            completedUtc = DateTimeOffset.UtcNow.ToString("O"),
            importedPresets = build.PresetCount,
            importedResources = build.ResourceCount,
            importedSettings,
            issueCount = issues.Count,
            issues
        }));
        File.Move(temporary, marker, overwrite: true);
    }

    private static void AddIssue(
        ICollection<MigrationImportIssue> issues,
        string kind,
        string? source,
        Exception exception)
    {
        string normalizedSource = string.IsNullOrWhiteSpace(source) ? "(unknown)" : source;
        issues.Add(new MigrationImportIssue(kind, normalizedSource, exception.Message));
        Logger.Instance.CreateWarningLog(
            nameof(MigrationImportService),
            $"Cannot import {kind} '{normalizedSource}': {exception}");
    }

    private static void DeleteKnownStage(string stageDirectory, string storeRoot)
    {
        string fullStage = Path.GetFullPath(stageDirectory);
        string prefix = Path.GetFullPath(storeRoot).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullStage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullStage).StartsWith(
                SharedConstants.MigratedGoodbyeDpiUiStoreItemId + ".importing-",
                StringComparison.Ordinal) ||
            (File.GetAttributes(fullStage) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing to remove an unexpected import staging directory.");
        Directory.Delete(fullStage, recursive: true);
    }

    private sealed record StoredResource(string Path, string Reference);
    private sealed record ImportBuildResult(
        int PresetCount,
        int ResourceCount,
        int ReviewRequiredCount,
        IReadOnlyDictionary<string, string> PresetFilesBySource);
}
