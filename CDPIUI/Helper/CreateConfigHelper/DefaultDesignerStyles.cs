using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using TextControlBoxNS;
using Windows.UI;

namespace CDPIUI.Helper.CreateConfigHelper
{
    internal class DarkDefaultHighlighter : SyntaxHighlightLanguage
    {
        public DarkDefaultHighlighter(
            IEnumerable<string> errorTokens = null,
            IEnumerable<string> warningTokens = null)
        {
            Name = "CMD Prompt";
            Author = "Storik4";
            Filter = [];
            Description = "Syntax highlighting for CMD";
            Highlights =
            [
                new SyntaxHighlights("(\\*)", "#dd0077", "#dd0077"),
                new SyntaxHighlights("(%.+?%)", "#5fe354", "#5fe354"),
                new SyntaxHighlights("((--(\\w+(-)*)+)|(-(\\w+(-)*)+))(\\s|$|=)", "#888888", "#888888"),
                new SyntaxHighlights("(--new)|(-A.*?(?:\\s|^|=))|(--auto.*?(?:\\s|^|=))", "#ffffff", "#ffffff"),
                new SyntaxHighlights("\\b([+-]?(?=\\.\\d|\\d)(?:\\d+)?(?:\\.?\\d*))(?:[eE]([+-]?\\d+))?\\b", "#ff6b84", "#ff6b84"),
                new SyntaxHighlights("(\\\".+?\\\"|\\'.+?\\')", "#6e86ff", "#6e86ff"),
                .. DiagnosticHighlights.Create(warningTokens, "#CA5010"),
                .. DiagnosticHighlights.Create(errorTokens, "#C42B1C"),
            ];
        }
    }
    internal class LightDefaultHighlighter : SyntaxHighlightLanguage
    {
        public LightDefaultHighlighter(
            IEnumerable<string> errorTokens = null,
            IEnumerable<string> warningTokens = null)
        {
            Name = "CMD Prompt";
            Author = "Storik4";
            Filter = [];
            Description = "Syntax highlighting for CMD";
            Highlights =
            [
                new SyntaxHighlights("(:.*)", "#00C000", "#00C000"),
                new SyntaxHighlights("(\\*)", "#dd0077", "#dd0077"),
                new SyntaxHighlights("(%.+?%)", "#dd0077", "#dd0077"),
                new SyntaxHighlights("((--(\\w+(-)*)+)|(-(\\w+(-)*)+))(\\s|$|=)", "#888888", "#888888"),
                new SyntaxHighlights("(--new)|(-A.*?(?:\\s|^|=))|(--auto.*?(?:\\s|^|=))", "#000000", "#000000"),
                new SyntaxHighlights("\\b([+-]?(?=\\.\\d|\\d)(?:\\d+)?(?:\\.?\\d*))(?:[eE]([+-]?\\d+))?\\b", "#dd00dd", "#dd00dd"),
                new SyntaxHighlights("(\\\".+?\\\"|\\'.+?\\')", "#00C000", "#00C000"),
                .. DiagnosticHighlights.Create(warningTokens, "#CA5010"),
                .. DiagnosticHighlights.Create(errorTokens, "#C42B1C"),
            ];
        }
    }

    internal static class DiagnosticHighlights
    {
        public static IReadOnlyList<SyntaxHighlights> Create(
            IEnumerable<string> tokens,
            string color)
        {
            string[] escapedTokens = (tokens ?? [])
                .Where(token => !string.IsNullOrWhiteSpace(token) && token.Length > 1)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(Regex.Escape)
                .OrderByDescending(token => token.Length)
                .ToArray();
            if (escapedTokens.Length == 0)
            {
                return [];
            }

            string pattern = $"(?<![A-Za-z0-9_.-])(?:{string.Join('|', escapedTokens)})(?=$|[\\s=])";
            return [new SyntaxHighlights(pattern, color, color, false, false, true)];
        }
    }

    public static class TextControlBoxDesigns
    {
        public static TextControlBoxDesign DefaultLightDesign = new(
            background: new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
            textColor: Color.FromArgb(255, 50, 50, 50),
            selectionColor: Color.FromArgb(100, 0, 100, 255),
            cursorColor: Color.FromArgb(255, 0, 0, 0),
            lineHighlighterColor: Color.FromArgb(50, 200, 200, 200),
            lineNumberColor: Color.FromArgb(255, 180, 180, 180),
            lineNumberBackground: Color.FromArgb(0, 0, 0, 0),
            searchHighlightColor: Color.FromArgb(100, 200, 120, 0)
            );
        public static TextControlBoxDesign DefaultDarkDesign = new(
            background: new SolidColorBrush(Color.FromArgb(0, 30, 30, 30)),
            textColor: Color.FromArgb(255, 255, 255, 255),
            selectionColor: Color.FromArgb(100, 0, 100, 255),
            cursorColor: Color.FromArgb(255, 255, 255, 255),
            lineHighlighterColor: Color.FromArgb(50, 100, 100, 100),
            lineNumberColor: Color.FromArgb(255, 100, 100, 100),
            lineNumberBackground: Color.FromArgb(0, 0, 0, 0),
            searchHighlightColor: Color.FromArgb(100, 160, 80, 0)
            );
    }
}
