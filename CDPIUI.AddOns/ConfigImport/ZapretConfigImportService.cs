using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Statically imports zapret batch scripts and text configs without executing them.
/// This importer deliberately does not perform winws1 to winws2 conversion.
/// </summary>
public sealed partial class ZapretConfigImportService
{
    private const string ConfigMeta = "pUC:v1.0";

    public ZapretConfigImportResult Import(
        string filePath,
        ZapretConfigImportOptions? options = null)
    {
        options ??= new ZapretConfigImportOptions();
        var context = new ImportContext(options);

        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                context.AddError("FILE_NOT_FOUND", "Config source file was not found.", filePath);
                return context.CreateResult();
            }

            string fullPath = Path.GetFullPath(filePath);
            string extension = Path.GetExtension(fullPath);

            ImportedPayload? payload;
            if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                payload = ImportBatch(fullPath, context);
            }
            else if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                payload = ImportText(fullPath, expectedGeneration: null, context, depth: 0);
            }
            else
            {
                context.AddError(
                    "UNSUPPORTED_FORMAT",
                    $"The '{extension}' file format is not supported by the zapret importer.",
                    fullPath);
                return context.CreateResult();
            }

            if (payload == null)
                return context.CreateResult();

            string version = ResolveVersion(payload, context.Options);
            string targetId = payload.Generation == ZapretConfigGeneration.Winws2
                ? HardcodedItemIds.ComponentIds[Components.Zapret2]
                : HardcodedItemIds.ComponentIds[Components.Zapret];

            var conditionalVariableNames = payload.ConditionalVariables
                .Select(variable => variable.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string>? commaVariables = payload.Variables.Values
                .Where(variable =>
                    !conditionalVariableNames.Contains(variable.Name) &&
                    variable.Alternatives.Count > 0)
                .ToDictionary(
                    variable => variable.Name,
                    variable => TranslateBatchValueToConfig(variable.Value),
                    StringComparer.OrdinalIgnoreCase);
            AddUnrecognizedCommaVariables(payload, commaVariables);
            if (commaVariables.Count == 0)
                commaVariables = null;

            List<AvailableVarValues>? alternatives = payload.Variables.Values
                .Where(variable =>
                    !conditionalVariableNames.Contains(variable.Name) &&
                    variable.Alternatives.Count > 0)
                .Select(variable => new AvailableVarValues
                {
                    VarName = variable.Name,
                    CurrentValueIndex = 0,
                    Values = variable.GetSelectableValues()
                        .Select(TranslateBatchValueToConfig)
                        .Distinct(StringComparer.Ordinal)
                        .ToList(),
                })
                .ToList();

            var jparams = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var configVariables = new List<string>();
            foreach (ConditionalVariable variable in payload.ConditionalVariables)
            {
                jparams[variable.ParameterName] = variable.IsEnabled;
                configVariables.Add(
                    $"%{variable.Name}%=$LOCALCONDITION({variable.ParameterName}==true ? " +
                    $"{TranslateBatchValueToConfig(variable.EnabledValue)} $SEPARATOR " +
                    $"{TranslateBatchValueToConfig(variable.DisabledValue)})");
            }

            configVariables.AddRange(payload.Variables.Values
                .Where(variable =>
                    !conditionalVariableNames.Contains(variable.Name) &&
                    variable.Alternatives.Count == 0)
                .Select(variable =>
                    $"%{variable.Name}%={TranslateBatchValueToConfig(variable.Value)}"));

            var config = new ConfigItem
            {
                meta = ConfigMeta,
                name = payload.Name,
                target = [targetId, version],
                commaVars = commaVariables,
                availableCommaVarsValues = alternatives is { Count: > 0 } ? alternatives : null,
                jparams = jparams,
                variables = configVariables,
                startup_string = NormalizeWhitespace(TranslateBatchValueToConfig(payload.StartupString)),
            };

            return context.CreateResult(config, payload.Generation, payload.Variables.Values);
        }
        catch (Exception exception)
        {
            context.AddError("IMPORT_FAILED", exception.Message, filePath);
            return context.CreateResult();
        }
    }

    private static void AddUnrecognizedCommaVariables(
        ImportedPayload payload,
        IDictionary<string, string> commaVariables)
    {
        var recognized = payload.Variables.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> values = new[] { payload.StartupString }
            .Concat(payload.Variables.Values.Select(variable => variable.Value))
            .Concat(payload.Variables.Values.SelectMany(variable => variable.Alternatives));

        foreach (string value in values)
        {
            foreach (Match match in BatchVariableRegex().Matches(value))
            {
                string name = match.Groups["name"].Value;
                if (!name.StartsWith('~') && !recognized.Contains(name))
                    commaVariables.TryAdd(name, "$EMPTY");
            }
        }
    }

    private static ImportedPayload? ImportBatch(string filePath, ImportContext context)
    {
        context.AddSourceFile(filePath);
        IReadOnlyList<LogicalLine> lines;

        try
        {
            lines = ReadBatchLogicalLines(filePath);
        }
        catch (Exception exception)
        {
            context.AddError("BATCH_READ_FAILED", exception.Message, filePath);
            return null;
        }

        var variables = new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase);
        foreach (var predefined in context.Options.PredefinedVariables)
            SetVariable(variables, predefined.Key, predefined.Value, isAlternative: false);

        var inspectedCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var launches = new List<(BatchLaunch Launch, Dictionary<string, MutableVariable> Variables)>();

        foreach (LogicalLine logicalLine in lines)
        {
            string line = logicalLine.Text.Trim();
            if (line.Length == 0)
                continue;

            if (TryParseCommentedSet(line, out string alternativeName, out string alternativeValue))
            {
                SetVariable(variables, alternativeName, alternativeValue, isAlternative: true);
                continue;
            }

            if (IsComment(line))
                continue;

            if (TryParseSet(line, out string variableName, out string variableValue))
            {
                SetVariable(variables, variableName, variableValue, isAlternative: false);
                continue;
            }

            if (TryGetCalledScript(line, filePath, variables, out CalledScript calledScript))
            {
                CollectCalledScriptVariables(
                    calledScript,
                    variables,
                    inspectedCalls,
                    context,
                    depth: 0);
            }

            if (TryExtractLaunch(line, logicalLine.LineNumber, out BatchLaunch launch))
                launches.Add((launch, CloneVariables(variables)));
        }

        if (launches.Count == 0)
        {
            context.AddError(
                "WINWS_LAUNCH_NOT_FOUND",
                "The batch file does not contain a supported winws or winws2 launch.",
                filePath);
            return null;
        }

        if (launches.Count > 1)
        {
            context.AddError(
                "MULTIPLE_WINWS_LAUNCHES",
                "The batch file contains more than one winws launch. Automatic selection would be ambiguous.",
                filePath);
            return null;
        }

        BatchLaunch selected = launches[0].Launch;
        Dictionary<string, MutableVariable> snapshot = launches[0].Variables;
        IncludeExpansion expansion = ExpandConfigIncludes(
            selected.Arguments,
            filePath,
            selected.Generation,
            snapshot,
            context,
            depth: 0);

        ZapretConfigGeneration? detected = DetectGeneration(expansion.Text, expansion.BuiltinVersion);
        if (detected.HasValue && detected.Value != selected.Generation)
        {
            context.AddError(
                "GENERATION_MISMATCH",
                $"The batch file starts {GetExecutableName(selected.Generation)}, but its arguments look like a {GetExecutableName(detected.Value)} config.",
                filePath,
                selected.LineNumber);
            return null;
        }

        string startupString = NormalizeDelayedVariableSyntax(expansion.Text);
        Dictionary<string, MutableVariable> usedVariables = GetUsedVariables(startupString, snapshot, context, filePath);
        NormalizeDelayedVariableSyntax(usedVariables.Values);
        startupString = CreateActiveFileSelectors(startupString, filePath, snapshot, usedVariables);
        IReadOnlyList<ConditionalVariable> conditionalVariables = CreateConditionalVariables(usedVariables);
        CollectReferencedFiles(startupString, filePath, snapshot, context);
        foreach (MutableVariable variable in usedVariables.Values)
        {
            CollectReferencedFiles(variable.Value, filePath, snapshot, context);
            foreach (string alternative in variable.Alternatives)
                CollectReferencedFiles(alternative, filePath, snapshot, context);
        }

        return new ImportedPayload(
            selected.Generation,
            startupString,
            expansion.Name ?? Path.GetFileNameWithoutExtension(filePath),
            selected.Generation == ZapretConfigGeneration.Winws2 ? expansion.BuiltinVersion : null,
            usedVariables,
            conditionalVariables);
    }

    private static ImportedPayload? ImportText(
        string filePath,
        ZapretConfigGeneration? expectedGeneration,
        ImportContext context,
        int depth)
    {
        if (depth > context.Options.MaxIncludeDepth)
        {
            context.AddError(
                "INCLUDE_DEPTH_EXCEEDED",
                $"Config include depth exceeds the allowed limit of {context.Options.MaxIncludeDepth}.",
                filePath);
            return null;
        }

        string fullPath = Path.GetFullPath(filePath);
        if (!context.EnterInclude(fullPath))
        {
            context.AddError("INCLUDE_CYCLE", "A cycle was detected in config file includes.", fullPath);
            return null;
        }

        try
        {
            context.AddSourceFile(fullPath);
            TextConfig textConfig;
            try
            {
                textConfig = ParseTextConfig(fullPath);
            }
            catch (Exception exception)
            {
                context.AddError("TEXT_CONFIG_READ_FAILED", exception.Message, fullPath);
                return null;
            }

            if (string.IsNullOrWhiteSpace(textConfig.StartupString) ||
                (!textConfig.StartupString.Contains("--", StringComparison.Ordinal) &&
                 !StandaloneAtFileRegex().IsMatch(textConfig.StartupString)))
            {
                context.AddError(
                    "TEXT_CONFIG_HAS_NO_OPTIONS",
                    "The text file does not contain winws command-line options.",
                    fullPath);
                return null;
            }

            ZapretConfigGeneration? detected = DetectGeneration(textConfig.StartupString, textConfig.BuiltinVersion);
            ZapretConfigGeneration generation;

            if (expectedGeneration.HasValue)
            {
                generation = expectedGeneration.Value;
                if (detected.HasValue && detected.Value != generation)
                {
                    context.AddError(
                        "GENERATION_MISMATCH",
                        $"The referenced text config looks like {GetExecutableName(detected.Value)}, but {GetExecutableName(generation)} was expected.",
                        fullPath);
                    return null;
                }
            }
            else
            {
                if (!detected.HasValue)
                {
                    if (!context.Options.DefaultTextConfigGeneration.HasValue)
                    {
                        context.AddError(
                            "CONFIG_GENERATION_UNKNOWN",
                            "The text file does not contain enough information to distinguish winws1 from winws2.",
                            fullPath);
                        return null;
                    }

                    generation = context.Options.DefaultTextConfigGeneration.Value;
                }
                else
                {
                    generation = detected.Value;
                }
            }

            IncludeExpansion expansion = ExpandConfigIncludes(
                textConfig.StartupString,
                fullPath,
                generation,
                new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase),
                context,
                depth);
            CollectReferencedFiles(
                expansion.Text,
                fullPath,
                new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase),
                context);

            return new ImportedPayload(
                generation,
                expansion.Text,
                textConfig.Name ?? expansion.Name ?? Path.GetFileNameWithoutExtension(fullPath),
                textConfig.BuiltinVersion ?? expansion.BuiltinVersion,
                new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase),
                []);
        }
        finally
        {
            context.LeaveInclude(fullPath);
        }
    }

    private static IncludeExpansion ExpandConfigIncludes(
        string input,
        string referringFile,
        ZapretConfigGeneration expectedGeneration,
        IReadOnlyDictionary<string, MutableVariable> variables,
        ImportContext context,
        int depth)
    {
        if (depth > context.Options.MaxIncludeDepth)
        {
            context.AddError(
                "INCLUDE_DEPTH_EXCEEDED",
                $"Config include depth exceeds the allowed limit of {context.Options.MaxIncludeDepth}.",
                referringFile);
            return new IncludeExpansion(input, null, null);
        }

        MatchCollection matches = StandaloneAtFileRegex().Matches(input);
        if (matches.Count == 0)
            return new IncludeExpansion(input, null, null);

        foreach (Match match in matches)
        {
            string rawPath = match.Groups["quoted"].Success
                ? match.Groups["quoted"].Value
                : match.Groups["plain"].Value;

            string expandedPath = ExpandBatchValue(rawPath, referringFile, variables);
            string candidatePath = Path.IsPathRooted(expandedPath)
                ? expandedPath
                : Path.Combine(Path.GetDirectoryName(referringFile)!, expandedPath);

            string fullCandidatePath;
            try
            {
                fullCandidatePath = Path.GetFullPath(candidatePath);
            }
            catch
            {
                continue;
            }

            if (!File.Exists(fullCandidatePath))
            {
                context.AddError(
                    "INCLUDED_CONFIG_NOT_FOUND",
                    $"Referenced config file '{rawPath}' was not found.",
                    referringFile);
                continue;
            }

            if (!LooksLikeTextConfig(fullCandidatePath))
                continue;

            ImportedPayload? nested = ImportText(
                fullCandidatePath,
                expectedGeneration,
                context,
                depth + 1);

            if (nested != null)
            {
                Group tokenGroup = match.Groups["token"];
                string remainingArguments = string.Concat(
                    input.AsSpan(0, tokenGroup.Index),
                    input.AsSpan(tokenGroup.Index + tokenGroup.Length)).Trim();
                if (remainingArguments.Length > 0)
                {
                    context.AddWarning(
                        "CONFIG_INCLUDE_IGNORES_OTHER_ARGUMENTS",
                        "A response-file config replaces the command line; other arguments are ignored by winws.",
                        referringFile);
                }

                return new IncludeExpansion(
                    nested.StartupString,
                    nested.Name,
                    nested.BuiltinVersion);
            }
        }

        return new IncludeExpansion(input, null, null);
    }

    private static TextConfig ParseTextConfig(string filePath)
    {
        string[] lines = ReadAllLines(filePath);
        var command = new StringBuilder();
        string? name = null;
        string? builtinVersion = null;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('#'))
            {
                Match nameMatch = ConfigNameRegex().Match(line);
                if (nameMatch.Success && string.IsNullOrWhiteSpace(name))
                    name = nameMatch.Groups["value"].Value.Trim();

                Match versionMatch = BuiltinVersionRegex().Match(line);
                if (versionMatch.Success && string.IsNullOrWhiteSpace(builtinVersion))
                    builtinVersion = versionMatch.Groups["value"].Value.Trim();

                continue;
            }

            if (command.Length > 0)
                command.Append(' ');
            command.Append(line);
        }

        return new TextConfig(command.ToString(), name, builtinVersion);
    }

    private static bool LooksLikeTextConfig(string filePath)
    {
        if (!Path.GetExtension(filePath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            foreach (string rawLine in ReadAllLines(filePath).Take(64))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                    continue;
                if (BuiltinVersionRegex().IsMatch(line) || line.StartsWith("--", StringComparison.Ordinal))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static ZapretConfigGeneration? DetectGeneration(string startupString, string? builtinVersion)
    {
        if (!string.IsNullOrWhiteSpace(builtinVersion) || Winws2OptionRegex().IsMatch(startupString))
            return ZapretConfigGeneration.Winws2;

        if (Winws1OptionRegex().IsMatch(startupString))
            return ZapretConfigGeneration.Winws1;

        return null;
    }

    private static string ResolveVersion(ImportedPayload payload, ZapretConfigImportOptions options)
    {
        if (payload.Generation == ZapretConfigGeneration.Winws2 &&
            !string.IsNullOrWhiteSpace(payload.BuiltinVersion))
        {
            return payload.BuiltinVersion;
        }

        string? version = null;
        try
        {
            version = options.CurrentVersionResolver?.Invoke(payload.Generation);
        }
        catch
        {
            // Version lookup is deliberately best-effort. Import must continue.
        }
        if (string.IsNullOrWhiteSpace(version))
            version = TryGetInstalledVersion(payload.Generation);

        return string.IsNullOrWhiteSpace(version)
            ? ZapretConfigImportOptions.CurrentVersionPlaceholder
            : version;
    }

    private static string? TryGetInstalledVersion(ZapretConfigGeneration generation)
    {
        try
        {
            string id = generation == ZapretConfigGeneration.Winws2
                ? HardcodedItemIds.ComponentIds[Components.Zapret2]
                : HardcodedItemIds.ComponentIds[Components.Zapret];

            return DatabaseHelper.Instance.GetItemById(id)?.CurrentVersion;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<LogicalLine> ReadBatchLogicalLines(string filePath)
    {
        string[] physicalLines = ReadAllLines(filePath);
        var result = new List<LogicalLine>();
        var current = new StringBuilder();
        int currentLineNumber = 0;

        for (int index = 0; index < physicalLines.Length; index++)
        {
            string physicalLine = physicalLines[index].TrimEnd();
            if (current.Length == 0)
                currentLineNumber = index + 1;

            bool continues = HasContinuationCaret(physicalLine);
            if (continues)
                physicalLine = physicalLine[..physicalLine.LastIndexOf('^')];

            if (current.Length > 0)
                current.Append(' ');
            current.Append(physicalLine.Trim());

            if (!continues)
            {
                result.Add(new LogicalLine(currentLineNumber, current.ToString()));
                current.Clear();
            }
        }

        if (current.Length > 0)
            result.Add(new LogicalLine(currentLineNumber, current.ToString()));

        return result;
    }

    private static bool HasContinuationCaret(string line)
    {
        int caretCount = 0;
        for (int index = line.Length - 1; index >= 0 && line[index] == '^'; index--)
            caretCount++;
        return caretCount % 2 == 1;
    }

    private static bool TryExtractLaunch(string line, int lineNumber, out BatchLaunch launch)
    {
        launch = null!;
        Match match = WinwsExecutableRegex().Match(line);
        if (!match.Success)
            return false;

        int argumentsStart = match.Index + match.Length;
        if (argumentsStart < line.Length && line[argumentsStart] == '"')
            argumentsStart++;

        string arguments = TrimShellTail(line[argumentsStart..]).Trim();
        if (arguments.Length == 0 ||
            (!arguments.Contains("--", StringComparison.Ordinal) &&
             !StandaloneAtFileRegex().IsMatch(arguments) &&
             !BatchVariableRegex().IsMatch(arguments)))
        {
            return false;
        }

        string executable = match.Groups["exe"].Value;
        ZapretConfigGeneration generation = executable.StartsWith("winws2", StringComparison.OrdinalIgnoreCase)
            ? ZapretConfigGeneration.Winws2
            : ZapretConfigGeneration.Winws1;

        launch = new BatchLaunch(generation, arguments, lineNumber);
        return true;
    }

    private static string TrimShellTail(string input)
    {
        bool quoted = false;
        for (int index = 0; index < input.Length; index++)
        {
            char current = input[index];
            if (current == '"')
                quoted = !quoted;
            if (quoted)
                continue;

            if (current is '>' or '<' or '|')
                return input[..index];
            if (current == '&' && (index == 0 || input[index - 1] != '^'))
                return input[..index];
        }

        return input;
    }

    private static bool TryParseSet(string line, out string name, out string value)
    {
        Match match = SetRegex().Match(line);
        if (!match.Success)
        {
            name = string.Empty;
            value = string.Empty;
            return false;
        }

        name = match.Groups["name"].Value.Trim();
        value = match.Groups["value"].Value;
        if (match.Groups["quoted"].Success && value.EndsWith('"'))
            value = value[..^1];
        return name.Length > 0;
    }

    private static bool TryParseCommentedSet(string line, out string name, out string value)
    {
        Match match = CommentedSetRegex().Match(line);
        if (match.Success)
            return TryParseSet(match.Groups["set"].Value, out name, out value);

        name = string.Empty;
        value = string.Empty;
        return false;
    }

    private static bool IsComment(string line) =>
        line.StartsWith("rem ", StringComparison.OrdinalIgnoreCase) ||
        line.Equals("rem", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("::", StringComparison.Ordinal);

    private static void SetVariable(
        IDictionary<string, MutableVariable> variables,
        string name,
        string value,
        bool isAlternative,
        bool isConditionalCandidate = false)
    {
        if (!variables.TryGetValue(name, out MutableVariable? variable))
        {
            variable = new MutableVariable(name, isAlternative ? string.Empty : value);
            variables[name] = variable;
        }

        variable.IsConditionalCandidate |= isConditionalCandidate;
        variable.Observe(value);

        if (isAlternative)
        {
            variable.AddAlternative(value);
        }
        else
        {
            variable.AddAlternative(variable.Value);
            variable.Value = value;
        }
    }

    private static Dictionary<string, MutableVariable> CloneVariables(
        IReadOnlyDictionary<string, MutableVariable> variables) =>
        variables.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, MutableVariable> GetUsedVariables(
        string startupString,
        IReadOnlyDictionary<string, MutableVariable> allVariables,
        ImportContext context,
        string sourcePath)
    {
        var result = new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(GetReferencedVariableNames(startupString));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (queue.Count > 0)
        {
            string name = queue.Dequeue();
            if (!visited.Add(name))
                continue;

            if (!allVariables.TryGetValue(name, out MutableVariable? variable))
            {
                context.AddWarning(
                    "UNRESOLVED_BATCH_VARIABLE",
                    $"Batch variable '%{name}%' could not be resolved statically.",
                    sourcePath);
                continue;
            }

            result[name] = variable.Clone();
            foreach (string nestedName in GetReferencedVariableNames(variable.Value))
                queue.Enqueue(nestedName);
        }

        return result;
    }

    private static IEnumerable<string> GetReferencedVariableNames(string value)
    {
        foreach (Match match in BatchVariableRegex().Matches(value))
        {
            string name = match.Groups["name"].Value;
            if (!name.StartsWith('~'))
                yield return name;
        }

        foreach (Match match in DelayedVariableRegex().Matches(value))
            yield return match.Groups["name"].Value;
    }

    private static string NormalizeDelayedVariableSyntax(string value) =>
        DelayedVariableRegex().Replace(value, match => $"%{match.Groups["name"].Value}%");

    private static void NormalizeDelayedVariableSyntax(IEnumerable<MutableVariable> variables)
    {
        foreach (MutableVariable variable in variables)
        {
            variable.Value = NormalizeDelayedVariableSyntax(variable.Value);
            for (int index = 0; index < variable.Alternatives.Count; index++)
            {
                variable.Alternatives[index] =
                    NormalizeDelayedVariableSyntax(variable.Alternatives[index]);
            }
            for (int index = 0; index < variable.ObservedValues.Count; index++)
            {
                variable.ObservedValues[index] =
                    NormalizeDelayedVariableSyntax(variable.ObservedValues[index]);
            }
        }
    }

    private static string TranslateBatchValueToConfig(string value) =>
        value.Replace("%~dp0", "$GETCURRENTDIR()/", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ConditionalVariable> CreateConditionalVariables(
        IReadOnlyDictionary<string, MutableVariable> usedVariables)
    {
        var result = new List<ConditionalVariable>();
        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MutableVariable variable in usedVariables.Values)
        {
            if (!variable.IsConditionalCandidate)
                continue;

            List<string> values = variable.ObservedValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (values.Count != 2)
                continue;

            string parameterName = CreateConditionalParameterName(variable.Name, parameterNames);
            result.Add(new ConditionalVariable(
                variable.Name,
                parameterName,
                variable.Value.Equals(values[1], StringComparison.Ordinal),
                values[1],
                values[0]));
        }

        return result;
    }

    private static string CreateConditionalParameterName(string variableName, ISet<string> usedNames)
    {
        string identifier = new(variableName
            .Where(character => char.IsLetterOrDigit(character) || character == '_')
            .ToArray());
        if (identifier.Length == 0)
            identifier = "Variable";

        string baseName = $"use{char.ToUpperInvariant(identifier[0])}{identifier[1..]}";
        string result = baseName;
        for (int suffix = 2; !usedNames.Add(result); suffix++)
            result = $"{baseName}{suffix}";
        return result;
    }

    private static string CreateActiveFileSelectors(
        string startupString,
        string sourcePath,
        IDictionary<string, MutableVariable> allVariables,
        IDictionary<string, MutableVariable> usedVariables)
    {
        var selectors = new Dictionary<string, MutableVariable>(StringComparer.OrdinalIgnoreCase);
        string result = ActiveBinaryReferenceRegex().Replace(startupString, match =>
        {
            string rootVariableName = match.Groups["root"].Value;
            string fileName = match.Groups["file"].Value;
            string selectorKey = $"{rootVariableName}\0{fileName}";
            if (selectors.TryGetValue(selectorKey, out MutableVariable? existingSelector))
                return $"%{existingSelector.Name}%";

            if (!allVariables.TryGetValue(rootVariableName, out MutableVariable? rootVariable))
                return match.Value;

            string sourceDirectory = ExpandBatchValue(
                rootVariable.Value,
                sourcePath,
                (IReadOnlyDictionary<string, MutableVariable>)allVariables);
            if (!Directory.Exists(sourceDirectory))
                return match.Value;

            string[] alternatives = Directory.GetFiles(sourceDirectory, "*.bin")
                .Where(path => !Path.GetFileName(path).StartsWith("ACTIVE_", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (alternatives.Length == 0)
                return match.Value;

            string selectorName = CreateUniqueVariableName(
                Path.GetFileNameWithoutExtension(fileName),
                allVariables.Keys.Concat(usedVariables.Keys));
            string configDirectory = TranslateBatchValueToConfig(rootVariable.Value);
            string activePath = Path.Combine(sourceDirectory, fileName);
            string selectedPath = FindEquivalentBinary(alternatives, activePath) ?? activePath;
            var selector = new MutableVariable(
                selectorName,
                $"{configDirectory}{Path.GetFileName(selectedPath)}");
            foreach (string alternative in alternatives)
                selector.AddAlternative($"{configDirectory}{Path.GetFileName(alternative)}");

            selectors[selectorKey] = selector;
            allVariables[selectorName] = selector;
            usedVariables[selectorName] = selector.Clone();
            return $"%{selectorName}%";
        });

        return result;
    }

    private static string? FindEquivalentBinary(IEnumerable<string> candidates, string activePath)
    {
        if (!File.Exists(activePath))
            return null;

        try
        {
            byte[] activeHash;
            using (FileStream activeStream = File.OpenRead(activePath))
                activeHash = SHA256.HashData(activeStream);

            string? result = null;
            foreach (string candidate in candidates)
            {
                using FileStream candidateStream = File.OpenRead(candidate);
                if (activeHash.AsSpan().SequenceEqual(SHA256.HashData(candidateStream)))
                    result = candidate;
            }
            return result;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string CreateUniqueVariableName(string source, IEnumerable<string> existingNames)
    {
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string baseName = new(source
            .Where(character => char.IsLetterOrDigit(character) || character == '_')
            .ToArray());
        if (baseName.Length == 0)
            baseName = "ImportedFile";

        string result = baseName;
        for (int suffix = 2; existing.Contains(result); suffix++)
            result = $"{baseName}_{suffix}";
        return result;
    }

    private static string ExpandBatchValue(
        string value,
        string batchFile,
        IReadOnlyDictionary<string, MutableVariable> variables,
        int depth = 0)
    {
        if (depth > 16)
            return value;

        string directory = Path.GetDirectoryName(batchFile)! + Path.DirectorySeparatorChar;
        string result = value
            .Replace("$GETCURRENTDIR()/", directory, StringComparison.OrdinalIgnoreCase)
            .Replace("$GETCURRENTDIR()\\", directory, StringComparison.OrdinalIgnoreCase)
            .Replace("%~dp0", directory, StringComparison.OrdinalIgnoreCase)
            .Replace("%~f0", batchFile, StringComparison.OrdinalIgnoreCase)
            .Replace("%~n0", Path.GetFileNameWithoutExtension(batchFile), StringComparison.OrdinalIgnoreCase)
            .Replace("%0", batchFile, StringComparison.OrdinalIgnoreCase);

        string Replace(Match match)
        {
            string name = match.Groups["name"].Value;
            if (variables.TryGetValue(name, out MutableVariable? variable))
                return ExpandBatchValue(variable.Value, batchFile, variables, depth + 1);

            return Environment.GetEnvironmentVariable(name) ?? match.Value;
        }

        result = BatchVariableRegex().Replace(result, Replace);
        result = DelayedVariableRegex().Replace(result, Replace);
        return result;
    }

    private static bool TryGetCalledScript(
        string line,
        string callerPath,
        IReadOnlyDictionary<string, MutableVariable> variables,
        out CalledScript calledScript)
    {
        calledScript = null!;
        Match match = CallScriptRegex().Match(line);
        if (!match.Success)
            return false;

        string token = match.Groups["quoted"].Success
            ? match.Groups["quoted"].Value
            : match.Groups["plain"].Value;
        if (token.StartsWith(':'))
            return false;

        string expanded = ExpandBatchValue(token, callerPath, variables);
        string candidate = Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(Path.GetDirectoryName(callerPath)!, expanded);

        if (!Path.HasExtension(candidate))
            candidate += ".bat";

        try
        {
            string fullPath = Path.GetFullPath(candidate);
            if (!File.Exists(fullPath) ||
                (!Path.GetExtension(fullPath).Equals(".bat", StringComparison.OrdinalIgnoreCase) &&
                 !Path.GetExtension(fullPath).Equals(".cmd", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string argumentsText = TrimShellTail(line[(match.Index + match.Length)..]).Trim();
            calledScript = new CalledScript(fullPath, SplitBatchArguments(argumentsText));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CollectCalledScriptVariables(
        CalledScript call,
        IDictionary<string, MutableVariable> variables,
        ISet<string> inspectedCalls,
        ImportContext context,
        int depth)
    {
        string callKey = $"{call.Path}\0{string.Join('\0', call.Arguments)}";
        if (depth > context.Options.MaxIncludeDepth || !inspectedCalls.Add(callKey))
            return;

        IReadOnlyList<LogicalLine> lines;
        try
        {
            lines = ReadBatchLogicalLines(call.Path);
            context.AddSourceFile(call.Path);
        }
        catch (Exception exception)
        {
            context.AddWarning("CALLED_SCRIPT_READ_FAILED", exception.Message, call.Path);
            return;
        }

        var localVariables = CloneVariables((IReadOnlyDictionary<string, MutableVariable>)variables);
        string? requestedCommand = call.Arguments.FirstOrDefault();
        IReadOnlyList<string> dispatchedLabels = FindDispatchedLabels(lines, call.Arguments.FirstOrDefault());
        if (dispatchedLabels.Count > 0)
        {
            var visitedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string label in dispatchedLabels)
            {
                CollectLabelVariables(
                    call.Path,
                    lines,
                    label,
                    localVariables,
                    inspectedCalls,
                    context,
                    depth,
                    visitedLabels);
            }
        }
        else
        {
            CollectTopLevelVariables(
                call.Path,
                lines,
                localVariables,
                inspectedCalls,
                context,
                depth);
        }

        if (requestedCommand?.Equals("load_user_lists", StringComparison.OrdinalIgnoreCase) == true)
            CollectGeneratedFiles(call.Path, lines, "load_user_lists", localVariables, context);

        MergeVariables(localVariables, variables);
    }

    private static void CollectTopLevelVariables(
        string scriptPath,
        IReadOnlyList<LogicalLine> lines,
        IDictionary<string, MutableVariable> variables,
        ISet<string> inspectedCalls,
        ImportContext context,
        int depth)
    {
        foreach (LogicalLine logicalLine in lines)
        {
            string line = logicalLine.Text.Trim();
            if (BatchLabelRegex().IsMatch(line))
                break;

            CollectStaticLineVariables(
                line,
                scriptPath,
                variables,
                inspectedCalls,
                context,
                depth);
        }
    }

    private static void CollectLabelVariables(
        string scriptPath,
        IReadOnlyList<LogicalLine> lines,
        string label,
        IDictionary<string, MutableVariable> variables,
        ISet<string> inspectedCalls,
        ImportContext context,
        int depth,
        ISet<string> visitedLabels)
    {
        if (!visitedLabels.Add(label) || !TryGetLabelRange(lines, label, out int start, out int end))
            return;

        for (int index = start; index < end; index++)
        {
            string line = lines[index].Text.Trim();
            Match localCall = LocalLabelCallRegex().Match(line);
            if (localCall.Success)
            {
                CollectLabelVariables(
                    scriptPath,
                    lines,
                    localCall.Groups["label"].Value,
                    variables,
                    inspectedCalls,
                    context,
                    depth,
                    visitedLabels);
                continue;
            }

            CollectStaticLineVariables(
                line,
                scriptPath,
                variables,
                inspectedCalls,
                context,
                depth);
        }
    }

    private static void CollectStaticLineVariables(
        string line,
        string scriptPath,
        IDictionary<string, MutableVariable> variables,
        ISet<string> inspectedCalls,
        ImportContext context,
        int depth)
    {
        if (TryParseCommentedSet(line, out string alternativeName, out string alternativeValue))
        {
            SetVariable(variables, alternativeName, alternativeValue, isAlternative: true);
            return;
        }

        if (IsComment(line))
            return;

        if (TryParseSet(line, out string name, out string value))
        {
            SetVariable(
                variables,
                name,
                value,
                isAlternative: false,
                isConditionalCandidate: true);
            return;
        }

        if (TryGetCalledScript(line, scriptPath, (IReadOnlyDictionary<string, MutableVariable>)variables, out CalledScript nestedCall))
        {
            CollectCalledScriptVariables(
                nestedCall,
                variables,
                inspectedCalls,
                context,
                depth + 1);
        }
    }

    private static IReadOnlyList<string> FindDispatchedLabels(
        IReadOnlyList<LogicalLine> lines,
        string? requestedArgument)
    {
        if (string.IsNullOrWhiteSpace(requestedArgument))
            return [];

        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index].Text.Trim();
            Match dispatch = ExternalDispatchRegex().Match(line);
            if (!dispatch.Success ||
                !dispatch.Groups["argument"].Value.Equals(
                    requestedArgument,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var labels = new List<string>();
            int depth = CountParentheses(line);
            for (index++; index < lines.Count && depth > 0; index++)
            {
                string blockLine = lines[index].Text.Trim();
                Match localCall = LocalLabelCallRegex().Match(blockLine);
                if (localCall.Success)
                    labels.Add(localCall.Groups["label"].Value);
                depth += CountParentheses(blockLine);
            }

            return labels;
        }

        return [];
    }

    private static int CountParentheses(string line)
    {
        bool quoted = false;
        int result = 0;
        foreach (char current in line)
        {
            if (current == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (quoted)
                continue;
            if (current == '(')
                result++;
            else if (current == ')')
                result--;
        }

        return result;
    }

    private static bool TryGetLabelRange(
        IReadOnlyList<LogicalLine> lines,
        string label,
        out int start,
        out int end)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            Match match = BatchLabelRegex().Match(lines[index].Text.Trim());
            if (!match.Success ||
                !match.Groups["label"].Value.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            start = index + 1;
            end = lines.Count;
            for (int next = start; next < lines.Count; next++)
            {
                if (BatchLabelRegex().IsMatch(lines[next].Text.Trim()))
                {
                    end = next;
                    break;
                }
            }
            return true;
        }

        start = 0;
        end = 0;
        return false;
    }

    private static void CollectGeneratedFiles(
        string scriptPath,
        IReadOnlyList<LogicalLine> lines,
        string label,
        IReadOnlyDictionary<string, MutableVariable> variables,
        ImportContext context)
    {
        if (!TryGetLabelRange(lines, label, out int start, out int end))
            return;

        var contents = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
        for (int index = start; index < end; index++)
        {
            Match match = EchoFileWriteRegex().Match(lines[index].Text.Trim());
            if (!match.Success)
                continue;

            string expandedPath = ExpandBatchValue(
                match.Groups["path"].Value.Trim(),
                scriptPath,
                variables);
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(expandedPath);
            }
            catch
            {
                continue;
            }

            if (File.Exists(fullPath))
                continue;

            bool append = match.Groups["operator"].Value == ">>";
            if (!append || !contents.TryGetValue(fullPath, out StringBuilder? content))
            {
                content = new StringBuilder();
                contents[fullPath] = content;
            }

            content.Append(match.Groups["content"].Value.TrimEnd()).Append("\r\n");
        }

        string sourceRoot = Path.GetDirectoryName(scriptPath)!;
        foreach (var pair in contents)
            context.AddGeneratedFile(sourceRoot, pair.Key, pair.Value.ToString());
    }

    private static void CollectReferencedFiles(
        string startupString,
        string sourcePath,
        IReadOnlyDictionary<string, MutableVariable> variables,
        ImportContext context)
    {
        string expanded = ExpandBatchValue(startupString, sourcePath, variables);
        foreach (Match match in ResourcePathRegex().Matches(expanded))
        {
            string rawPath = match.Groups["quotedPath"].Success
                ? match.Groups["quotedPath"].Value
                : match.Groups["plainPath"].Value;
            rawPath = rawPath.TrimStart('@', '$');

            string candidate = Path.IsPathRooted(rawPath)
                ? rawPath
                : Path.Combine(Path.GetDirectoryName(sourcePath)!, rawPath);
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch
            {
                continue;
            }

            if (File.Exists(fullPath))
            {
                context.AddReferencedFile(fullPath);
            }
            else if (!context.IsGeneratedFile(fullPath))
            {
                context.AddMissingReferencedFile(fullPath, sourcePath);
            }
        }
    }

    private static IReadOnlyList<string> SplitBatchArguments(string input)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;

        foreach (char character in input)
        {
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (!quoted && char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
            result.Add(current.ToString());
        return result;
    }

    private static void MergeVariables(
        IReadOnlyDictionary<string, MutableVariable> source,
        IDictionary<string, MutableVariable> destination)
    {
        foreach (var pair in source)
        {
            if (!destination.TryGetValue(pair.Key, out MutableVariable? existing))
            {
                destination[pair.Key] = pair.Value.Clone();
                continue;
            }

            foreach (string alternative in pair.Value.Alternatives.Append(pair.Value.Value))
                existing.AddAlternative(alternative);

            existing.IsConditionalCandidate |= pair.Value.IsConditionalCandidate;
            foreach (string observedValue in pair.Value.ObservedValues)
                existing.Observe(observedValue);
        }
    }

    private static string[] ReadAllLines(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        Encoding encoding = DetectEncoding(bytes);
        string text = encoding.GetString(bytes).TrimStart('\uFEFF');
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1251);
        }
    }

    private static string NormalizeWhitespace(string input)
    {
        var result = new StringBuilder(input.Length);
        bool quoted = false;
        bool whitespacePending = false;

        foreach (char current in input.Trim())
        {
            if (current == '"')
            {
                if (whitespacePending && result.Length > 0)
                    result.Append(' ');
                whitespacePending = false;
                quoted = !quoted;
                result.Append(current);
                continue;
            }

            if (!quoted && char.IsWhiteSpace(current))
            {
                whitespacePending = true;
                continue;
            }

            if (whitespacePending && result.Length > 0)
                result.Append(' ');
            whitespacePending = false;
            result.Append(current);
        }

        return result.ToString();
    }

    private static string GetExecutableName(ZapretConfigGeneration generation) =>
        generation == ZapretConfigGeneration.Winws2 ? "winws2" : "winws1";

    private sealed class ImportContext(ZapretConfigImportOptions options)
    {
        private readonly List<ZapretConfigImportIssue> _issues = [];
        private readonly List<string> _sourceFiles = [];
        private readonly HashSet<string> _sourceFileSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _referencedFiles = [];
        private readonly HashSet<string> _referencedFileSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ZapretConfigImportGeneratedFile> _generatedFiles = [];
        private readonly HashSet<string> _generatedFilePaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingReferencedFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _activeIncludes = new(StringComparer.OrdinalIgnoreCase);

        public ZapretConfigImportOptions Options { get; } = options;

        public void AddError(string code, string message, string? path, int? line = null) =>
            _issues.Add(new ZapretConfigImportIssue(
                ZapretConfigImportIssueSeverity.Error,
                code,
                message,
                path,
                line));

        public void AddWarning(string code, string message, string? path, int? line = null) =>
            _issues.Add(new ZapretConfigImportIssue(
                ZapretConfigImportIssueSeverity.Warning,
                code,
                message,
                path,
                line));

        public void AddSourceFile(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (_sourceFileSet.Add(fullPath))
                _sourceFiles.Add(fullPath);
        }

        public bool EnterInclude(string path) => _activeIncludes.Add(path);

        public void LeaveInclude(string path) => _activeIncludes.Remove(path);

        public void AddReferencedFile(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (_referencedFileSet.Add(fullPath))
                _referencedFiles.Add(fullPath);
        }

        public void AddGeneratedFile(string sourceRoot, string path, string content)
        {
            string fullRoot = Path.GetFullPath(sourceRoot);
            string fullPath = Path.GetFullPath(path);
            string relativePath = Path.GetRelativePath(fullRoot, fullPath);
            if (Path.IsPathRooted(relativePath) ||
                relativePath.Equals("..", StringComparison.Ordinal) ||
                relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                AddWarning(
                    "GENERATED_FILE_OUTSIDE_SOURCE",
                    "A batch-generated file outside the source config directory was ignored.",
                    fullPath);
                return;
            }

            if (_generatedFilePaths.Add(fullPath))
                _generatedFiles.Add(new ZapretConfigImportGeneratedFile(relativePath, content));
        }

        public bool IsGeneratedFile(string path) =>
            _generatedFilePaths.Contains(Path.GetFullPath(path));

        public void AddMissingReferencedFile(string path, string sourcePath)
        {
            string fullPath = Path.GetFullPath(path);
            if (_missingReferencedFiles.Add(fullPath))
            {
                AddWarning(
                    "REFERENCED_FILE_NOT_FOUND",
                    $"A referenced BIN/TXT/LUA file was not found: {fullPath}",
                    sourcePath);
            }
        }

        public ZapretConfigImportResult CreateResult(
            ConfigItem? config = null,
            ZapretConfigGeneration? generation = null,
            IEnumerable<MutableVariable>? variables = null) =>
            new()
            {
                Config = config,
                Generation = generation,
                Issues = _issues.AsReadOnly(),
                SourceFiles = _sourceFiles.AsReadOnly(),
                ReferencedFiles = _referencedFiles.AsReadOnly(),
                MissingReferencedFiles = _missingReferencedFiles.ToList().AsReadOnly(),
                GeneratedFiles = _generatedFiles.AsReadOnly(),
                Variables = (variables ?? [])
                    .Select(variable => new ZapretConfigImportVariable(
                        variable.Name,
                        variable.Value,
                        variable.Alternatives.AsReadOnly()))
                    .ToList()
                    .AsReadOnly(),
            };
    }

    private sealed class MutableVariable(string name, string value)
    {
        public string Name { get; } = name;

        public string Value { get; set; } = value;

        public List<string> Alternatives { get; } = [];

        public List<string> ObservedValues { get; } = string.IsNullOrEmpty(value) ? [] : [value];

        public bool IsConditionalCandidate { get; set; }

        public void AddAlternative(string alternative)
        {
            if (!string.IsNullOrEmpty(alternative) &&
                !string.Equals(Value, alternative, StringComparison.Ordinal) &&
                !Alternatives.Contains(alternative, StringComparer.Ordinal))
            {
                Alternatives.Add(alternative);
            }
        }

        public void Observe(string observedValue)
        {
            if (!string.IsNullOrEmpty(observedValue) &&
                !ObservedValues.Contains(observedValue, StringComparer.Ordinal))
            {
                ObservedValues.Add(observedValue);
            }
        }

        public IEnumerable<string> GetSelectableValues() =>
            new[] { Value }.Concat(Alternatives);

        public MutableVariable Clone()
        {
            var result = new MutableVariable(Name, Value)
            {
                IsConditionalCandidate = IsConditionalCandidate,
            };
            result.Alternatives.AddRange(Alternatives);
            result.ObservedValues.Clear();
            result.ObservedValues.AddRange(ObservedValues);
            return result;
        }
    }

    private sealed record ImportedPayload(
        ZapretConfigGeneration Generation,
        string StartupString,
        string Name,
        string? BuiltinVersion,
        Dictionary<string, MutableVariable> Variables,
        IReadOnlyList<ConditionalVariable> ConditionalVariables);

    private sealed record ConditionalVariable(
        string Name,
        string ParameterName,
        bool IsEnabled,
        string EnabledValue,
        string DisabledValue);

    private sealed record TextConfig(string StartupString, string? Name, string? BuiltinVersion);

    private sealed record IncludeExpansion(string Text, string? Name, string? BuiltinVersion);

    private sealed record LogicalLine(int LineNumber, string Text);

    private sealed record BatchLaunch(
        ZapretConfigGeneration Generation,
        string Arguments,
        int LineNumber);

    private sealed record CalledScript(string Path, IReadOnlyList<string> Arguments);

    [GeneratedRegex(
        """(?ix)(?:^|\s)(?<token>[@$](?:"(?<quoted>[^"]+)"|(?<plain>[^\s"]+)))""")]
    private static partial Regex StandaloneAtFileRegex();

    [GeneratedRegex(
        @"(?im)^\s*#\s*(?:built(?:-|\s*)in\s*version|builtinversion)\s*[:=]\s*(?<value>[^\s#;]+)")]
    private static partial Regex BuiltinVersionRegex();

    [GeneratedRegex(@"(?im)^\s*#\s*(?:preset|activepreset)\s*[:=]\s*(?<value>.+?)\s*$")]
    private static partial Regex ConfigNameRegex();

    [GeneratedRegex(
        @"(?ix)(?:^|\s)(?:--lua-init(?:=|\s)|--lua-desync(?:=|\s)|--blob(?:=|\s)|--wf-(?:tcp|udp)-(?:out|in)(?:=|\s)|--out-range(?:=|\s)|--ctrack-[a-z0-9-]+(?:=|\s))")]
    private static partial Regex Winws2OptionRegex();

    [GeneratedRegex(
        @"(?ix)(?:^|\s)(?:--dpi-desync(?:=|\s)|--wf-(?:tcp|udp)(?:=|\s))")]
    private static partial Regex Winws1OptionRegex();

    [GeneratedRegex(
        """(?ix)(?<exe>winws2?)(?:\.exe)?(?=["\s]|$)""")]
    private static partial Regex WinwsExecutableRegex();

    [GeneratedRegex("""(?i)^\s*@?set\s+(?<quoted>")?(?<name>[^="]+)=(?<value>.*)$""")]
    private static partial Regex SetRegex();

    [GeneratedRegex(@"(?i)^\s*(?:rem\s+|::\s*)(?<set>set\s+.+)$")]
    private static partial Regex CommentedSetRegex();

    [GeneratedRegex(@"%(?<name>[A-Za-z0-9_~]+)%", RegexOptions.IgnoreCase)]
    private static partial Regex BatchVariableRegex();

    [GeneratedRegex(@"!(?<name>[A-Za-z0-9_]+)!", RegexOptions.IgnoreCase)]
    private static partial Regex DelayedVariableRegex();

    [GeneratedRegex(@"%(?<root>[A-Za-z0-9_]+)%(?<file>ACTIVE_[^\s\""'\\/]+\.bin)", RegexOptions.IgnoreCase)]
    private static partial Regex ActiveBinaryReferenceRegex();

    [GeneratedRegex(
        """(?ix)^\s*(?:@?call)\s+(?:"(?<quoted>[^"]+\.(?:bat|cmd))"|(?<plain>[^\s"]+\.(?:bat|cmd)))""")]
    private static partial Regex CallScriptRegex();

    [GeneratedRegex(
        """(?ix)^\s*if(?:\s+/i)?\s+"?%~?1"?\s*==\s*"(?<argument>[^"]*)"\s*\(""")]
    private static partial Regex ExternalDispatchRegex();

    [GeneratedRegex(@"(?i)^\s*(?:@?call)\s+:(?<label>[A-Za-z0-9_.-]+)")]
    private static partial Regex LocalLabelCallRegex();

    [GeneratedRegex(@"^\s*:(?!:)(?<label>[A-Za-z0-9_.-]+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex BatchLabelRegex();

    [GeneratedRegex(
        """(?ix)^\s*@?echo(?:\s+|\.)?(?<content>.*?)\s*(?<operator>>>|>)\s*"?(?<path>[^">]+)"?\s*$""")]
    private static partial Regex EchoFileWriteRegex();

    [GeneratedRegex(
        """(?ix)(?:["'](?<quotedPath>[^"']+\.(?:bin|txt|lua))["']|(?<plainPath>(?:[A-Za-z]:[\\/][^\s"'=]+|[^\s"'=:]+)\.(?:bin|txt|lua)))""")]
    private static partial Regex ResourcePathRegex();
}
