namespace StarterTool
{
    /// <summary>
    /// (Re)generates the solutions: Source/Hosts/&lt;Variant&gt;/LowCodeApp.sln for every variant and the maintainer
    /// solution Source/Codeer.LowCode.Blazor.Starter.sln with everything. Every project exists exactly once in the
    /// repository (the variant's own projects in its folder, common ones in Hosts/Common, borrowed ones in the owning
    /// variant); the solutions reference them in place, nothing is copied.
    /// </summary>
    public class Assembler
    {
        readonly string _root;

        public Assembler(string root)
        {
            _root = root;
        }

        public void Run()
        {
            foreach (var variant in Variant.All)
            {
                var variantDir = Path.Combine(Repository.Hosts(_root), variant.Name);
                var entries = variant.Projects.Select(p => (Csproj(p, variant), p.SolutionFolder, p.Deploy)).ToList();
                CreateSolution(variantDir, Repository.AppName, entries);
                Console.WriteLine($"assembled {variant.Name}");
            }

            CreateAllSolution();
            Console.WriteLine($"assembled {Repository.AllSolutionName}");
        }

        string Csproj(VariantProject project, Variant variant)
        {
            var path = Path.Combine(project.MasterDir(_root, variant.Name), project.ProjectName + ".csproj");
            if (!File.Exists(path)) throw new InvalidOperationException($"Master project missing: {path}");
            return path;
        }

        //Maintainer view: Common once, each variant's own projects under a folder named after the variant, and the tool.
        //Same-named projects (LowCodeApp.Server of Normal and Cookie) are fine because they sit in different solution folders.
        void CreateAllSolution()
        {
            var entries = new List<(string csproj, string folder, bool deploy)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variant in Variant.All)
            {
                foreach (var project in variant.Projects)
                {
                    var csproj = Csproj(project, variant);
                    if (!seen.Add(csproj)) continue;
                    var folder = $@"{Repository.HostsDir}\{(project.Source == ProjectSource.Common ? Repository.CommonDir : variant.Name)}";
                    entries.Add((csproj, folder, project.Deploy));
                }
            }
            entries.Add((Path.Combine(Repository.Tools(_root), "StarterTool", "StarterTool.csproj"), Repository.ToolsDir, false));
            CreateSolution(Repository.Source(_root), Repository.AllSolutionName, entries);
        }

        static void CreateSolution(string dir, string name, List<(string csproj, string folder, bool deploy)> entries)
        {
            var slnName = name + ".sln";
            var slnPath = Path.Combine(dir, slnName);
            if (File.Exists(slnPath)) File.Delete(slnPath);
            Repository.RunDotnet(dir, $"new sln -n {name} --format sln");
            foreach (var group in entries.GroupBy(e => e.folder))
            {
                var paths = string.Join(" ", group.Select(e => $"\"{Path.GetRelativePath(dir, e.csproj)}\""));
                Repository.RunDotnet(dir, $"sln {slnName} add --solution-folder \"{group.Key}\" {paths}");
            }

            //Mobile projects need Deploy entries or VS refuses to launch them on a device/emulator.
            var deployProjects = entries.Where(e => e.deploy).Select(e => Path.GetFileNameWithoutExtension(e.csproj)).ToList();
            if (deployProjects.Count == 0) return;
            var lines = File.ReadAllLines(slnPath);
            var guids = lines
                .Where(l => l.StartsWith("Project(") && deployProjects.Any(n => l.Contains($"= \"{n}\",")))
                .Select(l => l.Substring(l.LastIndexOf('{')).TrimEnd().TrimEnd('"'))
                .ToList();
            var result = new List<string>();
            foreach (var line in lines)
            {
                result.Add(line);
                var trimmed = line.Trim();
                if (guids.Any(g => trimmed.StartsWith(g)) && trimmed.Contains(".Build.0 = "))
                {
                    result.Add(line.Replace(".Build.0", ".Deploy.0"));
                }
            }
            File.WriteAllLines(slnPath, result);
        }
    }
}
