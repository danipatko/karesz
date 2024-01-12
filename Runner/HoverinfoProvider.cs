using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace karesz.Runner
{
    public partial class HoverinfoProvider
    {
        static SemanticModel? SemanticModel = null;

        // by default, the formatter barely displays any information
        private static readonly SymbolDisplayFormat SymbolFmt = new SymbolDisplayFormat()
                .AddParameterOptions(SymbolDisplayParameterOptions.IncludeModifiers | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeOptionalBrackets | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName)
                .AddKindOptions(SymbolDisplayKindOptions.IncludeTypeKeyword)
                .AddMemberOptions(SymbolDisplayMemberOptions.IncludeModifiers | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeRef)
                .AddLocalOptions(SymbolDisplayLocalOptions.IncludeModifiers | SymbolDisplayLocalOptions.IncludeType | SymbolDisplayLocalOptions.IncludeConstantValue)
                .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.IncludeNotNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral | SymbolDisplayMiscellaneousOptions.UseSpecialTypes)
                .AddGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance);

        public static async Task<string?> GetHoverinfoAsync(string code, int offset)
        {
            WorkspaceService.Code = code;
            SemanticModel = await WorkspaceService.Document.GetSemanticModelAsync();
            if (SemanticModel == null) { return null; }

            // the hovered item
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(SemanticModel, offset, WorkspaceService.Workspace);
            if(symbol == null) { return null; }

            // should contain names, type information, visibility modifiers etc.
            return symbol.ToDisplayString(SymbolFmt);
        }
    }
}

