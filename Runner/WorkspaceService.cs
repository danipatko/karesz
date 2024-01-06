using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using System.Collections.Concurrent;
using System.Reflection;
using karesz.Core;

namespace karesz.Runner
{
    public class WorkspaceService
    {
        public static readonly AdhocWorkspace Workspace = new(MefHostServices.DefaultHost);

        // can't be assigned in costructor as the loading process is async
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public static List<PortableExecutableReference> BasicReferenceAssemblies;
        public static Document Document { get; private set; }
        public static Project Project { get; private set; }
#pragma warning restore CS8618

        private static string _code = string.Empty;

        public static string Code { 
            set
            {
                _code = value;
                Document = Document.WithText(SourceText.From(value));
            }    
            get => _code;
        }

        // Edit allowed references here
        static readonly Assembly[] basicReferenceAssemblyRoots =
            [
                typeof(Task).Assembly,  // System.Threading.Tasks
                typeof(Console).Assembly, // System.Console
                typeof(IQueryable).Assembly, // System.Linq.Expressions
                // typeof(object).Assembly, // everything?!
                typeof(Robot).Assembly,
                typeof(Karesz.Form).Assembly,
                typeof(Karesz.Form1).Assembly,
            ];
        
        public static async Task InitAsync(HttpClient httpClient)
        {
            // load assemblies
            await GetReferencesAsync(httpClient);
            // create 'virutal' project (needed for autocomplete services)
            var projectInfo = ProjectInfo
                .Create(ProjectId.CreateNewId(), VersionStamp.Create(), PROJECT_NAME, PROJECT_NAME, LanguageNames.CSharp)
                .WithMetadataReferences(BasicReferenceAssemblies);

            Project = Workspace.AddProject(projectInfo);
            // document is the snippet edited by the user
            // only a single document is used (as Diak.cs)
            Document = Workspace.AddDocument(Project.Id, DEFAULT_DOCUMENT_NAME, SourceText.From(DEFAULT_TEMPLATE));
            Code = DEFAULT_TEMPLATE;
        }

        private static async Task GetReferencesAsync(HttpClient httpClient)
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

        // CONSTANTS
        public const string PROJECT_NAME = nameof(Karesz);
        public const string DEFAULT_DOCUMENT_NAME = "Diak.cs";
        public const string DEFAULT_TEMPLATE = $@"using System;

namespace {nameof(Karesz)}
{{
    using {nameof(karesz)}.{nameof(Core)};
    
    public partial class {nameof(Karesz.Form1)} : {nameof(Karesz.Form)}
    {{
        public void {nameof(Karesz.Form1.DIÁK_ROBOTJAI)}()
        {{
            var karesz = Robot.Get(""Karesz"");

            karesz.Feladat = delegate () {{
                karesz.Fordulj(jobbra);

                while(!karesz.Ki_fog_lépni_a_pályáról()) {{
                    karesz.Lépj();
                    karesz.Tegyél_le_egy_kavicsot(fekete);
                }}

                // suicide test
                karesz.Lépj();

                Console.Error.WriteLine(""exiting"");
            }};
        }}
    }}
}}
";
    }
}
