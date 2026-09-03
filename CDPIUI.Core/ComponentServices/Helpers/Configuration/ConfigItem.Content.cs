using System.Text.RegularExpressions;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration;

public partial class ConfigItem
{
    /// <summary>
    /// A fresh snapshot of file dependencies in arguments, variable alternatives and attached resources.
    /// No file existence checks, script execution, database access or mutation are performed.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [global::System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ConfigUsedFile> UsedFiles
    {
        get
        {
            List<ConfigUsedFile> files = [];
            foreach (ConfigMakerResourceMetadata resource in configMaker?.resources ?? [])
            {
                if (string.IsNullOrWhiteSpace(resource.alias) || string.IsNullOrWhiteSpace(resource.path))
                {
                    continue;
                }
                string reference = $"preset://{resource.alias}";
                ConfigFileKind kind = Enum.TryParse(resource.kind, true, out ConfigFileKind parsedKind)
                    ? parsedKind : ConfigFileReferences.InferKind(resource.path) ?? ConfigFileKind.Other;
                files.Add(ConfigFileReferences.CreateFile(reference, ExpandFileReference(reference), kind,
                    isAttachedResource: true) with { Name = resource.alias });
            }
            files.AddRange(ConfigFileReferences.ExtractFromText(startup_string, ExpandFileReference));
            foreach (string value in GetVariableCandidateValues())
            {
                files.AddRange(ConfigFileReferences.ExtractFromText(value, ExpandFileReference));
                ConfigUsedFile? directFile = ConfigFileReferences.ExtractDirectFile(value, ExpandFileReference);
                if (directFile != null)
                {
                    files.Add(directFile);
                }
            }
            return files.GroupBy(file => $"{file.Kind}|{file.ExpandedPath}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First() with
                {
                    OptionName = group.Select(file => file.OptionName).FirstOrDefault(name => name.Length > 0) ?? string.Empty,
                })
                .ToArray();
        }
    }

    /// <summary>All values, including unselected choices and both branches of local conditions.</summary>
    public IReadOnlyList<string> GetVariableCandidateValues()
    {
        List<string> result = [];
        result.AddRange(commaVars?.Values ?? Enumerable.Empty<string>());
        foreach (AvailableVarValues alternatives in availableCommaVarsValues ?? [])
        {
            result.AddRange(alternatives.Values ?? []);
        }
        foreach (string expression in variables ?? [])
        {
            if (!TryParseVariableAssignment(expression, out _, out string value))
            {
                continue;
            }
            if (TryParseLocalCondition(value, out _, out _, out string onValue, out string offValue))
            {
                result.Add(onValue);
                result.Add(offValue);
            }
            else
            {
                result.Add(value);
            }
        }
        foreach (ConfigMakerVariableMetadata variable in configMaker?.variables ?? [])
        {
            result.Add(variable.value ?? string.Empty);
            result.Add(variable.onValue ?? string.Empty);
            result.Add(variable.offValue ?? string.Empty);
            result.AddRange(variable.values ?? []);
        }
        return result.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Expands declared variables without executing LScript; cycles and unknown names remain unresolved.</summary>
    public string ExpandVariables(string? text)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (ConfigMakerVariableMetadata variable in configMaker?.variables ?? [])
        {
            if (string.IsNullOrWhiteSpace(variable.name))
            {
                continue;
            }
            bool isSwitch = string.Equals(variable.kind, "Switch", StringComparison.OrdinalIgnoreCase);
            values[variable.name] = (isSwitch
                ? variable.isSwitchEnabled ? variable.onValue : variable.offValue
                : variable.value) ?? string.Empty;
        }
        foreach ((string key, string value) in commaVars ?? [])
        {
            values[key] = value;
        }
        foreach (string expression in variables ?? [])
        {
            if (!TryParseVariableAssignment(expression, out string name, out string value))
            {
                continue;
            }
            if (TryParseLocalCondition(value, out string parameter, out bool expected,
                out string onValue, out string offValue))
            {
                value = (jparams?.GetValueOrDefault(parameter) ?? false) == expected ? onValue : offValue;
            }
            values[name] = value.Replace("$EMPTY", string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        string result = text ?? string.Empty;
        HashSet<string> visited = new(StringComparer.Ordinal);
        for (int depth = 0; depth < 16 && visited.Add(result); depth++)
        {
            string expanded = Regex.Replace(result, "%([A-Za-z0-9_]+)%",
                match => values.TryGetValue(match.Groups[1].Value, out string? value) ? value : match.Value,
                RegexOptions.CultureInvariant);
            if (expanded == result)
            {
                break;
            }
            result = expanded;
        }
        return result;
    }

    /// <summary>Expands a file's variables and preset:// alias, retaining $GETCURRENTDIR().</summary>
    public string ExpandFileReference(string? sourcePath)
    {
        string path = ConfigFileReferences.NormalizePath(ExpandVariables(sourcePath));
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        while (path.StartsWith("preset://", StringComparison.OrdinalIgnoreCase) && visited.Add(path))
        {
            string alias = path["preset://".Length..];
            ConfigMakerResourceMetadata? resource = configMaker?.resources?.FirstOrDefault(candidate =>
                string.Equals(candidate.alias, alias, StringComparison.OrdinalIgnoreCase));
            if (resource == null || string.IsNullOrWhiteSpace(resource.path))
            {
                break;
            }
            path = ConfigFileReferences.NormalizePath(ExpandVariables(resource.path));
        }
        return path;
    }

    /// <summary>
    /// Resolves a path without testing its existence. Ordinary relative paths belong to the component;
    /// $GETCURRENTDIR() belongs to the preset's pack. Roots are explicit to avoid ambient UI/database state.
    /// </summary>
    public string ResolveFilePath(string sourcePath, string componentDirectory, string presetDirectory)
    {
        string path = ExpandFileReference(sourcePath);
        if (path.StartsWith("preset://", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("Unknown or cyclic preset resource reference.", path);
        }
        if (path.StartsWith("$GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(presetDirectory))
            {
                throw new DirectoryNotFoundException("The preset directory is not specified.");
            }
            path = path["$GETCURRENTDIR()".Length..].TrimStart('/');
            return Path.GetFullPath(Path.Combine(presetDirectory, path.Replace('/', Path.DirectorySeparatorChar)));
        }
        if (Path.IsPathFullyQualified(path))
        {
            return Path.GetFullPath(path);
        }
        if (string.IsNullOrWhiteSpace(componentDirectory))
        {
            throw new DirectoryNotFoundException("The component directory is not specified.");
        }
        return Path.GetFullPath(Path.Combine(componentDirectory,
            path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
    }

    /// <summary>
    /// Rewrites runtime fields for testing/saving. Editor metadata deliberately keeps preset:// aliases.
    /// </summary>
    public void RewritePresetReferences(IReadOnlyDictionary<string, string> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        startup_string = ConfigFileReferences.RewritePresetReferences(startup_string, replacements);
        if (variables != null)
        {
            for (int index = 0; index < variables.Count; index++)
            {
                variables[index] = ConfigFileReferences.RewritePresetReferences(variables[index], replacements);
            }
        }
        if (commaVars != null)
        {
            foreach (string key in commaVars.Keys.ToArray())
            {
                commaVars[key] = ConfigFileReferences.RewritePresetReferences(commaVars[key], replacements);
            }
        }
        foreach (AvailableVarValues alternatives in availableCommaVarsValues ?? [])
        {
            if (alternatives.Values == null)
            {
                continue;
            }
            for (int index = 0; index < alternatives.Values.Count; index++)
            {
                alternatives.Values[index] = ConfigFileReferences.RewritePresetReferences(alternatives.Values[index], replacements);
            }
        }
    }

    private static bool TryParseVariableAssignment(string expression, out string name, out string value)
    {
        Match match = Regex.Match(expression ?? string.Empty, @"^\s*%(?<name>[A-Za-z0-9_]+)%\s*=(?<value>.*)$", RegexOptions.Singleline);
        name = match.Groups["name"].Value;
        value = match.Groups["value"].Value;
        return match.Success;
    }

    private static bool TryParseLocalCondition(string value, out string parameter, out bool expected,
        out string onValue, out string offValue)
    {
        Match match = Regex.Match(value,
            @"^\s*\$LOCALCONDITION\(\s*(?<parameter>[^=?]+?)\s*==\s*(?<expected>true|false)\s*\?(?<on>.*?)\$SEPARATOR(?<off>.*)\)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        parameter = match.Groups["parameter"].Value.Trim();
        expected = match.Groups["expected"].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        onValue = match.Groups["on"].Value.Trim();
        offValue = match.Groups["off"].Value.Trim();
        return match.Success;
    }
}
