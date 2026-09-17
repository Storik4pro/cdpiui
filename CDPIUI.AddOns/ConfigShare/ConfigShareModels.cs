using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.AddOns.ConfigShare;

public sealed class ConfigShareManifest
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Developer { get; set; } = "";
    public ConfigItem Config { get; set; } = new();
    public List<ConfigShareResource> Resources { get; set; } = [];
}

public sealed class ConfigShareResource
{
    public string Path { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public bool RewriteReferences { get; set; }
}

/// <summary>Owns temporary files; system shares can transfer cleanup to the next export dialog.</summary>
public sealed class ConfigSharePackage : IDisposable
{
    private bool retainedForSystemShare;
    public string DirectoryPath { get; }
    public string ArchivePath { get; }
    public ConfigShareManifest Manifest { get; }
    public ConfigItem Config { get; }

    internal ConfigSharePackage(string directory, string archive, ConfigShareManifest manifest, ConfigItem config)
    {
        DirectoryPath = directory;
        ArchivePath = archive;
        Manifest = manifest;
        Config = config;
    }

    public void RetainForSystemShare()
    {
        File.WriteAllText(Path.Combine(DirectoryPath, ConfigShareService.SystemShareMarker), "");
        retainedForSystemShare = true;
    }

    public void Dispose()
    {
        if (!retainedForSystemShare) ConfigShareService.DeleteTemporaryDirectory(DirectoryPath);
    }
}

public sealed record ConfigShareInstallResult(string ConfigFileName, string PackId);

public sealed class ConfigShareException : Exception
{
    public string Code { get; }
    public ConfigShareException(string code, string details, Exception? inner = null) : base(details, inner) => Code = code;
}
