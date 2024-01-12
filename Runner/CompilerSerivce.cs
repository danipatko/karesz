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

        private const BindingFlags BINDING_FLAGS = BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance;
        private const string TARGET_TYPE = $"{nameof(Karesz)}.{nameof(Karesz.Form1)}";
        private const string TARGET_METHOD = Preprocess.DIAK_ROBOTJAI;  // encoding issue
        private const string DEFAULT_ROOT_NAMESPACE = $"{nameof(karesz)}.{nameof(Runner)}"; // same as this namespace (assembly is loaded here)

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

        public static async Task InitAsync(List<PortableExecutableReference> basicReferenceAssemblies)
        {
            BaseCompilation = CSharpCompilation.Create(DEFAULT_ROOT_NAMESPACE, Array.Empty<SyntaxTree>(), basicReferenceAssemblies, compilationOptions);
            CSharpParseOptions = new CSharpParseOptions(LanguageVersion.Preview);
            // launch base compile on startup to speed up things a bit
            await CompileAsync(WorkspaceService.DEFAULT_TEMPLATE);
        }

        /// <summary>
        /// Updates WorkspaceService.Code, and compiles code.
        /// If successful, saves assembly binary to AssemblyBytes, which can be loaded runtime.
        /// </summary>
        public static async Task<EmitResult> CompileAsync(string code, CompilationMode mode = default, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            WorkspaceService.Code = mode == CompilationMode.Async ? Preprocess.Asyncronize(code) : code;
            var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(WorkspaceService.Code), cancellationToken: cancellationToken);
            var compilation = BaseCompilation.AddSyntaxTrees(syntaxTree);

            MemoryStream ms = new();
            EmitResult result = compilation.Emit(ms, cancellationToken: cancellationToken);

            if (result.Success)
            {
                ms.Seek(0, SeekOrigin.Begin);
                AssemblyBytes = ms.ToArray();
            }

            return result;
        }

        /// <summary>
        /// Loads the assemby from the last successful compilation and invokes it's entry point
        /// <returns>true on success</returns>
        /// </summary>
        public static bool LoadAndInvoke()
        {
            if (!HasAssembly)
                return false;

            try
            {
                var assembly = Assembly.Load(AssemblyBytes);
                // creates a Karesz.Form1 class instance
                var type = assembly.GetType(TARGET_TYPE, true, true)!;
                var instance = Activator.CreateInstance(type);
                // invoke DIÁK_ROBOTJAI method
                type.InvokeMember(TARGET_METHOD, BINDING_FLAGS, null, instance, null);
                return true;
            }
            catch (TargetInvocationException e)
            {
                Console.WriteLine("{0}\n{1}", e.InnerException!.Message, e.InnerException.StackTrace);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to find entry point.\n{0}", e.Message);
            }

            return false;
        }
    }
}
