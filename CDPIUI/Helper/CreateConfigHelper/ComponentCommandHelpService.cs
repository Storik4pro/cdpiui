using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Communication;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Helper.CreateConfigHelper;

public sealed class ComponentCommandHelpDocument
{
    public string ComponentId { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public string Usage { get; init; } = string.Empty;
    public string RawText { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public IReadOnlyList<ComponentCommandHelpOption> Options { get; init; } = [];
}

public sealed class ComponentCommandHelpOption
{
    public string DisplayName => Names.FirstOrDefault(name => name.StartsWith("--", StringComparison.Ordinal))
        ?? Names.FirstOrDefault()
        ?? Syntax;
    public IReadOnlyList<string> Names { get; init; } = [];
    public string GroupName { get; init; } = string.Empty;
    public string Syntax { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ArgumentPlaceholder { get; init; } = string.Empty;
    public bool IsArgumentRequired { get; init; }

    public bool Matches(string flag) => Names.Any(name =>
        string.Equals(name, flag, StringComparison.OrdinalIgnoreCase));
}

public sealed partial class ComponentCommandHelpService
{
    private const int ParserFormatVersion = 3;
    private static readonly ConcurrentDictionary<string, ComponentCommandHelpDocument> Cache = new();
    private static readonly TimeSpan HelpTimeout = TimeSpan.FromSeconds(15);

    [GeneratedRegex(@"(?<!\S)(-{1,2}[A-Za-z0-9][A-Za-z0-9_.-]*|/[A-Za-z?][A-Za-z0-9?_.-]*)")]
    private static partial Regex OptionNameRegex();

    [GeneratedRegex(@"-{1,2}[A-Za-z0-9][A-Za-z0-9_.-]*|/[A-Za-z?][A-Za-z0-9?_.-]*")]
    private static partial Regex EmbeddedOptionNameRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ColumnSeparatorRegex();

    [GeneratedRegex(@"\x1B\][^\x07]*(?:\x07|\x1B\\)")]
    private static partial Regex OscSequenceRegex();

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex CsiSequenceRegex();

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*E")]
    private static partial Regex CsiNextLineSequenceRegex();

    [GeneratedRegex(@"\x1B[DE]")]
    private static partial Regex EscLineSequenceRegex();

    [GeneratedRegex(@"(?<![A-Z0-9])(?<header>[A-Z][A-Z0-9 /()&+_-]{2,}:)(?=\s+(?:-{1,2}[A-Za-z0-9]|/[A-Za-z?]))")]
    private static partial Regex GroupHeaderBeforeOptionRegex();

    public async Task<ComponentCommandHelpDocument> LoadAsync(
        string componentId,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false)
    {
        ComponentHelper component = ComponentItemsLoaderHelper.Instance
            .GetComponentHelperFromId(componentId);
        string executablePath = component?.GetExecutablePath();

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return CreateError(componentId, executablePath, "The component executable was not found.");
        }

        string cacheKey = $"{executablePath}|{File.GetLastWriteTimeUtc(executablePath).Ticks}|parser:{ParserFormatVersion}";
        if (!forceRefresh && Cache.TryGetValue(cacheKey, out ComponentCommandHelpDocument cached))
        {
            return cached;
        }

        ComponentCommandHelpDocument result = await ReadHelpAsync(
            componentId,
            executablePath,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(result.Error))
        {
            Cache[cacheKey] = result;
        }

        return result;
    }

    private static async Task<ComponentCommandHelpDocument> ReadHelpAsync(
        string componentId,
        string executablePath,
        CancellationToken cancellationToken)
    {
        try
        {
            ConPtyHelpCaptureResult capture = await ConPtyHelpCaptureClient.CaptureHelpAsync(
                componentId,
                executablePath,
                HelpTimeout,
                cancellationToken);
            string rawText = SanitizeConPtyOutput(capture.Output).Trim();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                string error = !string.IsNullOrWhiteSpace(capture.Error)
                    ? capture.Error
                    : capture.TimedOut
                        ? "The component did not return help before the timeout."
                        : $"The component returned no help text (exit code {capture.ExitCode}).";
                return CreateError(
                    componentId,
                    executablePath,
                    error);
            }

            string warning = capture.TimedOut
                ? "The help process exceeded the time limit; the displayed output may be incomplete."
                : string.Empty;
            return Parse(componentId, executablePath, rawText, warning);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CreateError(componentId, executablePath, exception.Message);
        }
    }

    public static ComponentCommandHelpDocument Parse(
        string componentId,
        string executablePath,
        string rawText,
        string error = "")
    {
        string usage = string.Empty;
        string currentGroupName = string.Empty;
        List<MutableHelpOption> parsed = [];
        MutableHelpOption current = null;
        string[] lines = GetLogicalHelpLines(rawText);

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string originalLine = lines[lineIndex];
            string trimmed = originalLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                current = null;
                continue;
            }

            if (string.IsNullOrEmpty(usage) &&
                (trimmed.StartsWith("usage", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("syntax", StringComparison.OrdinalIgnoreCase)))
            {
                usage = trimmed;
            }

            string nextNonEmptyLine = GetNextNonEmptyLine(lines, lineIndex + 1);
            if (TryGetGroupName(
                originalLine,
                trimmed,
                current == null,
                nextNonEmptyLine,
                out string groupName))
            {
                currentGroupName = groupName;
                current = null;
                continue;
            }

            Match firstName = OptionNameRegex().Match(trimmed);
            if (!firstName.Success || firstName.Index > 2)
            {
                if (current != null)
                {
                    current.Description = MergeDescriptionContinuation(
                        current.Description,
                        NormalizeDescription(trimmed),
                        originalLine.Length > 0 && char.IsWhiteSpace(originalLine[0]));
                }
                continue;
            }

            (string syntax, string description) = SplitOptionLine(trimmed);
            List<string> names = OptionNameRegex().Matches(syntax)
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (names.Count == 0)
            {
                continue;
            }

            (string placeholder, bool required) = FindArgument(syntax, names);
            current = new MutableHelpOption
            {
                Names = names,
                GroupName = currentGroupName,
                Syntax = syntax,
                Description = description,
                ArgumentPlaceholder = placeholder,
                IsArgumentRequired = required,
            };
            parsed.Add(current);
        }

        IReadOnlyList<ComponentCommandHelpOption> options = parsed
            .GroupBy(item => item.Names.First(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().ToImmutable())
            .ToList();

        return new ComponentCommandHelpDocument
        {
            ComponentId = componentId,
            ExecutablePath = executablePath,
            Usage = usage,
            RawText = rawText,
            Error = error,
            Options = options,
        };
    }

    private static (string Placeholder, bool Required) FindArgument(
        string syntax,
        IReadOnlyList<string> names)
    {
        int lastNameEnd = names
            .Select(name => syntax.LastIndexOf(name, StringComparison.OrdinalIgnoreCase) + name.Length)
            .DefaultIfEmpty(0)
            .Max();
        if (lastNameEnd <= 0 || lastNameEnd >= syntax.Length)
        {
            return (string.Empty, false);
        }

        string remainder = syntax[lastNameEnd..].Trim().TrimStart('=').Trim();
        if (string.IsNullOrWhiteSpace(remainder) || remainder.StartsWith(','))
        {
            return (string.Empty, false);
        }

        string placeholder = remainder.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        bool required = !IsEntirelyOptional(placeholder);
        return (placeholder, required);
    }

    private static bool TryGetGroupName(
        string originalLine,
        string trimmed,
        bool isBetweenOptions,
        string nextNonEmptyLine,
        out string groupName)
    {
        groupName = string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) ||
            OptionNameRegex().IsMatch(trimmed) ||
            trimmed.StartsWith("usage", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("syntax", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int leadingWhitespace = originalLine.Length - originalLine.TrimStart().Length;
        Match nextOption = OptionNameRegex().Match(nextNonEmptyLine ?? string.Empty);
        bool followedByOption = nextOption.Success && nextOption.Index <= 2;
        bool hasHeaderPunctuation = trimmed.EndsWith(':') ||
            (trimmed.StartsWith("==", StringComparison.Ordinal) &&
             trimmed.EndsWith("==", StringComparison.Ordinal));
        bool looksLikePlainHeader = trimmed.Length <= 60 &&
            trimmed.IndexOfAny([';', '=', '<', '>', '|', '@', '$']) < 0;
        if (!hasHeaderPunctuation &&
            !(isBetweenOptions && followedByOption && looksLikePlainHeader))
        {
            return false;
        }
        if (leadingWhitespace > 8 && !followedByOption)
        {
            return false;
        }

        groupName = trimmed
            .TrimEnd(':')
            .Trim('=', '-', '*', '#', ' ');
        return !string.IsNullOrWhiteSpace(groupName) && groupName.Length <= 100;
    }

    private static string GetNextNonEmptyLine(IReadOnlyList<string> lines, int startIndex)
    {
        for (int index = startIndex; index < lines.Count; index++)
        {
            string candidate = lines[index].Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }
        return string.Empty;
    }

    private static string[] GetLogicalHelpLines(string rawText)
    {
        string normalized = NormalizeLineSeparators(SanitizeConPtyOutput(rawText));

        // ConPTY can express a new row by moving the cursor instead of writing a
        // conventional CR/LF. If such a transition was already stripped before
        // the text reached us, a section title can remain glued to the preceding
        // description. A title is safe to separate here when an option follows it.
        normalized = GroupHeaderBeforeOptionRegex().Replace(
            normalized,
            match =>
            {
                bool startsAtLineBeginning = match.Index == 0 || normalized[match.Index - 1] == '\n';
                string prefix = startsAtLineBeginning ? string.Empty : "\n";
                return $"{prefix}{match.Groups["header"].Value}\n";
            });

        List<string> logicalLines = [];
        foreach (string line in normalized.Split('\n'))
        {
            if (TrySplitAttachedTrailingOption(line, out string prefix, out string option))
            {
                logicalLines.Add(prefix);
                logicalLines.Add(option);
            }
            else
            {
                logicalLines.Add(line);
            }
        }

        return logicalLines.ToArray();
    }

    private static bool TrySplitAttachedTrailingOption(
        string line,
        out string prefix,
        out string option)
    {
        prefix = string.Empty;
        option = string.Empty;

        MatchCollection matches = EmbeddedOptionNameRegex().Matches(line);
        for (int index = matches.Count - 1; index >= 0; index--)
        {
            Match match = matches[index];
            if (match.Index <= 2)
            {
                continue;
            }

            string candidatePrefix = line[..match.Index].TrimEnd();
            string candidateOption = line[match.Index..].Trim();
            if (candidateOption.Any(char.IsWhiteSpace) ||
                candidateOption.EndsWith('.') ||
                candidateOption.EndsWith(',') ||
                candidateOption.EndsWith(';') ||
                candidateOption.EndsWith(':') ||
                candidateOption.EndsWith('\'') ||
                candidateOption.EndsWith('"'))
            {
                continue;
            }

            bool followsCompletedDescription = candidatePrefix.Contains(';') &&
                (candidatePrefix.EndsWith('.') ||
                 candidatePrefix.EndsWith('!') ||
                 candidatePrefix.EndsWith('?'));
            bool followsConfigFileSyntax = candidatePrefix.TrimStart().StartsWith("@<", StringComparison.Ordinal) ||
                candidatePrefix.TrimStart().StartsWith("$<", StringComparison.Ordinal);
            if (!followsCompletedDescription && !followsConfigFileSyntax)
            {
                continue;
            }

            prefix = candidatePrefix;
            option = candidateOption;
            return true;
        }

        return false;
    }

    private static (string Syntax, string Description) SplitOptionLine(string line)
    {
        int descriptionDelimiter = line.IndexOf(';');
        if (descriptionDelimiter >= 0)
        {
            return (
                line[..descriptionDelimiter].TrimEnd(),
                NormalizeDescription(line[(descriptionDelimiter + 1)..]));
        }

        string[] columns = ColumnSeparatorRegex().Split(line, 2);
        return (
            columns[0].Trim(),
            columns.Length > 1
                ? NormalizeDescription(columns[1])
                : string.Empty);
    }

    private static string NormalizeDescription(string description) =>
        (description ?? string.Empty).Trim().TrimStart(';').TrimStart();

    private static string MergeDescriptionContinuation(
        string description,
        string continuation,
        bool startsIndented)
    {
        if (string.IsNullOrWhiteSpace(continuation))
        {
            return description;
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            return continuation;
        }

        // A terminal soft-wrap starts in column zero. When it cuts a word at
        // the right edge, ConPTY may repeat the edge character on the next row
        // ("prefi" + "ix"). Rejoin that case without introducing a space.
        bool likelyMidWordWrap = !startsIndented &&
            description.Length >= 80 &&
            char.IsLetterOrDigit(description[^1]) &&
            char.IsLower(continuation[0]);
        if (likelyMidWordWrap)
        {
            int continuationStart = description[^1] == continuation[0] ? 1 : 0;
            return description + continuation[continuationStart..];
        }

        return $"{description} {continuation}".Trim();
    }

    private static bool IsEntirelyOptional(string placeholder)
    {
        if (placeholder.Length < 2 || placeholder[0] != '[')
        {
            return false;
        }

        int depth = 0;
        for (int index = 0; index < placeholder.Length; index++)
        {
            depth += placeholder[index] switch
            {
                '[' => 1,
                ']' => -1,
                _ => 0,
            };

            if (depth == 0)
            {
                return index == placeholder.Length - 1;
            }
            if (depth < 0)
            {
                return false;
            }
        }

        return false;
    }

    private static string SanitizeConPtyOutput(string output)
    {
        string sanitized = OscSequenceRegex().Replace(output ?? string.Empty, string.Empty);
        sanitized = CsiNextLineSequenceRegex().Replace(sanitized, "\n");
        sanitized = EscLineSequenceRegex().Replace(sanitized, "\n");
        sanitized = CsiSequenceRegex().Replace(sanitized, string.Empty);
        sanitized = sanitized.Replace("\0", string.Empty, StringComparison.Ordinal);
        return RemoveInvisibleCharacters(NormalizeLineSeparators(sanitized));
    }

    private static string RemoveInvisibleCharacters(string text)
    {
        StringBuilder result = new(text.Length);
        foreach (char character in text)
        {
            if (character is '\n' or '\t')
            {
                result.Append(character);
                continue;
            }

            System.Globalization.UnicodeCategory category = char.GetUnicodeCategory(character);
            if (category is System.Globalization.UnicodeCategory.Control or
                System.Globalization.UnicodeCategory.Format)
            {
                continue;
            }

            result.Append(character);
        }

        return result.ToString();
    }

    private static string NormalizeLineSeparators(string text) =>
        (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\v', '\n')
            .Replace('\f', '\n')
            .Replace('\u0085', '\n')
            .Replace('\u2028', '\n')
            .Replace('\u2029', '\n');

    private static ComponentCommandHelpDocument CreateError(
        string componentId,
        string executablePath,
        string error) => new()
        {
            ComponentId = componentId,
            ExecutablePath = executablePath ?? string.Empty,
            Error = error,
        };

    private sealed class MutableHelpOption
    {
        public List<string> Names { get; init; } = [];
        public string GroupName { get; init; } = string.Empty;
        public string Syntax { get; init; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ArgumentPlaceholder { get; init; } = string.Empty;
        public bool IsArgumentRequired { get; init; }

        public ComponentCommandHelpOption ToImmutable() => new()
        {
            Names = Names,
            GroupName = GroupName,
            Syntax = Syntax,
            Description = Description,
            ArgumentPlaceholder = ArgumentPlaceholder,
            IsArgumentRequired = IsArgumentRequired,
        };
    }
}

public static partial class ComponentCommandLineFormatter
{
    [GeneratedRegex(@"^-?\d+(?:[.,]\d+)?$")]
    private static partial Regex NumericTokenRegex();

    public static string FormatByFlags(string commandLine)
    {
        List<string> lines = [];
        StringBuilder current = new();

        foreach (string token in Tokenize(commandLine))
        {
            if (IsFlag(token))
            {
                FlushLine(lines, current);
                current.Append(token);
            }
            else
            {
                if (current.Length > 0)
                {
                    current.Append(' ');
                }
                current.Append(token);
            }
        }

        FlushLine(lines, current);
        return string.Join(Environment.NewLine, lines);
    }

    public static string ToSingleLine(string commandLine) =>
        string.Join(' ', Tokenize(commandLine));

    public static IReadOnlyList<string> ExtractFlags(string commandLine) => Tokenize(commandLine)
        .Where(IsFlag)
        .Select(token => token.Split('=', 2)[0])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static IReadOnlyList<string> Tokenize(string commandLine)
    {
        List<string> tokens = [];
        StringBuilder current = new();
        char quote = '\0';

        foreach (char character in commandLine ?? string.Empty)
        {
            if ((character == '\"' || character == '\'') && (quote == '\0' || quote == character))
            {
                quote = quote == character ? '\0' : character;
                current.Append(character);
                continue;
            }

            if (char.IsWhiteSpace(character) && quote == '\0')
            {
                FlushToken(tokens, current);
                continue;
            }

            current.Append(character);
        }

        FlushToken(tokens, current);
        return tokens;
    }

    private static bool IsFlag(string token) =>
        !NumericTokenRegex().IsMatch(token) &&
        (token.StartsWith("--", StringComparison.Ordinal) ||
         (token.StartsWith("-", StringComparison.Ordinal) && token.Length > 1) ||
         (token.StartsWith("/", StringComparison.Ordinal) && token.Length > 1));

    private static void FlushLine(List<string> lines, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }
        lines.Add(current.ToString());
        current.Clear();
    }

    private static void FlushToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }
        tokens.Add(current.ToString());
        current.Clear();
    }
}
