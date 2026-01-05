using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextControlBoxNS;

namespace CDPI_UI.Helper.CreateConfigHelper
{
    internal class DarkDefaultHighlighter : SyntaxHighlightLanguage
    {
        public DarkDefaultHighlighter()
        {
            this.Name = "CMD Prompt";
            this.Author = "Storik4";
            this.Filter = [];
            this.Description = "Syntax highlighting for CMD";
            this.Highlights =
            [
                new SyntaxHighlights("(\\*)", "#dd0077", "#dd0077"),
                new SyntaxHighlights("(%.+?%)", "#5fe354", "#5fe354"),
                new SyntaxHighlights("((--(\\w+(-)*)+)|(-(\\w+(-)*)+))(\\s|$|=)", "#888888", "#888888"),
                new SyntaxHighlights("(--new)|(-A.*?(?:\\s|^|=))|(--auto.*?(?:\\s|^|=))", "#ffffff", "#ffffff"),
                new SyntaxHighlights("\\b([+-]?(?=\\.\\d|\\d)(?:\\d+)?(?:\\.?\\d*))(?:[eE]([+-]?\\d+))?\\b", "#ff6b84", "#ff6b84"),
                new SyntaxHighlights("(\\\".+?\\\"|\\'.+?\\')", "#6e86ff", "#6e86ff"),
            ];
        }
    }
    internal class LightDefaultHighlighter : SyntaxHighlightLanguage
    {
        public LightDefaultHighlighter()
        {
            this.Name = "CMD Prompt";
            this.Author = "Storik4";
            this.Filter = [];
            this.Description = "Syntax highlighting for CMD";
            this.Highlights =
            [
                new SyntaxHighlights("(:.*)", "#00C000", "#00C000"),
                new SyntaxHighlights("(\\*)", "#dd0077", "#dd0077"),
                new SyntaxHighlights("(%.+?%)", "#dd0077", "#dd0077"),
                new SyntaxHighlights("((--(\\w+(-)*)+)|(-(\\w+(-)*)+))(\\s|$|=)", "#888888", "#888888"),
                new SyntaxHighlights("(--new)|(-A.*?(?:\\s|^|=))|(--auto.*?(?:\\s|^|=))", "#000000", "#000000"),
                new SyntaxHighlights("\\b([+-]?(?=\\.\\d|\\d)(?:\\d+)?(?:\\.?\\d*))(?:[eE]([+-]?\\d+))?\\b", "#dd00dd", "#dd00dd"),
                new SyntaxHighlights("(\\\".+?\\\"|\\'.+?\\')", "#00C000", "#00C000"),
            ];
        }
    }
}
