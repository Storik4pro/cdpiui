using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Data;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigShare;

/// <summary>Portable, versioned single-preset ZIP format. No package install scripts are executed.</summary>
public sealed class ConfigShareService
{
    public const string Extension = ".cdpiconfig";
    internal const string SystemShareMarker = ".retain-for-system-share";
    internal const string ReferencePrefix = "share://";
    private const string ManifestName = "manifest.json";
    private const long MaxTotalBytes = 512L * 1024 * 1024;
    private const int MaxEntries = 4096;
    private const int MaxManifestBytes = 4 * 1024 * 1024;
    private static readonly SemaphoreSlim InstallGate = new(1, 1);

    public static bool IsSupported(string path) => Path.GetExtension(path).Equals(Extension, StringComparison.OrdinalIgnoreCase);
    public static bool IsWritablePackId(string? id) => id == SharedConstants.LocalUserItemsId || Guid.TryParse(id, out _);

    public static string? GetInstalledComponentId(ConfigItem config)
    {
        string? required = config.target?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(required)) return null;
        if (DatabaseHelper.Instance.GetItemById(required)?.Type == "component") return required;
        string zapret2 = HardcodedItemIds.ComponentIds[Components.Zapret2];
        return required == HardcodedItemIds.ComponentIds[Components.Zapret] &&
            DatabaseHelper.Instance.GetItemById(zapret2)?.Type == "component" ? zapret2 : null;
    }

    public static string? GetMissingComponentId(ConfigItem config) =>
        GetInstalledComponentId(config) == null ? config.target?.FirstOrDefault() : null;

    /// <summary>Called when opening the next export dialog, including after restarting the app.</summary>
    public static void CleanupPreviousSystemShares()
    {
        TryCleanup(() =>
        {
            if (!Directory.Exists(Directories.TempFilesDirectory)) return;
            foreach (string directory in Directory.EnumerateDirectories(Directories.TempFilesDirectory, "*_ConfigShare_*"))
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0 &&
                    File.Exists(Path.Combine(directory, SystemShareMarker)))
                    DeleteTemporaryDirectory(directory);
        });
    }

    public IReadOnlyList<DatabaseStoreItem> GetDestinationPacks() => DatabaseHelper.Instance.GetItemsByType("configlist")
        .Where(item => IsWritablePackId(item.Id))
        .OrderBy(item => item.Id == SharedConstants.LocalUserItemsId ? 0 : 1)
        .ThenBy(item => item.ShortName ?? item.Name).ToList();

    public Task<ConfigSharePackage> ExportAsync(ConfigItem source, string name, string developer,
        string? componentDirectory = null) => Task.Run(() => Export(source, name, developer, componentDirectory));

    private ConfigSharePackage Export(ConfigItem source, string name, string developer, string? componentDirectory)
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(developer))
                throw new ConfigShareException("SHARE_NAME_REQUIRED", "Preset name and developer are required.");
            ConfigItem config = Clone(source);
            if (config.target is not { Count: > 0 } || string.IsNullOrWhiteSpace(config.startup_string))
                throw new ConfigShareException("SHARE_CONFIG_INVALID", "The preset has no component or startup arguments.");
            string packDirectory = Path.IsPathFullyQualified(source.packId ?? "") ? source.packId!
                : ConfigurationService.GetItemFolderFromPackId(source.packId ?? SharedConstants.LocalUserItemsId);
            componentDirectory ??= DatabaseHelper.Instance.GetItemById(config.target[0])?.Directory;
            var collector = new ConfigShareResources(config, packDirectory, componentDirectory);
            collector.Collect();
            config.name = config.not_converted_name = name.Trim();
            config.jparams ??= [];
            config.variables ??= [];
            config.packId = null;
            config.file_name = null;
            config.MarkAsRemoved = false;
            var manifest = new ConfigShareManifest { Name = name.Trim(), Developer = developer.Trim(), Config = config };
            string archivePath = Path.Combine(directory, SafeFileName(name) + Extension);
            long total = 0;
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                foreach (var resource in collector.Files)
                {
                    byte[] bytes = ReadBoundedFile(resource.Key);
                    total += bytes.LongLength;
                    if (total > MaxTotalBytes || manifest.Resources.Count >= MaxEntries - 1)
                        throw new ConfigShareException("SHARE_TOO_LARGE", "The preset exceeds the package size or file count limit.");
                    using (var output = archive.CreateEntry(resource.Value.ArchivePath, CompressionLevel.Optimal).Open())
                        output.Write(bytes);
                    manifest.Resources.Add(new ConfigShareResource
                    {
                        Path = resource.Value.ArchivePath, Length = bytes.Length,
                        Sha256 = Convert.ToHexString(SHA256.HashData(bytes))
                    });
                }
                byte[] manifestBytes = Encoding.UTF8.GetBytes(JSONConvertor.SerializeObject(manifest));
                if (manifestBytes.Length > MaxManifestBytes)
                    throw new ConfigShareException("SHARE_TOO_LARGE", "The preset metadata is too large.");
                using var outputManifest = archive.CreateEntry(ManifestName).Open();
                outputManifest.Write(manifestBytes);
            }
            Log($"Exported {manifest.Name}: {manifest.Resources.Count} resources.");
            return new ConfigSharePackage(directory, archivePath, manifest, config);
        }
        catch (Exception exception)
        {
            DeleteTemporaryDirectory(directory);
            throw Report("SHARE_EXPORT_FAILED", exception);
        }
    }

    public Task<ConfigSharePackage> ReadAsync(string path) => Task.Run(() => Read(path));

    public ConfigSharePackage Read(string path)
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            using var archive = ZipFile.OpenRead(path);
            if (archive.Entries.Count == 0 || archive.Entries.Count > MaxEntries)
                throw new ConfigShareException("SHARE_ARCHIVE_INVALID", "Invalid archive file count.");
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                SafeDestination(directory, entry.FullName);
                if (!entries.TryAdd(entry.FullName, entry) || entry.Length > MaxTotalBytes || (total += entry.Length) > MaxTotalBytes)
                    throw new ConfigShareException("SHARE_ARCHIVE_INVALID", "Duplicate entries or excessive archive size.");
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                    throw new ConfigShareException("SHARE_ARCHIVE_INVALID", "Symbolic links are not supported.");
            }
            if (!entries.TryGetValue(ManifestName, out var manifestEntry) || manifestEntry.Length > MaxManifestBytes)
                throw new ConfigShareException("SHARE_ARCHIVE_INVALID", "The archive has no valid manifest.");
            string json = Encoding.UTF8.GetString(ReadEntry(manifestEntry, MaxManifestBytes));
            var manifest = JSONConvertor.DeserializeObject<ConfigShareManifest>(json)
                ?? throw new ConfigShareException("SHARE_ARCHIVE_INVALID", "The manifest is empty.");
            if (manifest.Version != 1)
                throw new ConfigShareException("SHARE_VERSION_UNSUPPORTED", $"Unsupported format version: {manifest.Version}.");
            if (string.IsNullOrWhiteSpace(manifest.Name) || string.IsNullOrWhiteSpace(manifest.Developer) ||
                manifest.Config?.target is not { Count: > 0 } || string.IsNullOrWhiteSpace(manifest.Config.target[0]) ||
                string.IsNullOrWhiteSpace(manifest.Config.startup_string) || manifest.Resources == null ||
                entries.Count != manifest.Resources.Count + 1)
                throw new ConfigShareException("SHARE_ARCHIVE_INVALID", "The preset metadata or resource list is invalid.");
            var references = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var resource in manifest.Resources)
            {
                if (resource == null || !resource.Path.StartsWith("resources/", StringComparison.Ordinal) ||
                    !entries.TryGetValue(resource.Path, out var entry) || resource.Length != entry.Length ||
                    !references.TryAdd(ReferencePrefix + resource.Path, SafeDestination(directory, resource.Path).Replace('\\', '/')))
                    throw new ConfigShareException("SHARE_ARCHIVE_INVALID", "Invalid or missing resource entry.");
                byte[] data = ReadEntry(entry, MaxTotalBytes);
                if (!Convert.ToHexString(SHA256.HashData(data)).Equals(resource.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new ConfigShareException("SHARE_CHECKSUM_FAILED", $"Resource checksum mismatch: {resource.Path}");
                string destination = SafeDestination(directory, resource.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(destination, data);
            }
            ConfigItem config = Clone(manifest.Config);
            config.jparams ??= [];
            config.variables ??= [];
            AddParentReferences(references);
            ConfigShareResources.MapConfig(config, text => ReplaceConfigReferences(text, references));
            config.packId = directory;
            config.file_name = null;
            foreach (var resource in manifest.Resources.Where(item => item.RewriteReferences))
            {
                string destination = SafeDestination(directory, resource.Path);
                File.WriteAllText(destination, ReplaceReferences(File.ReadAllText(destination), references), new UTF8Encoding(false));
            }
            Log($"Opened {Path.GetFileName(path)}: {manifest.Resources.Count} resources.");
            return new ConfigSharePackage(directory, Path.GetFullPath(path), manifest, config);
        }
        catch (Exception exception)
        {
            DeleteTemporaryDirectory(directory);
            throw Report("SHARE_IMPORT_FAILED", exception);
        }
    }

    public async Task<ConfigShareInstallResult> InstallAsync(ConfigSharePackage package, string displayName,
        string? packId = null, string? newPackName = null, ConfigItem? editedConfig = null)
    {
        await InstallGate.WaitAsync();
        try { return await Task.Run(() => Install(package, displayName, packId, newPackName, editedConfig)); }
        finally { InstallGate.Release(); }
    }

    private ConfigShareInstallResult Install(ConfigSharePackage package, string name, string? packId,
        string? newPackName, ConfigItem? editedConfig)
    {
        bool createPack = newPackName != null;
        packId = createPack ? Guid.NewGuid().ToString("D") : packId ?? SharedConstants.LocalUserItemsId;
        string? newPackDirectory = null;
        string? resourceDirectory = null;
        string? configPath = null;
        bool configCreated = false;
        try
        {
            if (!IsWritablePackId(packId))
                throw new ConfigShareException("SHARE_DESTINATION_INVALID", "Store kits cannot receive shared presets.");
            if (string.IsNullOrWhiteSpace(name) || (createPack && string.IsNullOrWhiteSpace(newPackName)))
                throw new ConfigShareException("SHARE_NAME_REQUIRED", "A preset and kit name are required.");
            string packDirectory = ConfigurationService.GetItemFolderFromPackId(packId);
            if (!createPack && DatabaseHelper.Instance.GetItemById(packId)?.Type != "configlist")
                throw new ConfigShareException("SHARE_DESTINATION_INVALID", "The selected kit is not installed.");
            if (createPack)
            {
                if (Directory.Exists(packDirectory)) throw new IOException("Kit directory already exists.");
                Directory.CreateDirectory(packDirectory);
                newPackDirectory = packDirectory;
                File.WriteAllText(Path.Combine(packDirectory, "init.json"), JSONConvertor.SerializeObject(new ConfigInitItem
                { toggleListAvailable = [], localized_strings_directory = [] }));
            }
            string relativeRoot = "Shared/" + Guid.NewGuid().ToString("N");
            resourceDirectory = SafeDestination(packDirectory, relativeRoot);
            Directory.CreateDirectory(resourceDirectory);
            var runtimeReferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var libraryReferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var resource in package.Manifest.Resources)
            {
                string source = SafeDestination(package.DirectoryPath, resource.Path);
                string target = SafeDestination(resourceDirectory, resource.Path);
                runtimeReferences[source] = "$GETCURRENTDIR()/" + relativeRoot + "/" + resource.Path;
                runtimeReferences[source.Replace('\\', '/')] = runtimeReferences[source];
                runtimeReferences[ReferencePrefix + resource.Path] = runtimeReferences[source];
                libraryReferences[source] = target.Replace('\\', '/');
                libraryReferences[source.Replace('\\', '/')] = libraryReferences[source];
                libraryReferences[ReferencePrefix + resource.Path] = libraryReferences[source];
            }
            AddParentReferences(runtimeReferences);
            AddParentReferences(libraryReferences);
            foreach (var resource in package.Manifest.Resources)
            {
                string source = SafeDestination(package.DirectoryPath, resource.Path);
                string destination = SafeDestination(resourceDirectory, resource.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (resource.RewriteReferences)
                    File.WriteAllText(destination, ReplaceReferences(File.ReadAllText(source), libraryReferences), new UTF8Encoding(false));
                else File.Copy(source, destination, false);
            }
            ConfigItem config = Clone(editedConfig ?? package.Config);
            ConfigShareResources.MapConfig(config, text => ReplaceConfigReferences(text, runtimeReferences));
            config.name = config.not_converted_name = name.Trim();
            config.packId = packId;
            string filename = SafeFileName(name) + "_" + Guid.NewGuid().ToString("N")[..8] + ".json";
            config.file_name = filename;
            configPath = SafeDestination(packDirectory, filename);
            using (var stream = new FileStream(configPath, FileMode.CreateNew))
            {
                configCreated = true;
                using var output = new StreamWriter(stream, new UTF8Encoding(false));
                output.Write(JSONConvertor.SerializeObject(config));
            }
            if (createPack && !DatabaseHelper.Instance.AddOrUpdateItem(new DatabaseStoreItem
            {
                Id = packId, Type = "configlist", Directory = packDirectory, Name = newPackName!.Trim(),
                ShortName = newPackName.Trim(), Developer = package.Manifest.Developer,
                VersionControlType = "local", CurrentVersion = "1.0", IconPath = "$STATICIMAGE(Store/empty.png)",
                BackgroudColor = "",
                RequiredItemIds = [Tuple.Create(config.target![0], config.target.ElementAtOrDefault(1) ?? "0.0")], DependentItemIds = []
            })) throw new ConfigShareException("SHARE_DATABASE_FAILED", "Cannot register the new kit.");
            Log($"Installed shared preset in {packId}/{filename}.");
            return new ConfigShareInstallResult(filename, packId);
        }
        catch (Exception exception)
        {
            if (configCreated && configPath != null) TryCleanup(() => File.Delete(configPath));
            if (resourceDirectory != null) TryCleanup(() => Directory.Delete(resourceDirectory, true));
            if (newPackDirectory != null) TryCleanup(() => Directory.Delete(newPackDirectory, true));
            throw Report("SHARE_INSTALL_FAILED", exception);
        }
    }

    internal static ConfigItem Clone(ConfigItem config) => JSONConvertor.DeserializeObject<ConfigItem>(JSONConvertor.SerializeObject(config))!;

    private static void AddParentReferences(Dictionary<string, string> references)
    {
        foreach (var (source, destination) in references.ToArray())
        {
            string left = source.Replace('\\', '/');
            string right = destination.Replace('\\', '/');
            // All packaged files start with resources/<category>/... . Only expose their real parents.
            int boundary = left.IndexOf("resources/", StringComparison.Ordinal);
            if (boundary < 0) continue;
            while (left.LastIndexOf('/') > boundary + "resources".Length && right.LastIndexOf('/') >= 0)
            {
                left = left[..left.LastIndexOf('/')];
                right = right[..right.LastIndexOf('/')];
                references.TryAdd(left, right);
            }
        }
    }

    internal static string ReplaceReferences(string text, IReadOnlyDictionary<string, string> replacements)
    {
        if (replacements.Count == 0) return text;
        string pattern = string.Join("|", replacements.Keys.OrderByDescending(key => key.Length).Select(Regex.Escape));
        return Regex.Replace(text, pattern, match => replacements[match.Value], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5));
    }

    internal static string ReplaceConfigReferences(string text, IReadOnlyDictionary<string, string> replacements)
    {
        if (replacements.Count == 0) return text;
        string pattern = string.Join("|", replacements.Keys.OrderByDescending(key => key.Length).Select(Regex.Escape));
        return Regex.Replace(text, pattern, match =>
        {
            string replacement = replacements[match.Value];
            // The runtime directory may contain spaces on the recipient's computer.
            // Preserve existing quoting and quote otherwise bare path arguments.
            char quote = '\0';
            for (int i = 0; i < match.Index; i++)
                if (text[i] is '"' or '\'')
                    quote = quote == text[i] ? '\0' : quote == '\0' ? text[i] : quote;
            bool wholeValue = match.Index == 0 && match.Length == text.Length;
            return quote != '\0' || wholeValue ? replacement : '"' + replacement + '"';
        }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
    }

    internal static string SafeDestination(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') ||
            relative.Split('/').Any(part => part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase)))
            throw new ConfigShareException("SHARE_ARCHIVE_INVALID", $"Unsafe resource path: {relative}");
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string destination = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!destination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new ConfigShareException("SHARE_ARCHIVE_INVALID", "Resource path leaves its destination.");
        // Imported archives never create links. Existing destination parents must not redirect writes either.
        for (string? parent = Path.GetDirectoryName(destination); parent != null && parent.Length >= fullRoot.Length - 1; parent = Path.GetDirectoryName(parent))
            if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                throw new ConfigShareException("SHARE_DESTINATION_INVALID", "The destination contains a filesystem link.");
        return destination;
    }

    internal static byte[] ReadBoundedFile(string path)
    {
        using var input = File.OpenRead(path);
        if (input.Length > MaxTotalBytes) throw new ConfigShareException("SHARE_TOO_LARGE", path);
        using var output = new MemoryStream();
        CopyBounded(input, output, MaxTotalBytes);
        return output.ToArray();
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, long limit)
    {
        using var input = entry.Open();
        using var output = new MemoryStream();
        CopyBounded(input, output, Math.Min(limit, entry.Length));
        if (output.Length != entry.Length) throw new InvalidDataException("Truncated ZIP entry.");
        return output.ToArray();
    }

    private static void CopyBounded(Stream input, Stream output, long limit)
    {
        byte[] buffer = new byte[81920];
        int count;
        long total = 0;
        while ((count = input.Read(buffer)) > 0)
        {
            if ((total += count) > limit) throw new ConfigShareException("SHARE_TOO_LARGE", "Resource exceeds declared size.");
            output.Write(buffer, 0, count);
        }
    }

    public static string SafeFileName(string name)
    {
        string safe = new(name.Trim().Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character).ToArray());
        safe = safe.TrimEnd('.', ' ');
        return "preset_" + (safe.Length > 80 ? safe[..80] : safe.Length == 0 ? "shared" : safe);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Directories.TempFilesDirectory,
            FileSystemService.GetNewTempFileName("ConfigShare_" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(path);
        return path;
    }

    internal static void DeleteTemporaryDirectory(string path)
    {
        string root = Path.GetFullPath(Directories.TempFilesDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(path).Contains("_ConfigShare_", StringComparison.Ordinal) && Directory.Exists(path))
            TryCleanup(() => Directory.Delete(path, true));
    }

    private static void TryCleanup(Action cleanup)
    {
        try { cleanup(); }
        catch (Exception exception) { Logger.Instance.CreateWarningLog(nameof(ConfigShareService), $"Cleanup failed: {exception}"); }
    }

    private static void Log(string text)
    {
        try { Logger.Instance.CreateInfoLog(nameof(ConfigShareService), text); }
        catch { /* A logging failure must not roll back an already registered kit. */ }
    }
    internal static ConfigShareException Report(string code, Exception exception)
    {
        Logger.Instance.CreateWarningLog(nameof(ConfigShareService), $"{code}: {exception}");
        return exception as ConfigShareException ?? new ConfigShareException(code, exception.Message, exception);
    }
}
