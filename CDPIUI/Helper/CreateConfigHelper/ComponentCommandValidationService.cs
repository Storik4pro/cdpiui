using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CDPIUI.Helper.CreateConfigHelper;

public enum ComponentCommandDiagnosticSeverity
{
    Error,
    Warning,
}

public enum ComponentCommandDiagnosticKind
{
    UnknownFlag,
    MissingRequiredArgument,
    UnterminatedQuote,
}

public sealed class ComponentCommandDiagnostic
{
    public string Code { get; init; } = string.Empty;
    public ComponentCommandDiagnosticKind Kind { get; init; }
    public ComponentCommandDiagnosticSeverity Severity { get; init; }
    public string Token { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Column { get; init; }
}

public static partial class ComponentCommandValidationService
{
    [GeneratedRegex(@"(?<!\S)(?<flag>-{1,2}[A-Za-z0-9][A-Za-z0-9_.-]*|/[A-Za-z?][A-Za-z0-9?_.-]*)(?<equals>=)?(?<value>[^\s]*)")]
    private static partial Regex FlagRegex();

    public static IReadOnlyList<ComponentCommandDiagnostic> Validate(
        string commandText,
        IReadOnlyList<ComponentCommandHelpOption> knownOptions)
    {
        List<ComponentCommandDiagnostic> diagnostics = [];
        string normalizedText = (commandText ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string[] lines = normalizedText.Split('\n');
        bool hasReference = knownOptions?.Count > 0;

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            foreach (Match match in FlagRegex().Matches(line))
            {
                string flag = match.Groups["flag"].Value;
                ComponentCommandHelpOption option = knownOptions?.FirstOrDefault(item => item.Matches(flag));
                if (option == null)
                {
                    bool componentUsesSlashOptions = knownOptions?.Any(item =>
                        item.Names.Any(name => name.StartsWith("/", StringComparison.Ordinal))) == true;
                    if (hasReference &&
                        (!flag.StartsWith("/", StringComparison.Ordinal) || componentUsesSlashOptions))
                    {
                        diagnostics.Add(new ComponentCommandDiagnostic
                        {
                            Code = "CFG001",
                            Kind = ComponentCommandDiagnosticKind.UnknownFlag,
                            Severity = ComponentCommandDiagnosticSeverity.Warning,
                            Token = flag,
                            Line = lineIndex + 1,
                            Column = match.Groups["flag"].Index + 1,
                        });
                    }
                    continue;
                }

                if (!option.IsArgumentRequired || HasRequiredArgument(line, match, option, flag))
                {
                    continue;
                }

                diagnostics.Add(new ComponentCommandDiagnostic
                {
                    Code = "CFG002",
                    Kind = ComponentCommandDiagnosticKind.MissingRequiredArgument,
                    Severity = ComponentCommandDiagnosticSeverity.Error,
                    Token = flag,
                    Line = lineIndex + 1,
                    Column = match.Groups["flag"].Index + 1,
                });
            }

            AddUnterminatedQuoteDiagnostic(line, lineIndex, diagnostics);
        }

        return diagnostics
            .OrderBy(item => item.Line)
            .ThenBy(item => item.Column)
            .ThenBy(item => item.Severity)
            .ToList();
    }

    private static bool HasRequiredArgument(
        string line,
        Match match,
        ComponentCommandHelpOption option,
        string flag)
    {
        bool usesEquals = option.Syntax.Contains($"{flag}=", StringComparison.OrdinalIgnoreCase);
        if (usesEquals)
        {
            return match.Groups["equals"].Success &&
                !string.IsNullOrWhiteSpace(match.Groups["value"].Value);
        }

        int remainderStart = match.Index + match.Length;
        string remainder = remainderStart < line.Length
            ? line[remainderStart..].TrimStart()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        Match nextFlag = FlagRegex().Match(remainder);
        return !nextFlag.Success || nextFlag.Index != 0;
    }

    private static void AddUnterminatedQuoteDiagnostic(
        string line,
        int lineIndex,
        ICollection<ComponentCommandDiagnostic> diagnostics)
    {
        char activeQuote = '\0';
        int openingColumn = -1;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character != '\'' && character != '"')
            {
                continue;
            }

            int precedingBackslashes = 0;
            for (int previous = index - 1; previous >= 0 && line[previous] == '\\'; previous--)
            {
                precedingBackslashes++;
            }
            if (precedingBackslashes % 2 != 0)
            {
                continue;
            }

            if (activeQuote == '\0')
            {
                activeQuote = character;
                openingColumn = index;
            }
            else if (activeQuote == character)
            {
                activeQuote = '\0';
                openingColumn = -1;
            }
        }

        if (activeQuote == '\0')
        {
            return;
        }

        diagnostics.Add(new ComponentCommandDiagnostic
        {
            Code = "CFG003",
            Kind = ComponentCommandDiagnosticKind.UnterminatedQuote,
            Severity = ComponentCommandDiagnosticSeverity.Error,
            Token = activeQuote.ToString(),
            Line = lineIndex + 1,
            Column = openingColumn + 1,
        });
    }
}
