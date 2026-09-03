using System.Text;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration;

/// <summary>An option and its zero-based position in the original parameter string.</summary>
public sealed record ConfigCommandOption(
    string Name,
    string DisplayText,
    string Value,
    int SourceIndex = -1,
    int SourceLength = 0,
    int SourceLine = 0,
    int SourceColumn = 0);

/// <summary>Lexical analysis only; does not invoke a shell or execute parameters.</summary>
public static class ConfigCommandLine
{
    public static IReadOnlyList<string> Tokenize(string? commandLine)
    {
        List<string> tokens = [];
        StringBuilder current = new();
        char quote = '\0';
        int backslashes = 0;
        foreach (char character in commandLine ?? string.Empty)
        {
            if ((character is '"' or '\'') &&
                (quote == '\0' || quote == character) && backslashes % 2 == 0)
            {
                quote = quote == character ? '\0' : character;
            }
            if (char.IsWhiteSpace(character) && quote == '\0')
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(character);
            }
            backslashes = character == '\\' ? backslashes + 1 : 0;
        }
        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }
        return tokens;
    }

    public static IReadOnlyList<ConfigCommandOption> ParseOptions(string? commandText)
    {
        commandText ??= string.Empty;
        IReadOnlyList<string> tokens = Tokenize(commandText);
        List<ConfigCommandOption> result = [];
        int searchIndex = 0;
        for (int index = 0; index < tokens.Count; index++)
        {
            string token = tokens[index];
            if (!TryGetOptionName(token, out string name, out int equalsIndex))
            {
                continue;
            }
            string value = equalsIndex >= 0 ? token[(equalsIndex + 1)..] : string.Empty;
            string displayText = token;
            int sourceIndex = commandText.IndexOf(token, searchIndex, StringComparison.Ordinal);
            int sourceEnd = sourceIndex >= 0 ? sourceIndex + token.Length : -1;
            if (equalsIndex < 0 && index + 1 < tokens.Count && !IsOption(tokens[index + 1]))
            {
                value = tokens[++index];
                displayText = $"{token} {value}";
                int valueIndex = commandText.IndexOf(value, Math.Max(searchIndex, sourceEnd), StringComparison.Ordinal);
                if (valueIndex >= 0)
                {
                    sourceEnd = valueIndex + value.Length;
                }
            }
            int sourceLength = sourceIndex >= 0 && sourceEnd >= sourceIndex ? sourceEnd - sourceIndex : 0;
            int line = 0;
            int lineStart = 0;
            for (int character = 0; character < sourceIndex; character++)
            {
                if (commandText[character] == '\n')
                {
                    line++;
                    lineStart = character + 1;
                }
            }
            result.Add(new ConfigCommandOption(name, displayText, value, sourceIndex, sourceLength,
                line, Math.Max(0, sourceIndex - lineStart)));
            if (sourceEnd >= 0)
            {
                searchIndex = sourceEnd;
            }
        }
        return result;
    }

    public static bool TryGetOptionName(string token, out string name, out int equalsIndex)
    {
        equalsIndex = token.IndexOf('=');
        name = equalsIndex >= 0 ? token[..equalsIndex] : token;
        return IsOption(name);
    }

    public static bool IsOption(string token) => token.Length > 1 && token[0] == '-';

    public static string Unquote(string? value)
    {
        string result = (value ?? string.Empty).Trim();
        if (result.Length >= 2 && ((result[0] == '"' && result[^1] == '"') ||
            (result[0] == '\'' && result[^1] == '\'')))
        {
            result = result[1..^1];
        }
        return result.Replace("\\\"", "\"");
    }

    public static string Quote(string value, bool force = false) =>
        force || value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
