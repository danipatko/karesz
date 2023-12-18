using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;

namespace karesz.Runner
{
    public class LanguageProvider
    {
        static CompletionService? CompletionService;

        public static async Task GetCompletionItems(Document document, int position)
        {
            CompletionService ??= CompletionService.GetService(document);
            if (CompletionService == null)
            {
                await Console.Out.WriteLineAsync("CompletionService was null");
                return;
            }

            var results = await CompletionService.GetCompletionsAsync(document, position);
            await Console.Out.WriteLineAsync(results.ToString());
            if (results == null) return;

            foreach (var item in results.ItemsList)
            {
                await Console.Out.WriteLineAsync($"{item.Rules} | {item.DisplayText} | {item.DisplayTextSuffix}\ndesc: {item.InlineDescription}");
            }
        }
    }
}

//label: keyword,
//kind: monaco.languages.CompletionItemKind.Keyword,
//insertText: keyword
