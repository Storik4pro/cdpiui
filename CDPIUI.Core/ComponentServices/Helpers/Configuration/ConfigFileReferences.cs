using System.Text.RegularExpressions;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration;

public enum ConfigFileKind
{
    SiteList,
    Library,
    Payload,
    Other,
}

/// <summary>
/// A lexical file dependency. Path retains the reference used in the config;
/// ExpandedPath resolves variables and preset aliases, but not filesystem roots.
/// Missing files are deliberately included. Reading this model performs no I/O.
/// </summary>
public sealed record ConfigUsedFile(
    string Name,
    string Path,
    string Folder,
    ConfigFileKind Kind,
    string OptionName = "",
    bool IsAttachedResource = false)
{
    public string ExpandedPath { get; init; } = Path;
}

/// <summary>Shared, UI-independent parsing and rewriting of config file references.</summary>
public static class ConfigFileReferences
{
    public static IReadOnlyList<ConfigUsedFile> ExtractFromText(
        string? text,
        Func<string, string>? expandPath = null) => ConfigCommandLine.ParseOptions(text)
        .Select(option => ExtractOption(option, expandPath))
        .OfType<ConfigUsedFile>()
        .ToArray();

    public static ConfigUsedFile? ExtractDirectFile(string? value, Func<string, string>? expandPath = null)
    {
        if (string.IsNullOrWhiteSpace(value) || ConfigCommandLine.ParseOptions(value).Count > 0)
        {
            return null;
        }
        string path = NormalizePath(value);
        string expanded = NormalizePath(expandPath?.Invoke(path) ?? path);
        if (ConfigCommandLine.ParseOptions(expanded).Count > 0)
        {
            return null;
        }
        ConfigFileKind? kind = InferKind(expanded);
        return kind == null ? null : CreateFile(path, expanded, kind.Value);
    }

    public static ConfigFileKind? InferKind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".txt" or ".list" => ConfigFileKind.SiteList,
        ".lua" => ConfigFileKind.Library,
        ".bin" or ".dat" or ".der" or ".pem" => ConfigFileKind.Payload,
        _ => null,
    };

    private static ConfigUsedFile? ExtractOption(ConfigCommandOption option, Func<string, string>? expandPath)
    {
        if (string.IsNullOrWhiteSpace(option.Value) || option.Name.Equals("--debug", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        string path = GetOptionFileReference(option.Name, option.Value);
        string expanded = NormalizePath(expandPath?.Invoke(path) ?? path);
        ConfigFileKind? kind = IsSiteListOption(option.Name) ? ConfigFileKind.SiteList :
            option.Name.Equals("--lua-init", StringComparison.OrdinalIgnoreCase) ? ConfigFileKind.Library :
            option.Name.Equals("--blob", StringComparison.OrdinalIgnoreCase) ? ConfigFileKind.Payload : InferKind(expanded);
        if (path.Length == 0 || kind == null ||
            (kind != ConfigFileKind.SiteList && !LooksLikeFilePath(expanded)))
        {
            return null;
        }
        // --lua-init also accepts inline Lua, and --blob accepts inline hexadecimal data.
        if (option.Name.Equals("--lua-init", StringComparison.OrdinalIgnoreCase) &&
            expanded.IndexOfAny([';', '=', '\n', '\r']) >= 0)
        {
            return null;
        }
        return CreateFile(path, expanded, kind.Value, option.Name);
    }

    public static ConfigUsedFile CreateFile(
        string path, string expandedPath, ConfigFileKind kind,
        string optionName = "", bool isAttachedResource = false)
    {
        string normalized = NormalizePath(expandedPath);
        string name = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? normalized;
        return new ConfigUsedFile(name, NormalizePath(path), GetDisplayFolder(normalized), kind,
            optionName, isAttachedResource) { ExpandedPath = normalized };
    }

    public static string GetDisplayFolder(string path)
    {
        string normalized = NormalizePath(path);
        int separator = normalized.LastIndexOf('/');
        return separator > 0 ? normalized[..separator] : string.Empty;
    }

    public static string NormalizePath(string? value) =>
        ConfigCommandLine.Unquote(StripFileMarker(ConfigCommandLine.Unquote(value))).Replace('\\', '/');

    public static bool PathsEqual(string? left, string? right) =>
        string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);

    public static string StripFileMarker(string value)
    {
        if (value.StartsWith('@') ||
            (value.StartsWith('$') && !value.StartsWith("$GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase)))
        {
            return value[1..];
        }
        return value;
    }

    private static bool LooksLikeFilePath(string path) =>
        path.Contains('/') || path.Contains('\\') || !string.IsNullOrWhiteSpace(Path.GetExtension(path));

    private static bool IsSiteListOption(string optionName) => optionName.ToLowerInvariant() is
        "--hostlist" or "--hostlist-exclude" or "--hostlist-auto" or "--ipset" or "--ipset-exclude";

    private static string GetOptionFileReference(string name, string value)
    {
        string path = ConfigCommandLine.Unquote(value);
        if (name.Equals("--blob", StringComparison.OrdinalIgnoreCase))
        {
            int separator = path.IndexOf(':');
            path = separator >= 0 ? path[(separator + 1)..] : path;
        }
        return NormalizePath(path);
    }

    /// <summary>Replaces all matching file arguments, retaining blob names and Lua markers.</summary>
    public static string ReplaceFileInText(string? text, string filePath, ConfigFileKind kind, string replacementPath)
    {
        text ??= string.Empty;
        List<string> tokens = ConfigCommandLine.Tokenize(text).ToList();
        bool replaced = false;
        for (int index = 0; index < tokens.Count; index++)
        {
            string token = tokens[index];
            if (!ConfigCommandLine.TryGetOptionName(token, out string name, out int equalsIndex))
            {
                continue;
            }
            int valueIndex = equalsIndex >= 0 ? index : index + 1;
            if (valueIndex >= tokens.Count || (equalsIndex < 0 && ConfigCommandLine.IsOption(tokens[valueIndex])))
            {
                continue;
            }
            string value = equalsIndex >= 0 ? token[(equalsIndex + 1)..] : tokens[valueIndex];
            ConfigUsedFile? detected = ExtractOption(new ConfigCommandOption(name, token, value), null);
            if ((detected != null && detected.Kind != kind) || !PathsEqual(GetOptionFileReference(name, value), filePath))
            {
                continue;
            }
            string updatedValue;
            string unquoted = ConfigCommandLine.Unquote(value);
            if (IsSiteListOption(name))
            {
                updatedValue = ConfigCommandLine.Quote(replacementPath, force: true);
            }
            else if (name.Equals("--lua-init", StringComparison.OrdinalIgnoreCase))
            {
                char marker = unquoted.StartsWith('$') && !unquoted.StartsWith("$GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase) ? '$' : '@';
                updatedValue = ConfigCommandLine.Quote($"{marker}{replacementPath}");
            }
            else if (name.Equals("--blob", StringComparison.OrdinalIgnoreCase))
            {
                int separator = unquoted.IndexOf(':');
                string prefix = separator >= 0 ? unquoted[..(separator + 1)] : string.Empty;
                string source = separator >= 0 ? unquoted[(separator + 1)..] : unquoted;
                char marker = source.StartsWith('$') && !source.StartsWith("$GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase) ? '$' : '@';
                updatedValue = ConfigCommandLine.Quote($"{prefix}{marker}{replacementPath}");
            }
            else
            {
                updatedValue = ConfigCommandLine.Quote(replacementPath);
            }
            tokens[valueIndex] = equalsIndex >= 0 ? $"{name}={updatedValue}" : updatedValue;
            replaced = true;
        }
        return replaced ? string.Join(' ', tokens) : text;
    }

    public static string RewritePresetReferences(string? text, IReadOnlyDictionary<string, string> replacements)
    {
        string result = text ?? string.Empty;
        foreach ((string alias, string replacement) in replacements.OrderByDescending(item => item.Key.Length))
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }
            string escapedReference = Regex.Escape($"preset://{alias}");
            string escapedPath = replacement.Replace("\"", "\\\"");
            result = Regex.Replace(result, $"[\"']{escapedReference}[\"']", _ => $"\"{escapedPath}\"",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(result, $"{escapedReference}(?![A-Za-z0-9_.-])",
                _ => ConfigCommandLine.Quote(replacement, replacement.Contains('&') || replacement.Contains(';')),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        return result;
    }

    public static string RestorePresetReferences(string? text, IEnumerable<ConfigMakerResourceMetadata> resources)
    {
        string result = text ?? string.Empty;
        foreach (ConfigMakerResourceMetadata resource in resources
            .Where(resource => !string.IsNullOrWhiteSpace(resource.alias) && !string.IsNullOrWhiteSpace(resource.path))
            .OrderByDescending(resource => resource.path!.Length))
        {
            result = Regex.Replace(result, Regex.Escape(resource.path!), _ => $"preset://{resource.alias}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        return result;
    }
}
