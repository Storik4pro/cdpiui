using CDPIUI.Core.Data;
using CDPIUI.Shared.Migration;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;

namespace CDPIUI.Helper.Migration;

internal sealed class MigrationArchiveInspectionService
{
    private const string ManifestEntryName = "manifest.json";
    private const string MigrationDataEntryName = "migration-data.json";
    private const long MaximumArchiveSize = 1024L * 1024L * 1024L;
    private const long MaximumExpandedSize = 1024L * 1024L * 1024L;
    private const long MaximumMetadataSize = 32L * 1024L * 1024L;
    private const int MaximumEntryCount = 10000;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        MaxDepth = 64
    };

    public VerifiedMigrationPackage InspectAndStage(
        GoodbyeDpiMigrationActivationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string stagedArchivePath = StageArchive(request, cancellationToken);

        using FileStream stream = new(
            stagedArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.SequentialScan);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);

        Dictionary<string, ZipArchiveEntry> entries = ValidateEntryEnvelope(archive);
        byte[] manifestBytes = ReadEntry(entries[ManifestEntryName], MaximumMetadataSize);
        ValidateManifestJson(manifestBytes);
        MigrationArchiveManifest manifest = Deserialize<MigrationArchiveManifest>(manifestBytes);
        ValidateManifest(manifest, request, entries);

        ZipArchiveEntry dataEntry = entries[MigrationDataEntryName];
        if (dataEntry.Length != manifest.MigrationDataSize)
            throw Invalid("The migration-data size differs from the signed manifest.");
        byte[] dataBytes = ReadEntry(dataEntry, MaximumMetadataSize);
        if (!HashEquals(dataBytes, manifest.MigrationDataSha256))
            throw Invalid("The migration-data SHA-256 hash differs from the manifest.");

        ValidateMigrationDataJson(dataBytes);
        MigrationDataDocument data = Deserialize<MigrationDataDocument>(dataBytes);
        ValidateMigrationData(data, manifest, request);
        VerifyPayloads(entries, manifest, cancellationToken);

        List<MigrationSetting> componentSettings = data.Settings
            .FindAll(group => group.Section == "COMPONENTS");

        IReadOnlyList<MigrationComponentRequirement> components = data.Presets
            .FindAll(item => componentSettings.Find(setting => setting.Key.Equals(item.Component, StringComparison.CurrentCultureIgnoreCase))?.Value == "True")
            .Select(GoodbyeDpiComponentMapper.Map)
            .GroupBy(item => item.InstallItemId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.SourceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        

        return new VerifiedMigrationPackage
        {
            Request = request,
            StagedArchivePath = stagedArchivePath,
            Manifest = manifest,
            Data = data,
            Components = components
        };
    }

    private static string StageArchive(
        GoodbyeDpiMigrationActivationRequest request,
        CancellationToken cancellationToken)
    {
        string incomingDirectory = Path.Combine(
            Directories.DataDirectory, "Migration", "GoodbyeDPIUI", "Incoming");
        Directory.CreateDirectory(incomingDirectory);
        string stagedPath = Path.Combine(
            incomingDirectory,
            $"{request.MigrationId:N}-{request.ArchiveSha256[..12]}.zip");

        if (File.Exists(stagedPath))
        {
            if (!HashFile(stagedPath, cancellationToken).Equals(
                    request.ArchiveSha256, StringComparison.Ordinal))
                throw Invalid("The protected staged archive was modified.");
            return stagedPath;
        }

        if (!File.Exists(request.ArchivePath))
            throw new FileNotFoundException("The migration archive was not found.", request.ArchivePath);
        FileInfo sourceInfo = new(request.ArchivePath);
        if (sourceInfo.Length <= 0 || sourceInfo.Length > MaximumArchiveSize ||
            (sourceInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            throw Invalid("The migration archive has an unsafe type or size.");

        string temporaryPath = Path.Combine(incomingDirectory, $"incoming-{Guid.NewGuid():N}.tmp");
        try
        {
            string actualHash;
            // Windows cannot move the temporary file while the FileShare.None
            // destination handle is open, so keep the copy streams in a nested scope.
            using (FileStream source = new(
                       request.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                       81920, FileOptions.SequentialScan))
            using (FileStream destination = new(
                       temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       81920, FileOptions.WriteThrough))
            using (IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = new byte[81920];
                long copied = 0;
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    copied += read;
                    if (copied > MaximumArchiveSize)
                        throw Invalid("The migration archive exceeds the size limit.");
                    destination.Write(buffer, 0, read);
                    hash.AppendData(buffer, 0, read);
                }
                destination.Flush(flushToDisk: true);
                actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }

            if (!actualHash.Equals(request.ArchiveSha256, StringComparison.Ordinal))
                throw Invalid("The migration archive SHA-256 hash is invalid.");

            try
            {
                File.Move(temporaryPath, stagedPath);
            }
            catch (IOException) when (File.Exists(stagedPath))
            {
                if (!HashFile(stagedPath, cancellationToken).Equals(
                        request.ArchiveSha256, StringComparison.Ordinal))
                    throw Invalid("The protected staged archive was modified.");
            }
            return stagedPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static Dictionary<string, ZipArchiveEntry> ValidateEntryEnvelope(ZipArchive archive)
    {
        if (archive.Entries.Count is < 2 or > MaximumEntryCount)
            throw Invalid("The migration archive has an invalid entry count.");

        Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
        long expandedSize = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            EnsureSafePath(entry.FullName);
            if (entry.FullName.EndsWith('/') || entry.Length < 0 || !entries.TryAdd(entry.FullName, entry))
                throw Invalid("The migration archive contains a directory or duplicate entry.");
            expandedSize = checked(expandedSize + entry.Length);
            if (expandedSize > MaximumExpandedSize)
                throw Invalid("The expanded migration archive exceeds the size limit.");
        }
        if (!entries.ContainsKey(ManifestEntryName) || !entries.ContainsKey(MigrationDataEntryName))
            throw Invalid("The migration archive metadata is incomplete.");
        return entries;
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, long maximumSize)
    {
        if (entry.Length < 0 || entry.Length > maximumSize)
            throw Invalid($"The archive entry '{entry.FullName}' exceeds the size limit.");
        using Stream source = entry.Open();
        using MemoryStream destination = new((int)entry.Length);
        source.CopyTo(destination);
        if (destination.Length != entry.Length)
            throw Invalid($"The archive entry '{entry.FullName}' is truncated.");
        return destination.ToArray();
    }

    private static T Deserialize<T>(byte[] bytes) where T : class =>
        JsonSerializer.Deserialize<T>(bytes, SerializerOptions) ??
        throw Invalid("The migration metadata is empty.");

    private static void ValidateManifestJson(byte[] bytes)
    {
        using JsonDocument document = ParseJson(bytes);
        JsonElement root = document.RootElement;
        ValidateObject(root, "manifest",
            ["SchemaVersion", "PackageKind", "MigrationId", "CreatedUtc", "SourceProduct",
             "TargetProduct", "SourceRoot", "PresetCollectionNameRu", "PresetCollectionNameEn",
             "Selection", "MigrationDataPath", "MigrationDataSchemaVersion", "MigrationDataSize",
             "MigrationDataSha256", "Items", "Diagnostics"],
            ["SchemaVersion", "PackageKind", "MigrationId", "CreatedUtc", "SourceProduct",
             "TargetProduct", "SourceRoot", "PresetCollectionNameRu", "PresetCollectionNameEn",
             "Selection", "MigrationDataPath", "MigrationDataSchemaVersion", "MigrationDataSize",
             "MigrationDataSha256", "Items", "Diagnostics"]);
        ValidateObject(root.GetProperty("Selection"), "manifest selection",
            ["IncludePresets", "IncludeApplicationSettings", "IncludeSiteLists"],
            ["IncludePresets", "IncludeApplicationSettings", "IncludeSiteLists"]);
        foreach (JsonElement item in RequireArray(root, "Items"))
            ValidateObject(item, "manifest item",
                ["Kind", "Component", "OriginalRelativePath", "ArchivePath", "Size", "Sha256",
                 "IsPresetDependency", "ReferencedBy"],
                ["Kind", "Component", "OriginalRelativePath", "ArchivePath", "Size", "Sha256",
                 "IsPresetDependency", "ReferencedBy"]);
        foreach (JsonElement diagnostic in RequireArray(root, "Diagnostics"))
            ValidateDiagnosticObject(diagnostic);
    }

    private static void ValidateMigrationDataJson(byte[] bytes)
    {
        using JsonDocument document = ParseJson(bytes);
        JsonElement root = document.RootElement;
        ValidateObject(root, "migration data",
            ["SchemaId", "SchemaVersion", "MigrationId", "PackageKind", "CreatedUtc", "Source",
             "PresetCollection", "Selection", "Presets", "Settings", "Resources", "Diagnostics"],
            ["SchemaId", "SchemaVersion", "MigrationId", "PackageKind", "CreatedUtc", "Source",
             "PresetCollection", "Selection", "Presets", "Settings", "Resources", "Diagnostics"]);
        ValidateObject(root.GetProperty("Source"), "source", ["Product", "FolderName"], ["Product", "FolderName"]);
        ValidateObject(root.GetProperty("PresetCollection"), "preset collection",
            ["Id", "NameRu", "NameEn"], ["Id", "NameRu", "NameEn"]);
        ValidateObject(root.GetProperty("Selection"), "selection",
            ["IncludePresets", "IncludeApplicationSettings", "IncludeSiteLists"],
            ["IncludePresets", "IncludeApplicationSettings", "IncludeSiteLists"]);

        foreach (JsonElement preset in RequireArray(root, "Presets"))
        {
            ValidateObject(preset, "preset",
                ["Id", "CollectionId", "Component", "Name", "SourceRelativePath", "PayloadPath",
                 "SourceSha256", "SourceFormat", "ParameterMode", "IsUserPreset", "RequiresReview", "Resources"],
                ["Id", "CollectionId", "Component", "Name", "SourceRelativePath", "PayloadPath",
                 "SourceSha256", "SourceFormat", "ParameterMode", "CustomParameters", "IsUserPreset",
                 "RequiresReview", "Resources"]);
            foreach (JsonElement link in RequireArray(preset, "Resources"))
                ValidateObject(link, "preset resource link",
                    ["OriginalReference", "Kind", "IsResolved"],
                    ["OriginalReference", "Kind", "IsResolved", "ResourceId", "ResolvedRelativePath", "DiagnosticCode"]);
        }
        foreach (JsonElement setting in RequireArray(root, "Settings"))
            ValidateObject(setting, "setting",
                ["Id", "SourceRelativePath", "PayloadPath", "SourceFormat", "Ordinal", "Key", "ValueType", "ImportPolicy"],
                ["Id", "SourceRelativePath", "PayloadPath", "SourceFormat", "Ordinal", "Section", "Key", "JsonPath",
                 "Value", "ValueType", "SemanticKey", "ImportPolicy"]);
        foreach (JsonElement resource in RequireArray(root, "Resources"))
            ValidateObject(resource, "resource",
                ["Id", "Kind", "Component", "SourceRelativePath", "PayloadPath", "Sha256", "Size",
                 "IsPresetDependency", "ReferencedByPresetIds"],
                ["Id", "Kind", "Component", "SourceRelativePath", "PayloadPath", "Sha256", "Size",
                 "IsPresetDependency", "ReferencedByPresetIds"]);
        foreach (JsonElement diagnostic in RequireArray(root, "Diagnostics"))
            ValidateDiagnosticObject(diagnostic);
    }

    private static JsonDocument ParseJson(byte[] bytes)
    {
        try
        {
            return JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
        }
        catch (JsonException exception)
        {
            throw Invalid($"The migration metadata is invalid JSON: {exception.Message}");
        }
    }

    private static void ValidateDiagnosticObject(JsonElement value) =>
        ValidateObject(value, "diagnostic", ["Severity", "Code", "Message"],
            ["Severity", "Code", "Message", "RelatedPath"]);

    private static JsonElement.ArrayEnumerator RequireArray(JsonElement owner, string propertyName)
    {
        JsonElement value = owner.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Array)
            throw Invalid($"'{propertyName}' must be an array.");
        return value.EnumerateArray();
    }

    private static void ValidateObject(
        JsonElement value,
        string context,
        IReadOnlyCollection<string> required,
        IReadOnlyCollection<string> allowed)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Invalid($"The {context} must be an object.");
        HashSet<string> allowedSet = new(allowed, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!allowedSet.Contains(property.Name) || !seen.Add(property.Name))
                throw Invalid($"The {context} contains an unexpected or duplicate property '{property.Name}'.");
        }
        if (required.Any(property => !seen.Contains(property)))
            throw Invalid($"The {context} is missing a required property.");
    }

    private static void ValidateManifest(
        MigrationArchiveManifest manifest,
        GoodbyeDpiMigrationActivationRequest request,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        if (manifest.SchemaVersion != 1 || manifest.MigrationDataSchemaVersion != 1 ||
            manifest.PackageKind != "migration" || manifest.SourceProduct != "GoodbyeDPI-UI" ||
            manifest.TargetProduct != "CDPIUI" || manifest.MigrationDataPath != MigrationDataEntryName ||
            manifest.PresetCollectionNameRu != "Перенесенные из GoodbyeDPI-UI" ||
            manifest.PresetCollectionNameEn != "Migrated from GoodbyeDPI-UI" ||
            !Guid.TryParse(manifest.MigrationId, out Guid migrationId) || migrationId != request.MigrationId ||
            !DateTimeOffset.TryParse(manifest.CreatedUtc, out _) || manifest.Selection == null ||
            manifest.MigrationDataSize < 0 || manifest.MigrationDataSize > MaximumMetadataSize)
            throw Invalid("The migration manifest identity or version is invalid.");
        EnsureHash(manifest.MigrationDataSha256, "migration-data");
        EnsureText(manifest.SourceRoot, "source root", 512);

        HashSet<string> expectedEntries = new(StringComparer.OrdinalIgnoreCase)
        {
            ManifestEntryName,
            MigrationDataEntryName
        };
        HashSet<string> sourcePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (MigrationArchiveItem item in manifest.Items)
        {
            if (item.Kind is not ("Preset" or "ApplicationSetting" or "SiteList" or "BinaryResource") ||
                item.Size < 0 || item.Size > MaximumExpandedSize)
                throw Invalid("A manifest item has an invalid kind or size.");
            EnsureText(item.Component, "component", 128);
            EnsureSafePath(item.OriginalRelativePath);
            EnsureSafePath(item.ArchivePath);
            if (!item.ArchivePath!.StartsWith("payload/", StringComparison.Ordinal) ||
                !expectedEntries.Add(item.ArchivePath) || !sourcePaths.Add(item.OriginalRelativePath!))
                throw Invalid("A manifest item path is duplicated or outside payload/.");
            EnsureHash(item.Sha256, item.ArchivePath);
            foreach (string reference in item.ReferencedBy)
                EnsureSafePath(reference);
        }

        if (!expectedEntries.SetEquals(entries.Keys))
            throw Invalid("The migration archive contains files not declared by the manifest.");
        ValidateDiagnostics(manifest.Diagnostics);
    }

    private static void ValidateMigrationData(
        MigrationDataDocument data,
        MigrationArchiveManifest manifest,
        GoodbyeDpiMigrationActivationRequest request)
    {
        if (data.SchemaId != "gdpiui-migration-data" || data.SchemaVersion != 1 ||
            data.PackageKind != "migration" || data.Source?.Product != "GoodbyeDPI-UI" ||
            data.PresetCollection?.Id != "migrated-from-goodbyedpi-ui" ||
            data.PresetCollection.NameRu != "Перенесенные из GoodbyeDPI-UI" ||
            data.PresetCollection.NameEn != "Migrated from GoodbyeDPI-UI" ||
            !Guid.TryParse(data.MigrationId, out Guid migrationId) || migrationId != request.MigrationId ||
            data.MigrationId != manifest.MigrationId || data.CreatedUtc != manifest.CreatedUtc ||
            data.Selection == null || manifest.Selection == null ||
            data.Selection.IncludePresets != manifest.Selection.IncludePresets ||
            data.Selection.IncludeApplicationSettings != manifest.Selection.IncludeApplicationSettings ||
            data.Selection.IncludeSiteLists != manifest.Selection.IncludeSiteLists)
            throw Invalid("The migration-data identity does not match the manifest.");

        Dictionary<string, MigrationArchiveItem> manifestItems = manifest.Items.ToDictionary(
            item => item.ArchivePath!, StringComparer.OrdinalIgnoreCase);
        HashSet<string> presetIds = new(StringComparer.Ordinal);
        HashSet<string> settingIds = new(StringComparer.Ordinal);
        HashSet<string> resourceIds = new(StringComparer.Ordinal);
        Dictionary<string, MigrationResource> resources = new(StringComparer.Ordinal);

        foreach (MigrationResource resource in data.Resources)
        {
            EnsureEntityId(resource.Id, "resource");
            if (!resourceIds.Add(resource.Id!) || !resources.TryAdd(resource.Id, resource) ||
                resource.Kind is not ("site-list" or "binary") || resource.Size < 0)
                throw Invalid("A resource identity, kind, or size is invalid.");
            MigrationArchiveItem item = RequireManifestItem(manifestItems, resource.PayloadPath, resource.SourceRelativePath);
            string expectedKind = resource.Kind == "binary" ? "BinaryResource" : "SiteList";
            if (item.Kind != expectedKind || item.Size != resource.Size || item.Sha256 != resource.Sha256)
                throw Invalid("A resource does not match its manifest item.");
            EnsureHash(resource.Sha256, resource.Id);
            EnsureText(resource.Component, "resource component", 128);
        }

        foreach (MigrationPreset preset in data.Presets)
        {
            EnsureEntityId(preset.Id, "preset");
            if (!presetIds.Add(preset.Id!) || preset.CollectionId != "migrated-from-goodbyedpi-ui" ||
                preset.SourceFormat != "goodbyedpi-ui-json" ||
                preset.ParameterMode is not ("custom-parameters" or "structured-json") ||
                (preset.ParameterMode == "custom-parameters" && preset.CustomParameters == null))
                throw Invalid("A preset identity or format is invalid.");
            EnsureText(preset.Component, "preset component", 128);
            EnsureText(preset.Name, "preset name", 512);
            MigrationArchiveItem item = RequireManifestItem(manifestItems, preset.PayloadPath, preset.SourceRelativePath);
            if (item.Kind != "Preset" || item.Sha256 != preset.SourceSha256)
                throw Invalid("A preset does not match its manifest item.");
            EnsureHash(preset.SourceSha256, preset.Id);

            foreach (MigrationPresetResourceLink link in preset.Resources)
            {
                EnsureText(link.OriginalReference, "resource reference", 4096);
                if (link.Kind is not ("site-list" or "binary"))
                    throw Invalid("A preset resource link has an invalid kind.");
                if (link.IsResolved && (link.ResourceId == null || !resources.ContainsKey(link.ResourceId)))
                    throw Invalid("A resolved preset resource link points to an unknown resource.");
            }
        }

        foreach (MigrationResource resource in data.Resources)
        {
            if (resource.ReferencedByPresetIds.Distinct(StringComparer.Ordinal).Count() !=
                    resource.ReferencedByPresetIds.Length ||
                resource.ReferencedByPresetIds.Any(id => !presetIds.Contains(id)))
                throw Invalid("A resource references an unknown or duplicate preset.");
        }

        foreach (MigrationSetting setting in data.Settings)
        {
            EnsureEntityId(setting.Id, "setting");
            if (!settingIds.Add(setting.Id!) || setting.SourceFormat is not ("ini" or "json") ||
                setting.Ordinal < 0 || setting.ValueType is not ("string" or "boolean" or "number" or "null") ||
                setting.ImportPolicy is not ("preserve" or "candidate"))
                throw Invalid("A setting identity or format is invalid.");
            EnsureText(setting.Key, "setting key", 512);
            MigrationArchiveItem item = RequireManifestItem(manifestItems, setting.PayloadPath, setting.SourceRelativePath);
            if (item.Kind != "ApplicationSetting")
                throw Invalid("A setting does not match an application-setting manifest item.");
        }
        if (!manifest.Selection.IncludePresets && data.Presets.Count != 0 ||
            !manifest.Selection.IncludeApplicationSettings && data.Settings.Count != 0)
            throw Invalid("The migration data exceeds the user's selection.");
        ValidateDiagnostics(data.Diagnostics);
    }

    private static void VerifyPayloads(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        MigrationArchiveManifest manifest,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[81920];
        foreach (MigrationArchiveItem item in manifest.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ZipArchiveEntry entry = entries[item.ArchivePath!];
            if (entry.Length != item.Size)
                throw Invalid($"The archive entry '{entry.FullName}' has an invalid size.");
            using Stream stream = entry.Open();
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long readTotal = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                readTotal += read;
                if (readTotal > item.Size)
                    throw Invalid($"The archive entry '{entry.FullName}' exceeds its declared size.");
                hash.AppendData(buffer, 0, read);
            }
            string actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (readTotal != item.Size || actual != item.Sha256)
                throw Invalid($"The archive entry '{entry.FullName}' failed its integrity check.");
        }
    }

    private static MigrationArchiveItem RequireManifestItem(
        IReadOnlyDictionary<string, MigrationArchiveItem> manifestItems,
        string? payloadPath,
        string? sourcePath)
    {
        EnsureSafePath(payloadPath);
        EnsureSafePath(sourcePath);
        if (!manifestItems.TryGetValue(payloadPath!, out MigrationArchiveItem? item) ||
            !string.Equals(item.OriginalRelativePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            throw Invalid("A migration-data payload is absent from the manifest.");
        return item;
    }

    private static void ValidateDiagnostics(IEnumerable<MigrationDiagnostic> diagnostics)
    {
        foreach (MigrationDiagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Severity is not ("Information" or "Warning" or "Error"))
                throw Invalid("A migration diagnostic has an invalid severity.");
            EnsureText(diagnostic.Code, "diagnostic code", 256);
            if ((diagnostic.Message?.Length ?? 0) > 8192 ||
                (diagnostic.RelatedPath?.Length ?? 0) > 4096)
                throw Invalid("A migration diagnostic exceeds the size limit.");
        }
    }

    private static void EnsureEntityId(string? value, string prefix)
    {
        string expectedPrefix = prefix + "-";
        if (value == null || !value.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
            value.Length != expectedPrefix.Length + 24 ||
            value[expectedPrefix.Length..].Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Invalid($"A {prefix} ID is invalid.");
    }

    private static void EnsureHash(string? value, string? context)
    {
        if (value == null || value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Invalid($"The SHA-256 hash for '{context}' is invalid.");
    }

    private static void EnsureText(string? value, string context, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            throw Invalid($"The {context} is empty or too long.");
    }

    private static void EnsureSafePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 ||
            Path.IsPathRooted(path) || path.Contains(':') || path.Contains('\\'))
            throw Invalid("The migration archive contains an unsafe path.");
        string[] parts = path.Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or ".."))
            throw Invalid("The migration archive contains an unsafe path segment.");
    }

    private static string HashFile(string path, CancellationToken cancellationToken)
    {
        FileInfo info = new(path);
        if (info.Length <= 0 || info.Length > MaximumArchiveSize ||
            (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw Invalid("The protected staged archive has an unsafe type or size.");
        using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.SequentialScan);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static bool HashEquals(byte[] bytes, string? expected)
    {
        EnsureHash(expected, MigrationDataEntryName);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant() == expected;
    }

    private static InvalidDataException Invalid(string message) => new(message);
}
