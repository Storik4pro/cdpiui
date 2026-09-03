using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Static importer for command files and text configs. The selected component
/// supplies the executable name; source scripts are never executed.
/// </summary>
public sealed partial class ConfigImportService
{
    public const string CurrentVersionPlaceholder = "%CURRENT%";

    public ConfigImportResult Import(string filePath, ConfigImportTarget? target = null, string requestedTarget = "")
    {
        target ??= GetConfigImportTarget(filePath, requestedTarget);

        string fullPath = TryGetFullPath(filePath) ?? filePath;
        if (!ValidateRequest(fullPath, target, out ConfigImportResult? invalidResult))
            return invalidResult!;

        string executable = NormalizeExecutableName(target.Executable);
        if (IsZapretExecutable(executable))
            return ImportZapret(fullPath, target, executable);

        return ImportGeneric(fullPath, target, executable);
    }

    public ConfigImportResult Retarget(ConfigImportResult result, ConfigImportTarget target)
    {
        var issues = result.Issues.ToList();
        ConfigItem? config = result.Config == null
            ? null
            : JSONConvertor.DeserializeObject<ConfigItem>(JSONConvertor.SerializeObject(result.Config));
        if (config != null)
        {
            if (NormalizeExecutableName(result.Target.Executable).Equals("winws", StringComparison.OrdinalIgnoreCase) &&
                NormalizeExecutableName(target.Executable).Equals("winws2", StringComparison.OrdinalIgnoreCase))
            {
                config.packId = Path.GetDirectoryName(result.SourcePath);
                string expandedLegacyStartup = ConfigurationService.GetStartupParametersByConfigItem(config);
                ZapretConversionResult conversion = new Zapret1ToZapret2Converter().Convert(expandedLegacyStartup);
                issues.AddRange(conversion.Issues.Select(issue => new ConfigImportIssue(
                    issue.Severity == ZapretConversionIssueSeverity.Error
                        ? ConfigImportIssueSeverity.Error
                        : ConfigImportIssueSeverity.Warning,
                    issue.Code,
                    issue.Message,
                    result.SourcePath)));

                if (conversion.IsSuccessful)
                {
                    config.startup_string = conversion.StartupString;
                    config.jparams = [];
                    config.variables = [];
                    config.commaVars = null;
                    config.availableCommaVarsValues = null;
                }
                else
                {
                    config = null;
                }
            }

            if (config == null)
                return CreateRetargetedResult(result, target, null, issues);

            string version = ExecutablesEqual(result.Target.Executable, target.Executable) &&
                             config.target is { Count: > 1 } &&
                             !string.IsNullOrWhiteSpace(config.target[1])
                ? config.target[1]
                : ResolveTargetVersion(target);
            config.target = [target.ComponentId, version];
            config.packId = null;
        }

        return CreateRetargetedResult(result, target, config, issues);
    }

    public ConfigImportResult ApplyMissingFileResolutions(
        ConfigImportResult result,
        IEnumerable<ConfigImportMissingFileResolution> resolutions)
    {
        var missingFiles = result.MissingReferencedFiles
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<string, ConfigImportMissingFileResolution>(StringComparer.OrdinalIgnoreCase);
        var issues = result.Issues.ToList();

        foreach (ConfigImportMissingFileResolution resolution in resolutions)
        {
            string missingPath = Path.GetFullPath(resolution.MissingPath);
            if (!missingFiles.Contains(missingPath))
                continue;

            string? replacementPath = string.IsNullOrWhiteSpace(resolution.ReplacementPath)
                ? null
                : Path.GetFullPath(resolution.ReplacementPath);
            if (replacementPath != null && !File.Exists(replacementPath))
            {
                issues.Add(new(
                    ConfigImportIssueSeverity.Error,
                    "MISSING_FILE_REPLACEMENT_NOT_FOUND",
                    $"The selected replacement file was not found: {replacementPath}",
                    replacementPath));
            }

            normalized[missingPath] = new ConfigImportMissingFileResolution(missingPath, replacementPath);
        }

        return new ConfigImportResult
        {
            Config = result.Config,
            Target = result.Target,
            SourcePath = result.SourcePath,
            Issues = issues.AsReadOnly(),
            SourceFiles = result.SourceFiles,
            ReferencedFiles = result.ReferencedFiles,
            MissingReferencedFiles = result.MissingReferencedFiles,
            MissingFileResolutions = normalized.Values.ToList().AsReadOnly(),
            GeneratedFiles = result.GeneratedFiles,
        };
    }

    private static ConfigImportResult CreateRetargetedResult(
        ConfigImportResult result,
        ConfigImportTarget target,
        ConfigItem? config,
        IReadOnlyList<ConfigImportIssue> issues) =>
        new()
        {
            Config = config,
            Target = target,
            SourcePath = result.SourcePath,
            Issues = issues,
            SourceFiles = result.SourceFiles,
            ReferencedFiles = result.ReferencedFiles,
            MissingReferencedFiles = result.MissingReferencedFiles,
            MissingFileResolutions = result.MissingFileResolutions,
            GeneratedFiles = result.GeneratedFiles,
        };

    public IReadOnlyList<ConfigImportTarget> FindMatchingTargets(
        string filePath,
        IEnumerable<ConfigImportTarget> targets)
    {
        if (!File.Exists(filePath))
            return [];

        List<ConfigImportTarget> availableTargets = targets
            .GroupBy(target => target.ComponentId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        string extension = Path.GetExtension(filePath);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string? componentId = JSONConvertor.LoadJson<ConfigItem>(filePath)?.target?.FirstOrDefault();
                return availableTargets
                    .Where(target => string.Equals(target.ComponentId, componentId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        if (!extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return [];

        string text;
        try
        {
            text = File.ReadAllText(filePath);
        }
        catch
        {
            return [];
        }

        List<ConfigImportTarget> executableMatches = availableTargets
            .Where(target => ContainsExecutableReference(text, target.Executable))
            .ToList();
        if (executableMatches.Count > 0)
            return executableMatches;

        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return [];

        string? inferredExecutable = BuiltinVersionRegex().IsMatch(text) ||
                                     text.Contains("--lua-desync", StringComparison.OrdinalIgnoreCase)
            ? "winws2"
            : text.Contains("--dpi-desync", StringComparison.OrdinalIgnoreCase)
                ? "winws"
                : null;
        if (inferredExecutable == null)
            return [];

        return availableTargets
            .Where(target => NormalizeExecutableName(target.Executable)
                .Equals(inferredExecutable, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool ValidateRequest(
        string filePath,
        ConfigImportTarget target,
        out ConfigImportResult? errorResult)
    {
        var issues = new List<ConfigImportIssue>();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            issues.Add(new(ConfigImportIssueSeverity.Error, "FILE_NOT_FOUND", "Config source file was not found.", filePath));
        if (string.IsNullOrWhiteSpace(target.ComponentId))
            issues.Add(new(ConfigImportIssueSeverity.Error, "TARGET_REQUIRED", "A target component was not selected.", filePath));
        if (string.IsNullOrWhiteSpace(target.Executable))
            issues.Add(new(ConfigImportIssueSeverity.Error, "EXECUTABLE_REQUIRED", "The selected component has no executable in the database.", filePath));

        if (issues.Count == 0)
        {
            errorResult = null;
            return true;
        }

        errorResult = CreateResult(target, filePath, null, issues);
        return false;
    }

    private static ConfigImportResult ImportZapret(
        string filePath,
        ConfigImportTarget target,
        string executable)
    {
        ZapretConfigGeneration expectedGeneration = executable.Equals("winws2", StringComparison.OrdinalIgnoreCase)
            ? ZapretConfigGeneration.Winws2
            : ZapretConfigGeneration.Winws1;
        var importer = new ZapretConfigImportService();
        ZapretConfigImportResult result = importer.Import(
            filePath,
            new ZapretConfigImportOptions
            {
                CurrentVersionResolver = generation => generation == expectedGeneration
                    ? ResolveTargetVersion(target)
                    : null,
                DefaultTextConfigGeneration = expectedGeneration,
            });

        var issues = result.Issues.Select(issue => new ConfigImportIssue(
            issue.Severity == ZapretConfigImportIssueSeverity.Error
                ? ConfigImportIssueSeverity.Error
                : ConfigImportIssueSeverity.Warning,
            issue.Code,
            issue.Message,
            issue.SourcePath,
            issue.LineNumber)).ToList();

        IReadOnlyList<string> missingReferencedFiles = FilterComponentLuaFiles(
            result.MissingReferencedFiles,
            result.Config,
            filePath,
            target,
            issues);

        ConfigItem? config = result.Config;
        if (result.Generation.HasValue && result.Generation.Value != expectedGeneration)
        {
            issues.Add(new(
                ConfigImportIssueSeverity.Error,
                "EXECUTABLE_MISMATCH",
                $"The source starts {GetZapretExecutable(result.Generation.Value)}, but the selected component uses {executable}.",
                filePath));
            config = null;
        }

        if (config != null)
        {
            string version = config.target is { Count: > 1 } && !string.IsNullOrWhiteSpace(config.target[1])
                ? config.target[1]
                : ResolveTargetVersion(target);
            config.target = [target.ComponentId, version];
        }

        return new ConfigImportResult
        {
            Config = config,
            Target = target,
            SourcePath = filePath,
            Issues = issues.AsReadOnly(),
            SourceFiles = result.SourceFiles,
            ReferencedFiles = result.ReferencedFiles,
            MissingReferencedFiles = missingReferencedFiles,
            MissingFileResolutions = [],
            GeneratedFiles = result.GeneratedFiles
                .Select(file => new ConfigImportGeneratedFile(file.RelativePath, file.Content))
                .ToList()
                .AsReadOnly(),
        };
    }

    private static ConfigImportResult ImportGeneric(
        string filePath,
        ConfigImportTarget target,
        string executable)
    {
        var context = new GenericImportContext(target, filePath);
        try
        {
            string extension = Path.GetExtension(filePath);
            ConfigItem? config;
            if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                config = ImportGenericBatch(filePath, executable, context);
            }
            else if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                config = ImportGenericText(filePath, executable, context);
            }
            else if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                config = ImportJson(filePath, context);
            }
            else
            {
                context.AddError(
                    "UNSUPPORTED_FORMAT",
                    $"The '{extension}' file format is not supported.",
                    filePath);
                config = null;
            }

            return context.CreateResult(config);
        }
        catch (Exception exception)
        {
            context.AddError("IMPORT_FAILED", exception.Message, filePath);
            return context.CreateResult();
        }
    }

    private static IReadOnlyList<string> FilterComponentLuaFiles(
        IReadOnlyList<string> missingFiles,
        ConfigItem? config,
        string sourcePath,
        ConfigImportTarget target,
        List<ConfigImportIssue> issues)
    {
        if (config == null || string.IsNullOrWhiteSpace(target.Directory))
            return missingFiles;

        string sourceDirectory = Path.GetDirectoryName(sourcePath)!;
        var componentProvided = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string missingFile in missingFiles)
        {
            if (!Path.GetExtension(missingFile).Equals(".lua", StringComparison.OrdinalIgnoreCase))
                continue;

            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(sourceDirectory, missingFile);
            }
            catch
            {
                continue;
            }

            if (Path.IsPathRooted(relativePath) || relativePath.StartsWith("..", StringComparison.Ordinal))
                continue;
            if (UsesImportedCurrentDirectory(config, relativePath))
                continue;

            string componentPath = Path.GetFullPath(Path.Combine(target.Directory, relativePath));
            if (File.Exists(componentPath))
                componentProvided.Add(Path.GetFullPath(missingFile));
        }

        if (componentProvided.Count == 0)
            return missingFiles;

        issues.RemoveAll(issue =>
            issue.Code == "REFERENCED_FILE_NOT_FOUND" &&
            componentProvided.Any(path => issue.Message.Contains(path, StringComparison.OrdinalIgnoreCase)));
        return missingFiles
            .Where(path => !componentProvided.Contains(Path.GetFullPath(path)))
            .ToList()
            .AsReadOnly();
    }

    private static bool UsesImportedCurrentDirectory(ConfigItem config, string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/').TrimStart('/');
        string pattern = $@"\$GETCURRENTDIR\(\)[\\/]+{Regex.Escape(normalized).Replace("/", @"[\\/]+")}";
        var values = new List<string?> { config.startup_string };
        if (config.variables != null)
            values.AddRange(config.variables);
        if (config.commaVars != null)
            values.AddRange(config.commaVars.Values);
        if (config.availableCommaVarsValues != null)
            values.AddRange(config.availableCommaVarsValues.SelectMany(item => item.Values ?? []));
        return values.Any(value => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase));
    }

    private static ConfigItem? ImportGenericBatch(
        string filePath,
        string executable,
        GenericImportContext context)
    {
        context.AddSourceFile(filePath);
        IReadOnlyList<LogicalLine> lines = ReadLogicalLines(filePath);
        var variables = new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase);
        var launches = new List<(string Arguments, int LineNumber, Dictionary<string, MutableVariable> Variables)>();

        foreach (LogicalLine logicalLine in lines)
        {
            string line = logicalLine.Text.Trim();
            if (line.Length == 0)
                continue;

            if (TryParseCommentedSet(line, out string alternativeName, out string alternativeValue))
            {
                SetVariable(variables, alternativeName, alternativeValue, true);
                continue;
            }
            if (IsComment(line))
                continue;
            if (TryParseSet(line, out string variableName, out string variableValue))
            {
                SetVariable(variables, variableName, variableValue, false);
                continue;
            }
            if (TryExtractArguments(line, executable, filePath, variables, out string launchArguments))
                launches.Add((launchArguments, logicalLine.LineNumber, CloneVariables(variables)));
        }

        if (launches.Count == 0)
        {
            context.AddError(
                "EXECUTABLE_LAUNCH_NOT_FOUND",
                $"The command file does not contain a supported launch of {executable}.",
                filePath);
            return null;
        }
        if (launches.Count > 1)
        {
            context.AddError(
                "MULTIPLE_EXECUTABLE_LAUNCHES",
                $"The command file contains more than one launch of {executable}; automatic selection is ambiguous.",
                filePath);
            return null;
        }

        var selected = launches[0];
        string arguments = selected.Arguments;
        string argumentsSourcePath = filePath;
        if (TryResolveTextInclude(arguments, filePath, selected.Variables, out string includePath))
        {
            context.AddSourceFile(includePath);
            arguments = ReadTextArguments(includePath, executable, selected.Variables, context);
            argumentsSourcePath = includePath;
        }

        Dictionary<string, MutableVariable> usedVariables = GetUsedVariables(
            arguments,
            selected.Variables,
            context,
            filePath);
        CollectReferencedFiles(arguments, argumentsSourcePath, selected.Variables, context);
        foreach (MutableVariable variable in usedVariables.Values)
        {
            CollectReferencedFiles(variable.Value, filePath, selected.Variables, context);
            foreach (string alternative in variable.Alternatives)
                CollectReferencedFiles(alternative, filePath, selected.Variables, context);
        }

        return CreateGenericConfig(
            Path.GetFileNameWithoutExtension(filePath),
            arguments,
            usedVariables,
            context.Target);
    }

    private static ConfigItem? ImportGenericText(
        string filePath,
        string executable,
        GenericImportContext context)
    {
        context.AddSourceFile(filePath);
        string arguments = ReadTextArguments(
            filePath,
            executable,
            new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase),
            context);
        if (string.IsNullOrWhiteSpace(arguments))
        {
            context.AddError("TEXT_CONFIG_EMPTY", "The text config does not contain launch arguments.", filePath);
            return null;
        }

        CollectReferencedFiles(
            arguments,
            filePath,
            new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase),
            context);
        return CreateGenericConfig(
            ReadTextConfigName(filePath) ?? Path.GetFileNameWithoutExtension(filePath),
            arguments,
            new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase),
            context.Target);
    }

    private static ConfigItem? ImportJson(string filePath, GenericImportContext context)
    {
        context.AddSourceFile(filePath);
        ConfigItem? config = JSONConvertor.LoadJson<ConfigItem>(filePath);
        if (config == null || string.IsNullOrWhiteSpace(config.startup_string))
        {
            OldConfigItem? oldConfig = JSONConvertor.LoadJson<OldConfigItem>(filePath);
            if (string.IsNullOrWhiteSpace(oldConfig?.custom_parameters))
            {
                context.AddError("JSON_CONFIG_INVALID", "The JSON file is not a supported Config.", filePath);
                return null;
            }

            config = new ConfigItem
            {
                meta = "pUC:v1.0",
                name = Path.GetFileNameWithoutExtension(filePath),
                startup_string = oldConfig.custom_parameters,
                jparams = [],
                variables = [],
            };
        }

        config.target = [context.Target.ComponentId, ResolveTargetVersion(context.Target)];
        config.name ??= Path.GetFileNameWithoutExtension(filePath);
        return config;
    }

    private static ConfigItem CreateGenericConfig(
        string name,
        string arguments,
        IReadOnlyDictionary<string, MutableVariable> variables,
        ConfigImportTarget target)
    {
        Dictionary<string, string>? commaVariables = variables.Count == 0
            ? null
            : variables.ToDictionary(
                pair => pair.Key,
                pair => TranslateBatchValue(pair.Value.Value),
                StringComparer.OrdinalIgnoreCase);
        commaVariables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddUnrecognizedCommaVariables(arguments, variables, commaVariables);
        if (commaVariables.Count == 0)
            commaVariables = null;
        List<AvailableVarValues>? alternatives = variables.Values
            .Where(variable => variable.Alternatives.Count > 0)
            .Select(variable => new AvailableVarValues
            {
                VarName = variable.Name,
                CurrentValueIndex = 0,
                Values = variable.GetSelectableValues()
                    .Select(TranslateBatchValue)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            })
            .ToList();

        return new ConfigItem
        {
            meta = "pUC:v1.0",
            name = name,
            target = [target.ComponentId, ResolveTargetVersion(target)],
            commaVars = commaVariables,
            availableCommaVarsValues = alternatives is { Count: > 0 } ? alternatives : null,
            jparams = [],
            variables = [],
            startup_string = NormalizeWhitespace(TranslateBatchValue(arguments)),
        };
    }

    private static void AddUnrecognizedCommaVariables(
        string arguments,
        IReadOnlyDictionary<string, MutableVariable> variables,
        IDictionary<string, string> commaVariables)
    {
        var recognized = variables.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> values = new[] { arguments }
            .Concat(variables.Values.Select(variable => variable.Value))
            .Concat(variables.Values.SelectMany(variable => variable.Alternatives));

        foreach (string value in values)
        {
            foreach (string name in GetVariableNames(value))
            {
                if (!recognized.Contains(name) && !IsBuiltinBatchVariable(name))
                    commaVariables.TryAdd(name, "$EMPTY");
            }
        }
    }

    private static string ReadTextArguments(
        string filePath,
        string executable,
        IReadOnlyDictionary<string, MutableVariable> variables,
        GenericImportContext context)
    {
        IReadOnlyList<LogicalLine> lines = ReadLogicalLines(filePath);
        var content = new StringBuilder();
        foreach (LogicalLine line in lines)
        {
            string value = line.Text.Trim();
            if (value.Length == 0 || IsComment(value))
                continue;

            if (TryExtractArguments(value, executable, filePath, variables, out string extracted))
                value = extracted;
            if (content.Length > 0)
                content.Append(' ');
            content.Append(value);
        }

        return content.ToString();
    }

    private static string? ReadTextConfigName(string filePath)
    {
        foreach (string line in File.ReadLines(filePath))
        {
            Match match = ConfigNameRegex().Match(line);
            if (match.Success)
                return match.Groups["value"].Value.Trim();
        }
        return null;
    }

    private static bool TryResolveTextInclude(
        string arguments,
        string sourcePath,
        IReadOnlyDictionary<string, MutableVariable> variables,
        out string includePath)
    {
        includePath = string.Empty;
        List<CommandToken> tokens = Tokenize(arguments);
        if (tokens.Count != 1)
            return false;

        string expanded = ExpandBatchValue(tokens[0].Value.TrimStart('@', '$'), sourcePath, variables);
        string candidate = Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(Path.GetDirectoryName(sourcePath)!, expanded);
        try
        {
            candidate = Path.GetFullPath(candidate);
        }
        catch
        {
            return false;
        }

        if (!Path.GetExtension(candidate).Equals(".txt", StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
            return false;
        includePath = candidate;
        return true;
    }

    private static Dictionary<string, MutableVariable> GetUsedVariables(
        string input,
        IReadOnlyDictionary<string, MutableVariable> variables,
        GenericImportContext context,
        string sourcePath)
    {
        var result = new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>(GetVariableNames(input));
        var inspected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (pending.Count > 0)
        {
            string name = pending.Dequeue();
            if (!inspected.Add(name))
                continue;
            if (!variables.TryGetValue(name, out MutableVariable? variable))
            {
                if (!IsBuiltinBatchVariable(name))
                    context.AddWarning("UNRESOLVED_VARIABLE", $"Batch variable '{name}' could not be resolved.", sourcePath);
                continue;
            }

            result[name] = variable.Clone();
            foreach (string nested in GetVariableNames(variable.Value))
                pending.Enqueue(nested);
            foreach (string alternative in variable.Alternatives)
            {
                foreach (string nested in GetVariableNames(alternative))
                    pending.Enqueue(nested);
            }
        }
        return result;
    }

    private static IEnumerable<string> GetVariableNames(string input) =>
        BatchVariableRegex().Matches(input)
            .Select(match => match.Groups["name"].Value)
            .Where(name => !name.StartsWith('~'));

    private static bool TryExtractArguments(
        string line,
        string executable,
        string sourcePath,
        IReadOnlyDictionary<string, MutableVariable> variables,
        out string arguments)
    {
        arguments = string.Empty;
        foreach (CommandToken token in Tokenize(line))
        {
            string expanded = ExpandBatchValue(token.Value, sourcePath, variables).Trim('"');
            string fileName = Path.GetFileName(expanded.Replace('/', Path.DirectorySeparatorChar));
            if (!NormalizeExecutableName(fileName).Equals(executable, StringComparison.OrdinalIgnoreCase))
                continue;

            arguments = TrimShellTail(line[token.End..]).Trim();
            return arguments.Length > 0;
        }
        return false;
    }

    private static List<CommandToken> Tokenize(string input)
    {
        var result = new List<CommandToken>();
        int index = 0;
        while (index < input.Length)
        {
            while (index < input.Length && char.IsWhiteSpace(input[index]))
                index++;
            if (index >= input.Length)
                break;

            int start = index;
            var value = new StringBuilder();
            bool quoted = false;
            while (index < input.Length)
            {
                char current = input[index];
                if (current == '"')
                {
                    quoted = !quoted;
                    index++;
                    continue;
                }
                if (!quoted && char.IsWhiteSpace(current))
                    break;
                value.Append(current);
                index++;
            }
            result.Add(new CommandToken(value.ToString(), start, index));
        }
        return result;
    }

    private static void CollectReferencedFiles(
        string input,
        string sourcePath,
        IReadOnlyDictionary<string, MutableVariable> variables,
        GenericImportContext context)
    {
        string expanded = ExpandBatchValue(input, sourcePath, variables);
        foreach (Match match in ResourcePathRegex().Matches(expanded))
        {
            string rawPath = match.Groups["quotedPath"].Success
                ? match.Groups["quotedPath"].Value
                : match.Groups["plainPath"].Value;
            rawPath = rawPath.TrimStart('@', '$');
            string candidate = Path.IsPathRooted(rawPath)
                ? rawPath
                : Path.Combine(Path.GetDirectoryName(sourcePath)!, rawPath);
            try
            {
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                    context.AddReferencedFile(fullPath);
                else
                    context.AddMissingReferencedFile(fullPath, sourcePath);
            }
            catch
            {
                context.AddWarning("INVALID_RESOURCE_PATH", $"A referenced path could not be resolved: {rawPath}", sourcePath);
            }
        }
    }

    private static string ExpandBatchValue(
        string value,
        string sourcePath,
        IReadOnlyDictionary<string, MutableVariable> variables,
        int depth = 0)
    {
        if (depth > 16)
            return value;

        string directory = Path.GetDirectoryName(sourcePath)! + Path.DirectorySeparatorChar;
        string result = value
            .Replace("%~dp0", directory, StringComparison.OrdinalIgnoreCase)
            .Replace("%~f0", sourcePath, StringComparison.OrdinalIgnoreCase)
            .Replace("%~n0", Path.GetFileNameWithoutExtension(sourcePath), StringComparison.OrdinalIgnoreCase);

        return BatchVariableRegex().Replace(result, match =>
        {
            string name = match.Groups["name"].Value;
            if (variables.TryGetValue(name, out MutableVariable? variable))
                return ExpandBatchValue(variable.Value, sourcePath, variables, depth + 1);
            return Environment.GetEnvironmentVariable(name) ?? match.Value;
        });
    }

    private static string TranslateBatchValue(string value) =>
        value.Replace("%~dp0", "$GETCURRENTDIR()/", StringComparison.OrdinalIgnoreCase)
            .Replace('\\', '/');

    private static IReadOnlyList<LogicalLine> ReadLogicalLines(string filePath)
    {
        string[] physicalLines = File.ReadAllLines(filePath, Encoding.UTF8);
        var result = new List<LogicalLine>();
        var current = new StringBuilder();
        int currentLine = 1;
        for (int index = 0; index < physicalLines.Length; index++)
        {
            string physicalLine = physicalLines[index].TrimEnd();
            bool continues = HasContinuationCaret(physicalLine);
            if (current.Length == 0)
                currentLine = index + 1;
            if (continues)
                physicalLine = physicalLine[..^1];
            if (current.Length > 0)
                current.Append(' ');
            current.Append(physicalLine.Trim());
            if (!continues)
            {
                result.Add(new(currentLine, current.ToString()));
                current.Clear();
            }
        }
        if (current.Length > 0)
            result.Add(new(currentLine, current.ToString()));
        return result;
    }

    private static bool HasContinuationCaret(string line)
    {
        int count = 0;
        for (int index = line.Length - 1; index >= 0 && line[index] == '^'; index--)
            count++;
        return count % 2 == 1;
    }

    private static bool TryParseSet(string line, out string name, out string value)
    {
        Match match = SetRegex().Match(line);
        name = match.Success ? match.Groups["name"].Value.Trim() : string.Empty;
        value = match.Success ? match.Groups["value"].Value.Trim().TrimEnd('"') : string.Empty;
        return match.Success && name.Length > 0;
    }

    private static bool TryParseCommentedSet(string line, out string name, out string value)
    {
        Match match = CommentedSetRegex().Match(line);
        return TryParseSet(match.Success ? match.Groups["set"].Value : string.Empty, out name, out value);
    }

    private static bool IsComment(string line) =>
        line.StartsWith("::", StringComparison.Ordinal) ||
        line.StartsWith("rem ", StringComparison.OrdinalIgnoreCase) ||
        line.Equals("rem", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith('#');

    private static void SetVariable(
        IDictionary<string, MutableVariable> variables,
        string name,
        string value,
        bool alternative)
    {
        if (!variables.TryGetValue(name, out MutableVariable? variable))
        {
            variable = new(name, alternative ? string.Empty : value);
            variables[name] = variable;
        }
        if (alternative)
            variable.AddAlternative(value);
        else
            variable.Value = value;
    }

    private static Dictionary<string, MutableVariable> CloneVariables(
        IReadOnlyDictionary<string, MutableVariable> variables) =>
        variables.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.OrdinalIgnoreCase);

    private static string TrimShellTail(string input)
    {
        bool quoted = false;
        for (int index = 0; index < input.Length; index++)
        {
            if (input[index] == '"')
                quoted = !quoted;
            if (!quoted && input[index] is '>' or '<' or '|')
                return input[..index];
            if (!quoted && input[index] == '&')
                return input[..index];
        }
        return input;
    }

    private static string NormalizeWhitespace(string input) =>
        WhitespaceRegex().Replace(input, " ").Trim();

    private static string ResolveTargetVersion(ConfigImportTarget target) =>
        string.IsNullOrWhiteSpace(target.Version) ? CurrentVersionPlaceholder : target.Version;

    private static string NormalizeExecutableName(string executable)
    {
        string name = Path.GetFileName(executable.Trim().Trim('"'));
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    private static bool ExecutablesEqual(string left, string right) =>
        NormalizeExecutableName(left).Equals(NormalizeExecutableName(right), StringComparison.OrdinalIgnoreCase);

    private static bool IsZapretExecutable(string executable) =>
        executable.Equals("winws", StringComparison.OrdinalIgnoreCase) ||
        executable.Equals("winws2", StringComparison.OrdinalIgnoreCase);

    private static string GetZapretExecutable(ZapretConfigGeneration generation) =>
        generation == ZapretConfigGeneration.Winws2 ? "winws2" : "winws";

    private static bool ContainsExecutableReference(string text, string executable)
    {
        string name = Regex.Escape(NormalizeExecutableName(executable));
        return Regex.IsMatch(
            text,
            $@"(?i)(?:%~dp0|(?<![A-Za-z0-9_.-])){name}(?:\.exe)?(?![A-Za-z0-9_.-])");
    }

    private static bool IsBuiltinBatchVariable(string name) =>
        name.Equals("CD", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ERRORLEVEL", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("DATE", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("TIME", StringComparison.OrdinalIgnoreCase);

    private static string? TryGetFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return null; }
    }

    private static ConfigImportResult CreateResult(
        ConfigImportTarget target,
        string sourcePath,
        ConfigItem? config,
        IReadOnlyList<ConfigImportIssue> issues) =>
        new()
        {
            Config = config,
            Target = target,
            SourcePath = sourcePath,
            Issues = issues,
            SourceFiles = File.Exists(sourcePath) ? [sourcePath] : [],
            ReferencedFiles = [],
            MissingReferencedFiles = [],
            MissingFileResolutions = [],
            GeneratedFiles = [],
        };

    private static ConfigImportTarget CreateTarget(DatabaseStoreItem item) => new(
        item.Id!,
        item.ShortName ?? item.Name ?? item.Id!,
        item.Executable!,
        item.CurrentVersion,
        item.Directory);

    private ConfigImportTarget GetConfigImportTarget(string filePath, string requestedTarget)
    {
        var components = DatabaseHelper.Instance.GetItemsByType("component");
        List<ConfigImportTarget> targets = [.. components.Select(CreateTarget)];

        var matches = FindMatchingTargets(filePath, targets);
        return matches.FirstOrDefault()
            ?? FindRequestedTarget(targets, requestedTarget)
            ?? targets.FirstOrDefault()
            ?? new ConfigImportTarget(string.Empty, string.Empty, string.Empty, null);
    }

    private ConfigImportTarget? FindRequestedTarget(IReadOnlyList<ConfigImportTarget> targets, string requestedTarget) =>
        targets.FirstOrDefault(target =>
            string.Equals(target.ComponentId, requestedTarget, StringComparison.OrdinalIgnoreCase));

    private sealed class GenericImportContext(ConfigImportTarget target, string sourcePath)
    {
        private readonly List<ConfigImportIssue> _issues = [];
        private readonly List<string> _sourceFiles = [];
        private readonly HashSet<string> _sourceFileSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _referencedFiles = [];
        private readonly HashSet<string> _referencedFileSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _missingReferencedFiles = [];
        private readonly HashSet<string> _missingReferencedFileSet = new(StringComparer.OrdinalIgnoreCase);

        public ConfigImportTarget Target { get; } = target;

        public void AddError(string code, string message, string? path, int? line = null) =>
            _issues.Add(new(ConfigImportIssueSeverity.Error, code, message, path, line));

        public void AddWarning(string code, string message, string? path, int? line = null) =>
            _issues.Add(new(ConfigImportIssueSeverity.Warning, code, message, path, line));

        public void AddSourceFile(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (_sourceFileSet.Add(fullPath))
                _sourceFiles.Add(fullPath);
        }

        public void AddReferencedFile(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (_referencedFileSet.Add(fullPath))
                _referencedFiles.Add(fullPath);
        }

        public void AddMissingReferencedFile(string path, string pathSource)
        {
            string fullPath = Path.GetFullPath(path);
            if (!_missingReferencedFileSet.Add(fullPath))
                return;
            _missingReferencedFiles.Add(fullPath);
            AddWarning(
                "REFERENCED_FILE_NOT_FOUND",
                $"A referenced file was not found: {fullPath}",
                pathSource);
        }

        public ConfigImportResult CreateResult(ConfigItem? config = null) => new()
        {
            Config = config,
            Target = Target,
            SourcePath = sourcePath,
            Issues = _issues.AsReadOnly(),
            SourceFiles = _sourceFiles.AsReadOnly(),
            ReferencedFiles = _referencedFiles.AsReadOnly(),
            MissingReferencedFiles = _missingReferencedFiles.AsReadOnly(),
            MissingFileResolutions = [],
            GeneratedFiles = [],
        };
    }

    private sealed class MutableVariable(string name, string value)
    {
        public string Name { get; } = name;
        public string Value { get; set; } = value;
        public List<string> Alternatives { get; } = [];

        public void AddAlternative(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                !string.Equals(Value, value, StringComparison.Ordinal) &&
                !Alternatives.Contains(value, StringComparer.Ordinal))
            {
                Alternatives.Add(value);
            }
        }

        public IEnumerable<string> GetSelectableValues()
        {
            if (!string.IsNullOrWhiteSpace(Value))
                yield return Value;
            foreach (string alternative in Alternatives)
                yield return alternative;
        }

        public MutableVariable Clone()
        {
            var clone = new MutableVariable(Name, Value);
            clone.Alternatives.AddRange(Alternatives);
            return clone;
        }
    }

    private sealed record LogicalLine(int LineNumber, string Text);
    private sealed record CommandToken(string Value, int Start, int End);

    [GeneratedRegex("""(?i)^\s*@?set\s+(?<quoted>")?(?<name>[^="]+)=(?<value>.*)$""")]
    private static partial Regex SetRegex();

    [GeneratedRegex(@"(?i)^\s*(?:rem\s+|::\s*)(?<set>set\s+.+)$")]
    private static partial Regex CommentedSetRegex();

    [GeneratedRegex(@"%(?<name>[A-Za-z0-9_~]+)%", RegexOptions.IgnoreCase)]
    private static partial Regex BatchVariableRegex();

    [GeneratedRegex(@"(?im)^\s*#\s*(?:config|preset|activepreset)\s*[:=]\s*(?<value>.+?)\s*$")]
    private static partial Regex ConfigNameRegex();

    [GeneratedRegex(
        """(?ix)(?:["'](?<quotedPath>[^"']+\.(?:bin|txt|lua|dat|conf|cfg|json))['"]|(?<plainPath>[^\s"'=]+\.(?:bin|txt|lua|dat|conf|cfg|json)))""")]
    private static partial Regex ResourcePathRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^\s*#\s*BuiltinVersion\s*:\s*\S+", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex BuiltinVersionRegex();
}
