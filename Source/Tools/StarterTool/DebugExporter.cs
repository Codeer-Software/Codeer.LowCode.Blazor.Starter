using System.Text.RegularExpressions;

namespace StarterTool
{
    /// <summary>
    /// Copies the Normal / Cookie masters and Hosts/Common into the Codeer.LowCode.Blazor repository
    /// (Source/App/{Normal,Cookie,Common}) as debug copies: the Codeer.LowCode.Blazor* packages that are built in that
    /// repository become project references so the framework can be debugged through a real application.
    /// The copies are generated; edits belong in the Starter masters.
    /// The folder layout mirrors Hosts/, so the "..\..\Common\X\X.csproj" references work unchanged.
    /// </summary>
    public class DebugExporter
    {
        readonly string _root;

        //Package -> project inside Codeer.LowCode.Blazor/Source. Extras and DbAccess live in other repositories and stay packages.
        static readonly Dictionary<string, string> PackageToProject = new()
        {
            ["Codeer.LowCode.Blazor"] = @"Codeer.LowCode.Blazor\Codeer.LowCode.Blazor.csproj",
            ["Codeer.LowCode.Blazor.Designer"] = @"Codeer.LowCode.Blazor.Designer\Codeer.LowCode.Blazor.Designer.csproj",
            ["Codeer.LowCode.Blazor.Licensing"] = @"Codeer.LowCode.Blazor.Licensing\Codeer.LowCode.Blazor.Licensing.csproj",
        };

        public DebugExporter(string root)
        {
            _root = root;
        }

        public void Run(string mainSourceDir)
        {
            if (!File.Exists(Path.Combine(mainSourceDir, "Codeer.LowCode.Blazor.sln")))
                throw new InvalidOperationException($"Not the Codeer.LowCode.Blazor/Source folder: {mainSourceDir}");

            var appDir = Path.Combine(mainSourceDir, "App");
            var variants = Variant.All.Where(v => Variant.DebugVariants.Contains(v.Name)).ToList();

            var commonNames = variants.SelectMany(v => v.Projects)
                .Where(p => p.Source == ProjectSource.Common && !Variant.NotForDebug.Contains(p.Name))
                .Select(p => p.ProjectName).Distinct();
            foreach (var name in commonNames)
            {
                Export(Path.Combine(Repository.Hosts(_root), Repository.CommonDir, name), Path.Combine(appDir, Repository.CommonDir, name));
            }

            foreach (var variant in variants)
            {
                var variantDst = Path.Combine(appDir, variant.Name);
                var own = variant.Projects.Where(p => p.Source == ProjectSource.Own).ToList();
                foreach (var project in own)
                {
                    Export(project.MasterDir(_root, variant.Name), Path.Combine(variantDst, project.ProjectName));
                }
                //Anything else in the variant folder is a leftover of an older layout.
                if (Directory.Exists(variantDst))
                {
                    foreach (var dir in Directory.GetDirectories(variantDst))
                    {
                        if (!own.Any(p => p.ProjectName.Equals(Path.GetFileName(dir), StringComparison.OrdinalIgnoreCase)))
                            Repository.DeleteDirectory(dir);
                    }
                }
                Console.WriteLine($"exported {variant.Name}");
            }
        }

        //Files that belong to the framework repository's own environment (test databases, deploy targets, ports).
        //They are kept as they are in the debug copy; only the first export seeds them from the Starter defaults.
        static readonly string[] LocalFiles = { "appsettings.json", "appsettings.Development.json", @"Properties\launchSettings.json" };
        static readonly string[] LocalDirs = { @"Properties\PublishProfiles", @"Properties\ServiceDependencies" };

        static void Export(string src, string dst)
        {
            var keep = new Dictionary<string, byte[]>();
            if (Directory.Exists(dst))
            {
                foreach (var file in LocalFiles.Select(f => Path.Combine(dst, f)).Where(File.Exists))
                    keep[Path.GetRelativePath(dst, file)] = File.ReadAllBytes(file);
                foreach (var dir in LocalDirs.Select(d => Path.Combine(dst, d)).Where(Directory.Exists))
                    foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                        keep[Path.GetRelativePath(dst, file)] = File.ReadAllBytes(file);
            }

            Repository.DeleteDirectory(dst);
            Repository.CopyProject(src, dst, (relative, text) =>
                relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? ToProjectReferences(text) : text);

            foreach (var (relative, bytes) in keep)
            {
                var target = Path.Combine(dst, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, bytes);
            }
        }

        static string ToProjectReferences(string text)
        {
            //Both App\<Variant>\<Project> and App\Common\<Project> are three levels below Source.
            const string toSource = @"..\..\..\";
            foreach (var (package, project) in PackageToProject)
            {
                text = Regex.Replace(text,
                    $@"<PackageReference Include=""{Regex.Escape(package)}"" Version=""[^""]*"" />",
                    $@"<ProjectReference Include=""{toSource}{project}"" />");
            }
            return text;
        }
    }
}
