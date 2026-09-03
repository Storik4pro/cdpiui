using CDPIUI.Shared.Migration;
using System.Collections.Generic;

namespace CDPIUI.Helper.Migration;

internal sealed class MigrationArchiveManifest
{
    public int SchemaVersion { get; set; }
    public string? PackageKind { get; set; }
    public string? MigrationId { get; set; }
    public string? CreatedUtc { get; set; }
    public string? SourceProduct { get; set; }
    public string? TargetProduct { get; set; }
    public string? SourceRoot { get; set; }
    public string? PresetCollectionNameRu { get; set; }
    public string? PresetCollectionNameEn { get; set; }
    public MigrationSelection? Selection { get; set; }
    public string? MigrationDataPath { get; set; }
    public int MigrationDataSchemaVersion { get; set; }
    public long MigrationDataSize { get; set; }
    public string? MigrationDataSha256 { get; set; }
    public List<MigrationArchiveItem> Items { get; set; } = [];
    public List<MigrationDiagnostic> Diagnostics { get; set; } = [];
}

internal sealed class MigrationSelection
{
    public bool IncludePresets { get; set; }
    public bool IncludeApplicationSettings { get; set; }
    public bool IncludeSiteLists { get; set; }
}

internal sealed class MigrationArchiveItem
{
    public string? Kind { get; set; }
    public string? Component { get; set; }
    public string? OriginalRelativePath { get; set; }
    public string? ArchivePath { get; set; }
    public long Size { get; set; }
    public string? Sha256 { get; set; }
    public bool IsPresetDependency { get; set; }
    public string[] ReferencedBy { get; set; } = [];
}

internal sealed class MigrationDataDocument
{
    public string? SchemaId { get; set; }
    public int SchemaVersion { get; set; }
    public string? MigrationId { get; set; }
    public string? PackageKind { get; set; }
    public string? CreatedUtc { get; set; }
    public MigrationSource? Source { get; set; }
    public MigrationPresetCollection? PresetCollection { get; set; }
    public MigrationSelection? Selection { get; set; }
    public List<MigrationPreset> Presets { get; set; } = [];
    public List<MigrationSetting> Settings { get; set; } = [];
    public List<MigrationResource> Resources { get; set; } = [];
    public List<MigrationDiagnostic> Diagnostics { get; set; } = [];
}

internal sealed class MigrationSource
{
    public string? Product { get; set; }
    public string? FolderName { get; set; }
}

internal sealed class MigrationPresetCollection
{
    public string? Id { get; set; }
    public string? NameRu { get; set; }
    public string? NameEn { get; set; }
}

internal sealed class MigrationPreset
{
    public string? Id { get; set; }
    public string? CollectionId { get; set; }
    public string? Component { get; set; }
    public string? Name { get; set; }
    public string? SourceRelativePath { get; set; }
    public string? PayloadPath { get; set; }
    public string? SourceSha256 { get; set; }
    public string? SourceFormat { get; set; }
    public string? ParameterMode { get; set; }
    public string? CustomParameters { get; set; }
    public bool IsUserPreset { get; set; }
    public bool RequiresReview { get; set; }
    public List<MigrationPresetResourceLink> Resources { get; set; } = [];
}

internal sealed class MigrationPresetResourceLink
{
    public string? OriginalReference { get; set; }
    public string? Kind { get; set; }
    public bool IsResolved { get; set; }
    public string? ResourceId { get; set; }
    public string? ResolvedRelativePath { get; set; }
    public string? DiagnosticCode { get; set; }
}

internal sealed class MigrationResource
{
    public string? Id { get; set; }
    public string? Kind { get; set; }
    public string? Component { get; set; }
    public string? SourceRelativePath { get; set; }
    public string? PayloadPath { get; set; }
    public string? Sha256 { get; set; }
    public long Size { get; set; }
    public bool IsPresetDependency { get; set; }
    public string[] ReferencedByPresetIds { get; set; } = [];
}

internal sealed class MigrationSetting
{
    public string? Id { get; set; }
    public string? SourceRelativePath { get; set; }
    public string? PayloadPath { get; set; }
    public string? SourceFormat { get; set; }
    public int Ordinal { get; set; }
    public string? Section { get; set; }
    public string? Key { get; set; }
    public string? JsonPath { get; set; }
    public string? Value { get; set; }
    public string? ValueType { get; set; }
    public string? SemanticKey { get; set; }
    public string? ImportPolicy { get; set; }
}

internal sealed class MigrationDiagnostic
{
    public string? Severity { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? RelatedPath { get; set; }
}

internal sealed class VerifiedMigrationPackage
{
    public required GoodbyeDpiMigrationActivationRequest Request { get; init; }
    public required string StagedArchivePath { get; init; }
    public required MigrationArchiveManifest Manifest { get; init; }
    public required MigrationDataDocument Data { get; init; }
    public required IReadOnlyList<MigrationComponentRequirement> Components { get; init; }
}

internal sealed record MigrationComponentRequirement(
    string SourceName,
    string ConfigTargetId,
    string InstallItemId);

internal sealed class MigrationImportResult
{
    public int ImportedPresetCount { get; init; }
    public int ImportedResourceCount { get; init; }
    public int ImportedSettingCount { get; init; }
    public int ReviewRequiredCount { get; init; }
    public string? BackupDirectory { get; init; }
    public List<MigrationImportIssue> Issues { get; init; } = [];
}

internal sealed record MigrationImportIssue(
    string Kind,
    string Source,
    string Message);
