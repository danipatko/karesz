using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Emit;

namespace karesz.Runner
{
    public class CompilationResult
    {
        public CompilationResult(EmitResult result)
        {
            // CompletionService.GetService();

        }

        public static async Task GetCompletions(Document document, int offset)
        {
            var completionService = CompletionService.GetService(document);
            if(completionService == null) {
                return;
            }
            var test = await completionService.GetCompletionsAsync(document, offset);
        }
    }
}
