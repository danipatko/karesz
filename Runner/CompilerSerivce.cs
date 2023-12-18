using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;

namespace karesz.Runner
{
    public class CompilerSerivce
    {
        public const string DefaultRootNamespace = $"{nameof(karesz)}.{nameof(Runner)}";

        private static CSharpCompilation BaseCompilation;
        private static CSharpParseOptions CSharpParseOptions;

        private static byte[] AssemblyBytes = [];

        private static readonly CSharpCompilationOptions compilationOptions = new(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    concurrentBuild: false,
                    //// Warnings CS1701 and CS1702 are disabled when compiling in VS too
                    specificDiagnosticOptions: new[]
                    {
                        new KeyValuePair<string, ReportDiagnostic>("CS1701", ReportDiagnostic.Suppress),
                        new KeyValuePair<string, ReportDiagnostic>("CS1702", ReportDiagnostic.Suppress),
                    });

        private static readonly BindingFlags bindingFlags = BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.InvokeMethod;

        public static async Task InitAsync(List<PortableExecutableReference> basicReferenceAssemblies)
        {
            BaseCompilation = CSharpCompilation.Create(DefaultRootNamespace, Array.Empty<SyntaxTree>(), basicReferenceAssemblies, compilationOptions);
            CSharpParseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        }

        public static async Task Compile(string code) => await Task.Run(() =>
        {
            var sourceCode = SourceText.From(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var compilation = BaseCompilation.AddSyntaxTrees(syntaxTree);

            MemoryStream ms = new();
            EmitResult result = compilation.Emit(ms);

            ms.Seek(0, SeekOrigin.Begin);
            AssemblyBytes = ms.ToArray();

            if (!result.Success)
            {
                Console.WriteLine("BUILD FAILED");
                var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                foreach (Diagnostic diagnostic in failures)
                {
                    var startLinePos = diagnostic.Location.GetLineSpan().StartLinePosition;
                    var err = $"{diagnostic.Severity} on line {startLinePos.Line}:{startLinePos.Character} [{diagnostic.Id}]: {diagnostic.GetMessage()}";
                    Console.Error.WriteLine(err);
                }
            }
            else
            {
                var assembly = Assembly.Load(AssemblyBytes);
                Console.WriteLine(string.Join(", ", assembly.GetTypes().Select(x => x.FullName)));

                Type type = assembly.GetType("MyApp.Program")!;
                // create an instance
                object obj = Activator.CreateInstance(type);
                // call our test function
                var res = (string)type.InvokeMember("Main", bindingFlags, null, obj, new object[] { "Hello World" })!;

                // await Console.Out.WriteLineAsync(">> " + res?.ToString() ?? "raah");
            }
        });
        
        public static void EpicTestFunction()
        {
            Console.WriteLine("HEY! this function has been called from a dynamically loaded assemby!");
        }
    }
}
