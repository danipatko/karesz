using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis.Completion;

namespace karesz.Runner
{
    public class WorkspaceService
    {
        static readonly AdhocWorkspace Workspace = new(MefHostServices.DefaultHost);

        public static List<PortableExecutableReference> BasicReferenceAssemblies;

        public static Document Document;

        // private static LanguageProvider Provider = new();

        // edit allowed references here
        static readonly Assembly[] basicReferenceAssemblyRoots =
            {
                typeof(PortableExecutableKinds).Assembly,
                typeof(CompletionService).Assembly,
                typeof(Console).Assembly, // System.Console
                typeof(IQueryable).Assembly, // System.Linq.Expressions
                typeof(Thread).Assembly, // System.Threading
                // typeof(object).Assembly, // everything?!
                typeof(CompilerSerivce).Assembly // swap to karesz engine
            };

        public static async Task InitAsync(HttpClient httpClient)
        {
            await GetReferences(httpClient);

            var projectInfo = ProjectInfo
                .Create(ProjectId.CreateNewId(), VersionStamp.Create(), PROJECT_NAME, PROJECT_NAME, LanguageNames.CSharp)
                .WithMetadataReferences(BasicReferenceAssemblies);

            var project = Workspace.AddProject(projectInfo);
            Document = Workspace.AddDocument(project.Id, DEFAULT_DOCUMENT_NAME, SourceText.From(DEFAULT_TEMPLATE));
        }

        private static async Task GetReferences(HttpClient httpClient)
        {
            var assemblyNames = basicReferenceAssemblyRoots
                .SelectMany(assembly => assembly.GetReferencedAssemblies().Concat(new[] { assembly.GetName() }))
                .Select(x => x.Name)
                .Distinct()
                .ToList();

            var assemblyStreams = await GetStreamFromHttpAsync(httpClient, assemblyNames!);
            var allReferenceAssemblies = assemblyStreams.ToDictionary(a => a.Key, a => MetadataReference.CreateFromStream(a.Value));

            BasicReferenceAssemblies = allReferenceAssemblies
                .Where(a => basicReferenceAssemblyRoots
                    .Select(x => x.GetName().Name)
                    .Union(basicReferenceAssemblyRoots.SelectMany(assembly => assembly.GetReferencedAssemblies().Select(z => z.Name)))
                    .Any(n => n == a.Key))
                .Select(a => a.Value)
                .ToList();
        }

        private static async Task<IDictionary<string, Stream>> GetStreamFromHttpAsync(HttpClient httpClient, IEnumerable<string> assemblyNames)
        {
            var streams = new ConcurrentDictionary<string, Stream>();

            await Task.WhenAll(
                assemblyNames.Select(async assemblyName =>
                {
                    var result = await httpClient.GetAsync($"/_framework/{assemblyName}.dll");
                    result.EnsureSuccessStatusCode();
                    streams.TryAdd(assemblyName, await result.Content.ReadAsStreamAsync());
                }));

            return streams;
        }

        public static async Task TabComplete(string code, int offset)
        {
            Document = Document.WithText(SourceText.From(code));
            await LanguageProvider.GetCompletionItems(Document, offset);
        }

        // CONSTANTS
        const string PROJECT_NAME = "Karesz";
        const string DEFAULT_DOCUMENT_NAME = "Diak.cs";
        const string DEFAULT_TEMPLATE = @"using System;
using karesz.Runner;

namespace MyApp
{
    class Program
    {
        static void Main(string args)
        {
            Console.WriteLine(""Hello World!"");

            var cs = new CompilerSerivce();
            Console.WriteLine(cs.ToString());

            CompilerSerivce.EpicTestFunction();
        }
    }
}
";

    }
}
