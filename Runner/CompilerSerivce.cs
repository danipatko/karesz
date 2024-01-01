using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Emit;
using System.ComponentModel;
using System.Reflection;

namespace karesz.Runner
{
    public class CompilerSerivce
    {
        [DefaultValue(Sync)]
        public enum CompilationMode : int
        {
            Sync = 0,
            Async = 1
        }

        private static readonly BindingFlags bindingFlags = BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.NonPublic;
        private const string TARGET_TYPE = $"{nameof(Karesz)}.{nameof(Karesz.Form1)}";
        private const string TARGET_METHOD = nameof(Karesz.Form1.DIÁK_ROBOTJAI);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private static CSharpCompilation BaseCompilation;
        private static CSharpParseOptions CSharpParseOptions;
#pragma warning restore CS8618 

        public static byte[] AssemblyBytes { get; private set; } = [];
        public static bool HasAssembly { get => AssemblyBytes.Length > 0; }

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

        public static event EventHandler? CompileStarted;
        public static event EventHandler? CompileFinished;

        public static async Task InitAsync(List<PortableExecutableReference> basicReferenceAssemblies)
        {
            BaseCompilation = CSharpCompilation.Create(nameof(Karesz), Array.Empty<SyntaxTree>(), basicReferenceAssemblies, compilationOptions);
            CSharpParseOptions = new CSharpParseOptions(LanguageVersion.Preview);
            // launch base compile on startup to speed up things a bit
            await CompileAsync(WorkspaceService.DEFAULT_TEMPLATE);
        }

        /// <summary>
        /// Updates WorkspaceService.Code, and compiles code.
        /// If successful, saves assembly binary to AssemblyBytes, which can be loaded runtime.
        /// </summary>
        public static async Task<EmitResult> CompileAsync(string code, CompilationMode mode = default)
        {
            await Task.Yield();

            CompileStarted?.Invoke(null, EventArgs.Empty);

            WorkspaceService.Code = mode == CompilationMode.Async ? Preprocess.Asyncronize(code) : code;
            var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(WorkspaceService.Code));
            var compilation = BaseCompilation.AddSyntaxTrees(syntaxTree);

            MemoryStream ms = new();
            EmitResult result = compilation.Emit(ms);

            if(result.Success)
            {
                ms.Seek(0, SeekOrigin.Begin);
                AssemblyBytes = ms.ToArray();
            }

            CompileFinished?.Invoke(null, EventArgs.Empty);
            return result;
        }

        /// <summary>
        /// Loads the assemby from the last successful compilation and invokes it's entry point
        /// </summary>
        public static void LoadAndInvoke()
        {
            if (!HasAssembly) 
                return;

            Console.WriteLine(TARGET_TYPE);
            Console.WriteLine(TARGET_METHOD);
            Console.WriteLine(typeof(Karesz.Form).AssemblyQualifiedName);
            Console.WriteLine(typeof(Karesz.Form1).AssemblyQualifiedName);

            try
            {
                var assembly = Assembly.Load(AssemblyBytes);
                Console.WriteLine("loaded");

                
                foreach (var item in assembly.ExportedTypes.Select(t => t.FullName))
                {
                    Console.WriteLine(item);
                }

                Console.WriteLine(string.Join("\n", assembly.ExportedTypes.Select(t => t.FullName)));
                Console.WriteLine("hello");
                //Console.WriteLine(string.Join("\n", assembly.ExportedTypes.Select(t => t.FullName)));

                return;

                // creates a Karesz.Form1 class instance
                Type type = assembly.GetType(TARGET_TYPE, true, true)!;
                Console.WriteLine("got type");
                object? targetObj = Activator.CreateInstance(type);
                Console.WriteLine("got instance");
                // invoke DIÁK_ROBOTJAI method
                type.InvokeMember(TARGET_METHOD, bindingFlags, null, targetObj, []);
            }
            catch (Exception e)
            {
                // TODO: add to diagnostics
                Console.Error.WriteLine("Failed to find entry point.\n{0}", e.Message);
            }
        }
    }
}
