using System.Text.RegularExpressions;

namespace karesz.Runner
{
    public partial class Preprocess
    {
        // match "using System.Threading.Tasks;"
        [GeneratedRegex(@"\s*using\s+System\s*\.\s*Threading\s*\.\s*Tasks\s*;", RegexOptions.Compiled, "en-150")]
        private static partial Regex UsingTasksRe();

        // match karesz functions that should be converted to async
        [GeneratedRegex(@"(?<name>\w+)\s*\.\s*(?<func>Vegyél_fel_egy_kavicsot|Tegyél_le_egy_kavicsot|Fordulj|Lépj|Teleport|Lőjj|Várj)[\s\n]*\(", RegexOptions.Compiled, "en-150")]
        private static partial Regex KareszFunctionRe();

        // match "<name>.Feladat = delegate ("
        [GeneratedRegex(@"(?<name>[^\s\n]+)[\s\n]*\.[\s\n]*Feladat[\s\n]*=[\s\n]*delegate[\s\n]*\(", RegexOptions.Compiled, "en-150")]
        private static partial Regex KareszFeladatRe();

        // match "public void DIÁK_ROBOTJAI("
        [GeneratedRegex(@"public[\s\n]+void[\s\n]+DIÁK_ROBOTJAI[\s\n]*\(", RegexOptions.Compiled, "en-150")]
        private static partial Regex OverrideRe();

        private const string USING_TASKS = "using System.Threading.Tasks;\n";
        private const string AWAIT_PREFIX = "await ";
        private const string AWAIT_SUFFIX = "Async";

        public static string Asyncronize(string code)
        {
            if (!UsingTasksRe().IsMatch(code))
                code = USING_TASKS + code;

            code = KareszFunctionRe().Replace(code, $"{AWAIT_PREFIX}$1.$2{AWAIT_SUFFIX}(");
            code = KareszFeladatRe().Replace(code, $"$1.Feladat = async delegate(");
            code = OverrideRe().Replace(code, $"public override void DIÁK_ROBOTJAI(");

            Console.WriteLine("processed code:\n{0}", code);

            return code;
        }
    }
}
