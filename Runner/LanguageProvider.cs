using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Recommendations;

namespace karesz.Runner
{
    public class LanguageProvider
    {
        private static CompletionService? CompletionService;

        // relevant CompletionItem.Property keys
        private const string InsertionText = nameof(InsertionText); // same as: "InsertionText"
        private const string SymbolKind = nameof(SymbolKind);
        private const string SymbolName = nameof(SymbolName);
        private const string ShouldProvideParenthesisCompletion = nameof(ShouldProvideParenthesisCompletion);

        public static async Task GetCompletionItems(string code, int offset)
        {
            WorkspaceService.Code = code;

            CompletionService ??= CompletionService.GetService(WorkspaceService.Document);
            if (CompletionService == null) return;

            var wordToComplete = GetPartialWord(code, offset);

            var suggestedCompletions = await CompletionService.GetCompletionsAsync(WorkspaceService.Document, offset).ConfigureAwait(false);
            var result = suggestedCompletions.ItemsList
                .Where(ci => ci.FilterText.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
                .Select(TryConvertToSuggestion);
 
            Console.WriteLine(string.Join("\n", result));
        }

        private static Suggestion TryConvertToSuggestion(CompletionItem ci)
        {
            Console.WriteLine(string.Join(", ", ci.Properties.Keys));
            Console.WriteLine(string.Join(", ", ci.Properties.Values));

            var insertionText = ci.Properties.GetValueOrDefault(InsertionText, ci.DisplayText);

            var kind = !ci.Properties.TryGetValue(SymbolKind, out var symbolKindString) 
                ? MonacoSymbolKind.Key
                : MapSymbolKinds.GetValueOrDefault((SymbolKind)int.Parse(symbolKindString!), MonacoSymbolKind.Object);

            bool parenthesis = ci.Properties.TryGetValue(ShouldProvideParenthesisCompletion, out var p) && p == bool.TrueString;

            return new Suggestion {
                SymbolKind = kind, 
                InsertionText = parenthesis ? insertionText + "(${1})" : insertionText, // add parenthesis and move cursor
                Description = ci.InlineDescription, 
                DisplayText = ci.DisplayText
            };
        }

        public class Suggestion
        {
            public MonacoSymbolKind SymbolKind { get; set; }
            public string InsertionText { get; set; }
            public string Description { get; set; }
            public string DisplayText { get; set; }
            public override string ToString() => $"{SymbolKind} {DisplayText} | insert {InsertionText} | desc {Description}";
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

