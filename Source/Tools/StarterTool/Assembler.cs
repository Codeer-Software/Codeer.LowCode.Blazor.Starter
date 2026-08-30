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
            var slnPath = CreateSolution(Repository.Source(_root), Repository.AllSolutionName, entries);
            AddVsixProject(slnPath, Path.Combine(Repository.Tools(_root), Repository.TemplatesProject, Repository.TemplatesProject + ".csproj"), Repository.ToolsDir);
        }

        //The VSIX project is a legacy VSSDK project: "dotnet sln add" cannot evaluate it and "dotnet build" of the solution
        //cannot build it, so it is written by hand with ActiveCfg only (no Build.0). It shows up in VS and is built there
        //explicitly (or with msbuild -t:BuildSplitVsix).
        static void AddVsixProject(string slnPath, string csproj, string folder)
        {
            var text = File.ReadAllText(csproj);
            var guid = System.Text.RegularExpressions.Regex.Match(text, @"<ProjectGuid>(\{[^}]+\})</ProjectGuid>").Groups[1].Value.ToUpperInvariant();
            var name = Path.GetFileNameWithoutExtension(csproj);
            var relative = Path.GetRelativePath(Path.GetDirectoryName(slnPath)!, csproj);
            var lines = File.ReadAllLines(slnPath).ToList();
            var folderGuid = lines.First(l => l.StartsWith("Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\")") && l.Contains($"= \"{folder}\","));
            folderGuid = folderGuid.Substring(folderGuid.LastIndexOf('{')).TrimEnd().TrimEnd('"');

            var global = lines.FindIndex(l => l.StartsWith("Global"));
            lines.Insert(global, "EndProject");
            lines.Insert(global, $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{name}\", \"{relative}\", \"{guid}\"");

            var configs = lines
                .SkipWhile(l => !l.Contains("GlobalSection(SolutionConfigurationPlatforms)")).Skip(1)
                .TakeWhile(l => !l.Contains("EndGlobalSection"))
                .Select(l => l.Trim().Split(" = ")[0])
                .ToList();
            var configEnd = lines.FindIndex(lines.FindIndex(l => l.Contains("GlobalSection(ProjectConfigurationPlatforms)")), l => l.Contains("EndGlobalSection"));
            lines.InsertRange(configEnd, configs.Select(c => $"\t\t{guid}.{c}.ActiveCfg = {c.Split('|')[0]}|Any CPU"));

            var nested = lines.FindIndex(l => l.Contains("GlobalSection(NestedProjects)"));
            lines.Insert(nested + 1, $"\t\t{guid} = {folderGuid}");
            File.WriteAllLines(slnPath, lines);
        }

        static string CreateSolution(string dir, string name, List<(string csproj, string folder, bool deploy)> entries)
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
            StabilizeProjectGuids(slnPath);

            //Mobile projects need Deploy entries or VS refuses to launch them on a device/emulator.
            var deployProjects = entries.Where(e => e.deploy).Select(e => Path.GetFileNameWithoutExtension(e.csproj)).ToList();
            if (deployProjects.Count == 0) return slnPath;
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
            return slnPath;
        }

        //"dotnet sln add" invents a new GUID per SDK-style project on every run, which turns each regeneration into a
        //diff of every solution. Derive the GUID from the project path (relative to the solution) instead so that an
        //unchanged variant produces an unchanged solution.
        static void StabilizeProjectGuids(string slnPath)
        {
            var text = File.ReadAllText(slnPath);
            var projects = System.Text.RegularExpressions.Regex.Matches(text, @"Project\(""\{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC\}""\) = ""[^""]+"", ""([^""]+)"", ""(\{[^}]+\})""");
            foreach (System.Text.RegularExpressions.Match m in projects)
            {
                var relative = m.Groups[1].Value.Replace('/', '\\').ToLowerInvariant();
                var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(relative));
                var stable = "{" + new Guid(hash).ToString().ToUpperInvariant() + "}";
                text = text.Replace(m.Groups[2].Value, stable);
            }
            File.WriteAllText(slnPath, text);
        }
    }
}
