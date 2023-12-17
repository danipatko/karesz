using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;
using System.Runtime;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;

namespace karesz.Runner
{
    public class CompilerSerivce
    {
        public const string DefaultRootNamespace = $"{nameof(karesz)}.{nameof(Runner)}";

        private static CSharpCompilation baseCompilation;
        private static CSharpParseOptions cSharpParseOptions;

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

        public static async Task InitAsync(HttpClient httpClient)
        {
            var basicReferenceAssemblyRoots = new[]
            {
                typeof(Console).Assembly, // System.Console
                typeof(Uri).Assembly, // System.Private.Uri
                typeof(AssemblyTargetedPatchBandAttribute).Assembly, // System.Private.CoreLib
                typeof(IQueryable).Assembly, // System.Linq.Expressions
                typeof(IJSRuntime).Assembly, // Microsoft.JSInterop
                typeof(RequiredAttribute).Assembly, // System.ComponentModel.Annotations
                typeof(Thread).Assembly,
                typeof(CompilerSerivce).Assembly
            };

            var assemblyNames = basicReferenceAssemblyRoots
                .SelectMany(assembly => assembly.GetReferencedAssemblies().Concat(new[] { assembly.GetName() }))
                .Select(x => x.Name)
                .Distinct()
                .ToList();

            var assemblyStreams = await GetStreamFromHttpAsync(httpClient, assemblyNames!);
            var allReferenceAssemblies = assemblyStreams.ToDictionary(a => a.Key, a => MetadataReference.CreateFromStream(a.Value));

            var basicReferenceAssemblies = allReferenceAssemblies
                .Where(a => basicReferenceAssemblyRoots
                    .Select(x => x.GetName().Name)
                    .Union(basicReferenceAssemblyRoots.SelectMany(assembly => assembly.GetReferencedAssemblies().Select(z => z.Name)))
                    .Any(n => n == a.Key))
                .Select(a => a.Value)
                .ToList();

            baseCompilation = CSharpCompilation.Create(DefaultRootNamespace, Array.Empty<SyntaxTree>(), basicReferenceAssemblies, compilationOptions);
            cSharpParseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        }

        private static async Task<IDictionary<string, Stream>> GetStreamFromHttpAsync(HttpClient httpClient, IEnumerable<string> assemblyNames)
        {
            var streams = new ConcurrentDictionary<string, Stream>();

            await Task.WhenAll(
                assemblyNames.Select(async assemblyName =>
                {
                    // Console.WriteLine($"loading {assemblyName}");
                    var result = await httpClient.GetAsync($"/_framework/{assemblyName}.dll");
                    result.EnsureSuccessStatusCode();
                    streams.TryAdd(assemblyName, await result.Content.ReadAsStreamAsync());
                }));

            return streams;
        }

        public static async Task Compile(string code) => await Task.Run(() =>
        {
            var sourceCode = SourceText.From(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var compilation = baseCompilation.AddSyntaxTrees(syntaxTree);

            using MemoryStream ms = new();
            EmitResult result = compilation.Emit(ms);

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
                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());

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
