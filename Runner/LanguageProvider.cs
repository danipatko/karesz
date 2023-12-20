using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using System.Text.RegularExpressions;

namespace karesz.Runner
{
    public partial class LanguageProvider
    {
        private static CompletionService? CompletionService;

        // relevant CompletionItem.Property keys
        private const string InsertionText = nameof(InsertionText); // same as: "InsertionText"
        private const string SymbolKind = nameof(SymbolKind);
        private const string SymbolName = nameof(SymbolName);
        private const string DescriptionProperty = nameof(DescriptionProperty);
        private const string ShouldProvideParenthesisCompletion = nameof(ShouldProvideParenthesisCompletion);

        public static async Task<IEnumerable<Suggestion>> GetCompletionItems(string code, int offset)
        {
            WorkspaceService.Code = code;

            CompletionService ??= CompletionService.GetService(WorkspaceService.Document);
            if (CompletionService == null) return [];

            var wordToComplete = GetPartialWord(code, offset);

            var suggestedCompletions = await CompletionService.GetCompletionsAsync(WorkspaceService.Document, offset).ConfigureAwait(false);
            return suggestedCompletions.ItemsList
                .Where(ci => string.IsNullOrEmpty(wordToComplete) || ci.FilterText.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
                .Select(TryConvertToSuggestion)
                .Where(x => x != null) as IEnumerable<Suggestion>;
        }

        [GeneratedRegex(@"^Text\|(?<word>\w+)\sKeyword", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-150")]
        private static partial Regex KeywordRe();

        [GeneratedRegex(@"^Text\|(?<word>[^\s\|]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-150")]
        private static partial Regex SnippetRe();

        private static Suggestion? TryConvertToSuggestion(CompletionItem ci)
        {
            if (!ci.Properties.Keys.Any()) return null;

            ci.Properties.TryGetValue(InsertionText, out string? insertionText);
            var kind = ci.Properties.TryGetValue(SymbolKind, out string? symbolKindString)  // is number
                                        ? MapSymbolKinds.GetValueOrDefault((SymbolKind)int.Parse(symbolKindString), MonacoSymbolKind.Object)
                                        : MonacoSymbolKind.None;

            // DescriptionProperty may also include code snippets, but only provides a SnippetId of which I could
            // not find any documentation. Instead, snippets are loaded from JS and do not depend on context.
            if (ci.Properties.TryGetValue(DescriptionProperty, out var description))
            {
                // check for keywords
                var keywordMatches = KeywordRe().Matches(description);
                if (keywordMatches.Count > 0)
                {
                    insertionText ??= keywordMatches[0].Groups["word"].Value;
                    kind = MonacoSymbolKind.Key;
                }
            }

            // Indicates that parenthesis shoud be placed after function name
            bool parenthesis = ci.Properties.TryGetValue(ShouldProvideParenthesisCompletion, out var p) && p == bool.TrueString;

            if (kind == MonacoSymbolKind.None || insertionText == null) 
                return null;

            return new Suggestion {
                Kind = (int)kind,
                InsertText = parenthesis ? insertionText + "(${1})" : insertionText, 
                AsSnippet = parenthesis, // also move cursor
                Documentation = ci.InlineDescription, 
                Label = ci.DisplayText
            };
        }

        public class Suggestion
        {
            public int Kind { get; set; }
            public bool AsSnippet { get; set; }
            public string InsertText { get; set; }
            public string Documentation { get; set; }
            public string Label { get; set; }
        }

        // enum values differ in monaco editor API and Roslyn
        private static readonly Dictionary<SymbolKind, MonacoSymbolKind> MapSymbolKinds = new() {
            { Microsoft.CodeAnalysis.SymbolKind.Method, MonacoSymbolKind.Function },
            { Microsoft.CodeAnalysis.SymbolKind.Local, MonacoSymbolKind.Variable },
            { Microsoft.CodeAnalysis.SymbolKind.Field, MonacoSymbolKind.Field },
            { Microsoft.CodeAnalysis.SymbolKind.ArrayType, MonacoSymbolKind.Array },
            { Microsoft.CodeAnalysis.SymbolKind.NamedType, MonacoSymbolKind.Class },
            { Microsoft.CodeAnalysis.SymbolKind.DynamicType, MonacoSymbolKind.Object },
            { Microsoft.CodeAnalysis.SymbolKind.Parameter, MonacoSymbolKind.TypeParameter },
            { Microsoft.CodeAnalysis.SymbolKind.Property, MonacoSymbolKind.Property },
            { Microsoft.CodeAnalysis.SymbolKind.Event, MonacoSymbolKind.Event },
        };

        // https://microsoft.github.io/monaco-editor/typedoc/enums/languages.SymbolKind.html
        public enum MonacoSymbolKind : int
        {
            None = -1,
            Array = 17,
            Boolean = 16,
            Class = 4,
            Constant = 13,
            Constructor = 8,
            Enum = 9,
            EnumMember = 21,
            Event = 23,
            Field = 7,
            File = 0,
            Function = 11,
            Interface = 10,
            Key = 19,
            Method = 5,
            Module = 1,
            Namespace = 2,
            Null = 20,
            Number = 15,
            Object = 18,
            Operator = 24,
            Package = 3,
            Property = 6,
            String = 14,
            Struct = 22,
            TypeParameter = 25,
            Variable = 12
        }

        private static string GetPartialWord(string code, int offset)
        {
            if (string.IsNullOrEmpty(code) || offset == 0)
                return string.Empty;

            int index = offset;
            while (index >= 1)
            {
                var ch = code[index - 1];
                if (ch != '_' && !char.IsLetterOrDigit(ch)) break;
                index--;
            }

            return code[index..offset];
        }
    }
}

