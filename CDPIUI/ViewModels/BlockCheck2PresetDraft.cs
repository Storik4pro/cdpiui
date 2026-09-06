using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Helper.CreateConfigHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CDPIUI.ViewModels;

public enum BlockCheck2PresetDraftOrigin
{
    Automatic,
    Manual,
    ModifiedAutomatic,
}

public sealed class BlockCheck2PresetDraft : INotifyPropertyChanged
{
    private string automaticArguments = string.Empty;
    private string structuredArguments = string.Empty;
    private string expertArguments = string.Empty;
    private bool isExpertEditingEnabled;
    private bool hasExpertChanges;
    private BlockCheck2PresetDraftOrigin origin = BlockCheck2PresetDraftOrigin.Manual;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BlockCheck2ManualAssignmentItem> Assignments { get; } = [];
    public ObservableCollection<BlockCheck2PresetFileItem> Files { get; } = [];
    public ObservableCollection<BlockCheck2PresetGroupItem> Groups { get; } = [];

    public BlockCheck2PresetDraftOrigin Origin => origin;
    public bool HasAutomaticSource => !string.IsNullOrWhiteSpace(automaticArguments);
    public bool IsAutomatic => origin == BlockCheck2PresetDraftOrigin.Automatic;
    public bool IsModifiedAutomatic => origin == BlockCheck2PresetDraftOrigin.ModifiedAutomatic;
    public bool IsExpertEditingEnabled => isExpertEditingEnabled;
    public bool HasExpertChanges => hasExpertChanges;
    public string StructuredArguments => structuredArguments;
    public string EffectiveArguments => isExpertEditingEnabled ? expertArguments : structuredArguments;
    public bool CanUseConfig => !string.IsNullOrWhiteSpace(EffectiveArguments);

    public void LoadAutomatic(
        string? arguments,
        IEnumerable<BlockCheckReportProfile> profiles)
    {
        automaticArguments = arguments?.Trim() ?? string.Empty;
        structuredArguments = automaticArguments;
        expertArguments = string.Empty;
        isExpertEditingEnabled = false;
        hasExpertChanges = false;
        origin = HasAutomaticSource
            ? BlockCheck2PresetDraftOrigin.Automatic
            : BlockCheck2PresetDraftOrigin.Manual;
        ReplaceDetails(
            profiles.Select(BlockCheck2PresetGroupItem.FromReport),
            structuredArguments);
        NotifyAll();
    }

    public void ApplyStructuredChange(
        string? arguments,
        IEnumerable<Zapret2ProfilePlan>? profiles)
    {
        structuredArguments = arguments?.Trim() ?? string.Empty;
        origin = HasAutomaticSource
            ? BlockCheck2PresetDraftOrigin.ModifiedAutomatic
            : BlockCheck2PresetDraftOrigin.Manual;
        DiscardExpertChanges(notify: false);
        ReplaceDetails(
            (profiles ?? []).Select(BlockCheck2PresetGroupItem.FromPlan),
            structuredArguments);
        NotifyAll();
    }

    public void BeginExpertEditing()
    {
        if (isExpertEditingEnabled)
        {
            return;
        }
        expertArguments = structuredArguments;
        isExpertEditingEnabled = true;
        hasExpertChanges = false;
        NotifyAll();
    }

    public void SetExpertArguments(string? arguments)
    {
        if (!isExpertEditingEnabled)
        {
            return;
        }
        expertArguments = arguments ?? string.Empty;
        bool filesChanged = ReplaceFiles(
            expertArguments,
            Groups.SelectMany(group => group.HostListPaths));
        hasExpertChanges = !string.Equals(
            expertArguments,
            structuredArguments,
            StringComparison.Ordinal);
        Notify(nameof(EffectiveArguments));
        Notify(nameof(HasExpertChanges));
        Notify(nameof(CanUseConfig));
        if (filesChanged)
        {
            Notify(nameof(Files));
        }
    }

    public void ApplyFileReferenceChange(
        string? arguments,
        string originalPath,
        string replacementPath)
    {
        string updatedArguments = arguments?.Trim() ?? string.Empty;
        if (isExpertEditingEnabled)
        {
            expertArguments = updatedArguments;
            hasExpertChanges = !string.Equals(
                expertArguments,
                structuredArguments,
                StringComparison.Ordinal);
        }
        else
        {
            structuredArguments = updatedArguments;
            origin = HasAutomaticSource
                ? BlockCheck2PresetDraftOrigin.ModifiedAutomatic
                : BlockCheck2PresetDraftOrigin.Manual;
            BlockCheck2PresetGroupItem[] updatedGroups = Groups
                .Select(group => group.ReplaceHostListPath(originalPath, replacementPath))
                .ToArray();
            Groups.Clear();
            foreach (BlockCheck2PresetGroupItem group in updatedGroups)
            {
                Groups.Add(group);
            }
        }
        ReplaceFiles(
            isExpertEditingEnabled ? expertArguments : structuredArguments,
            isExpertEditingEnabled
                ? []
                : Groups.SelectMany(group => group.HostListPaths));
        NotifyAll();
    }

    public void DiscardExpertChanges() => DiscardExpertChanges(notify: true);

    private void DiscardExpertChanges(bool notify)
    {
        expertArguments = string.Empty;
        isExpertEditingEnabled = false;
        hasExpertChanges = false;
        ReplaceFiles(
            structuredArguments,
            Groups.SelectMany(group => group.HostListPaths));
        if (notify)
        {
            NotifyAll();
        }
    }

    private void ReplaceDetails(
        IEnumerable<BlockCheck2PresetGroupItem> groups,
        string arguments)
    {
        BlockCheck2PresetGroupItem[] materialized = groups.ToArray();
        Groups.Clear();
        foreach (BlockCheck2PresetGroupItem group in materialized)
        {
            Groups.Add(group);
        }

        ReplaceFiles(arguments, materialized.SelectMany(group => group.HostListPaths));
    }

    private bool ReplaceFiles(string arguments, IEnumerable<string> hostListPaths)
    {
        (string Path, BlockCheck2PresetFileKind Kind)[] references = hostListPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => (NormalizeFileReference(path), BlockCheck2PresetFileKind.SiteList))
            .Concat(ExtractFileReferences(arguments))
            .Where(item => !string.IsNullOrWhiteSpace(item.Item1))
            .DistinctBy(
                item => $"{item.Item2}:{item.Item1}",
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Item2)
            .ThenBy(item => item.Item1, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (Files.Count == references.Length && Files
            .Zip(references)
            .All(pair => pair.First.Kind == pair.Second.Kind &&
                         string.Equals(
                             pair.First.Path,
                             pair.Second.Path,
                             StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        Files.Clear();
        foreach ((string path, BlockCheck2PresetFileKind kind) in references)
        {
            Files.Add(new BlockCheck2PresetFileItem(path, kind));
        }
        return true;
    }

    private static IEnumerable<(string Path, BlockCheck2PresetFileKind Kind)> ExtractFileReferences(
        string arguments)
    {
        IReadOnlyList<string> tokens = ComponentCommandLineFormatter.Tokenize(arguments);
        for (int index = 0; index < tokens.Count; index++)
        {
            string token = tokens[index];
            int equalsIndex = token.IndexOf('=');
            string option = equalsIndex >= 0 ? token[..equalsIndex] : token;
            string value = equalsIndex >= 0 ? token[(equalsIndex + 1)..] : string.Empty;
            if (string.IsNullOrEmpty(value) && index + 1 < tokens.Count &&
                !tokens[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                value = tokens[++index];
            }

            if (option.Equals("--hostlist", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("--hostlist-exclude", StringComparison.OrdinalIgnoreCase))
            {
                yield return (NormalizeFileReference(value), BlockCheck2PresetFileKind.SiteList);
            }
            else if (option.Equals("--lua-init", StringComparison.OrdinalIgnoreCase))
            {
                yield return (NormalizeFileReference(value), BlockCheck2PresetFileKind.Library);
            }
            else if (option.Equals("--blob", StringComparison.OrdinalIgnoreCase))
            {
                string blob = value.Trim().Trim('"', '\'');
                int separatorIndex = blob.IndexOf(':');
                string source = separatorIndex >= 0 ? blob[(separatorIndex + 1)..] : string.Empty;
                if (LooksLikeFileReference(source))
                {
                    yield return (NormalizeFileReference(source), BlockCheck2PresetFileKind.Payload);
                }
            }
        }
    }

    private static bool LooksLikeFileReference(string source)
    {
        string value = source.Trim().Trim('"', '\'');
        return !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
               (value.StartsWith("@", StringComparison.Ordinal) ||
                value.StartsWith("$", StringComparison.Ordinal) ||
                value.Contains('/') ||
                value.Contains('\\') ||
                !string.IsNullOrWhiteSpace(Path.GetExtension(value)));
    }

    private static string NormalizeFileReference(string source)
    {
        string value = (source ?? string.Empty).Trim().Trim('"', '\'');
        return value.StartsWith("@", StringComparison.Ordinal) ||
               value.StartsWith("$", StringComparison.Ordinal)
            ? value[1..]
            : value;
    }

    private void NotifyAll()
    {
        Notify(nameof(Origin));
        Notify(nameof(HasAutomaticSource));
        Notify(nameof(IsAutomatic));
        Notify(nameof(IsModifiedAutomatic));
        Notify(nameof(IsExpertEditingEnabled));
        Notify(nameof(HasExpertChanges));
        Notify(nameof(StructuredArguments));
        Notify(nameof(EffectiveArguments));
        Notify(nameof(CanUseConfig));
        Notify(nameof(Files));
    }

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum BlockCheck2PresetFileKind
{
    SiteList,
    Library,
    Payload,
}

public sealed class BlockCheck2PresetFileItem
{
    public BlockCheck2PresetFileItem(string path, BlockCheck2PresetFileKind kind)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        Folder = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
        Kind = kind;
    }

    public string Name { get; }
    public string Path { get; }
    public string Folder { get; }
    public BlockCheck2PresetFileKind Kind { get; }
}

public sealed class BlockCheck2PresetGroupItem
{
    private BlockCheck2PresetGroupItem(
        string name,
        string route,
        string scope,
        IEnumerable<string> strategies,
        IEnumerable<string> hostListPaths)
    {
        Name = name;
        Route = route;
        Scope = scope;
        Strategies = string.Join(" -> ", strategies.Where(value => !string.IsNullOrWhiteSpace(value)));
        HostListPaths = hostListPaths.ToArray();
    }

    public string Name { get; }
    public string Route { get; }
    public string Scope { get; }
    public string Strategies { get; }
    public IReadOnlyList<string> HostListPaths { get; }

    public BlockCheck2PresetGroupItem ReplaceHostListPath(string originalPath, string replacementPath)
    {
        string[] updatedPaths = HostListPaths
            .Select(path => FileReferencesEqual(path, originalPath) ? replacementPath : path)
            .ToArray();
        if (updatedPaths.SequenceEqual(HostListPaths, StringComparer.OrdinalIgnoreCase))
        {
            return this;
        }
        return new BlockCheck2PresetGroupItem(
            Name,
            Route,
            Scope.Replace(
                originalPath,
                replacementPath,
                StringComparison.OrdinalIgnoreCase),
            [Strategies],
            updatedPaths);
    }

    private static bool FileReferencesEqual(string left, string right) =>
        string.Equals(
            NormalizeFileReference(left),
            NormalizeFileReference(right),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeFileReference(string value) =>
        (value ?? string.Empty)
            .Trim()
            .Trim('"', '\'')
            .TrimStart('@', '$')
            .Replace('\\', '/')
            .TrimStart('/');

    public static BlockCheck2PresetGroupItem FromReport(BlockCheckReportProfile profile) => new(
        profile.Name,
        $"{profile.Layer7Protocol}/{profile.Transport}/{profile.IpVersion}:{profile.Port}",
        string.Join(", ", profile.HostListPaths.Concat(profile.Domains)),
        new[] { profile.PrimaryStrategyId }.Concat(profile.FallbackStrategyIds),
        profile.HostListPaths);

    public static BlockCheck2PresetGroupItem FromPlan(Zapret2ProfilePlan profile) => new(
        profile.Name,
        $"{profile.Filter.Layer7Protocol}/{profile.Filter.Transport}/{profile.Filter.IpVersion}:{profile.Filter.Port}",
        string.Join(", ", profile.Filter.HostListPaths.Concat(profile.Filter.Domains)),
        new[] { profile.Primary.Id }.Concat(profile.Fallbacks.Select(strategy => strategy.Id)),
        profile.Filter.HostListPaths);
}
