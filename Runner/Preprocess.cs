using System.Text.RegularExpressions;

namespace karesz.Runner
{
    public partial class Preprocess
    {
        // match "using System.Threading.Tasks;"
        [GeneratedRegex(@"\s*using\s+System\s*\.\s*Threading\s*\.\s*Tasks\s*;", RegexOptions.Compiled, "en-150")]
        private static partial Regex UsingTasksRe();

        // match "using karesz.Core;"
        [GeneratedRegex(@"using[\n\s]+karesz[\n\s]*\.[\n\s]*Core[\n\s]*;", RegexOptions.Compiled, "en-150")]
        private static partial Regex UsingKareszRe();

        // match karesz functions that should be converted to async
        [GeneratedRegex(@"(?<name>\w+)\s*\.\s*(?<func>Vegyél_fel_egy_kavicsot|Tegyél_le_egy_kavicsot|Fordulj|Lépj|Teleport|Lőjj|Várj)[\s\n]*\(", RegexOptions.Compiled, "en-150")]
        private static partial Regex KareszFunctionRe();

        // match "<name>.Feladat = delegate ("
        [GeneratedRegex(@"(?<name>[^\s\n]+)[\s\n]*\.[\s\n]*Feladat[\s\n]*=[\s\n]*delegate[\s\n]*\(", RegexOptions.Compiled, "en-150")]
        private static partial Regex KareszFeladatRe();

        // match "public void DIÁK_ROBOTJAI("
        [GeneratedRegex(@"public[\s\n]+(|override[\s\n]+)void[\s\n]+DIÁK_ROBOTJAI[\s\n]*\(", RegexOptions.Compiled, "en-150")]
        private static partial Regex OverrideRe();

        private const string USING_TASKS = "using System.Threading.Tasks;\n";
        private const string USING_KARESZ = "using karesz.Core;\n";
        private const string AWAIT_PREFIX = "await ";
        private const string AWAIT_SUFFIX = "Async";
        public const string DIAK_ROBOTJAI = nameof(DIAK_ROBOTJAI);

        public static string Asyncronize(string code)
        {
            if (!UsingTasksRe().IsMatch(code))
                code = USING_TASKS + code;

            if (!UsingKareszRe().IsMatch(code))
                code = USING_TASKS + code;

            code = KareszFunctionRe().Replace(code, $"{AWAIT_PREFIX}$1.$2{AWAIT_SUFFIX}(");
            code = KareszFeladatRe().Replace(code, $"$1.Feladat = async delegate(");
            // NOTE:
            // The A instead of Á in DIÁK_ is intentional, because InvokeMember seems to be 
            // unable to find methods with a capital non-ascii letter in the name. Weird.
            code = OverrideRe().Replace(code, $"public void {DIAK_ROBOTJAI}(");

            return code;
        }
    }
}
