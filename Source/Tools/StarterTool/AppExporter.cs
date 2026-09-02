using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace StarterTool
{
    /// <summary>
    /// Writes the customer-shaped application folder: what a user gets from the Visual Studio template, plus the
    /// documents Claude Code works with. Used by the Claude Code setup procedure (ClaudeCodeForDeveloper/claude-code-setup.md)
    /// after downloading the repository, so that the user's folder holds only their application, not this repository.
    ///
    ///   &lt;dest&gt;/
    ///     CLAUDE.md                      (maintainer-only blocks removed, paths rewritten)
    ///     ClaudeCodeForDeveloper/claude-code-setup.md
    ///     .vscode/  .gitignore  LICENSE
    ///     Source/LowCodeApp.sln
    ///     Source/LowCodeApp.Server  LowCodeApp.Client  LowCodeApp.Client.Shared  LowCodeApp.Designer  LowCodeApp.LicenseRegisterCli
    ///     (--maui adds LowCodeApp.Maui)
    ///
    /// The projects are the Cookie variant's template projects (VariantProject.InVsix) with project references flattened
    /// to side-by-side folders, exactly like the VSIX template. Paths in the documents are rewritten from the repository
    /// layout (Source/Hosts/Cookie/X, Source/Hosts/Common/X) to the application layout (Source/X).
    /// Text between "&lt;!-- maintainer-only --&gt;" and "&lt;!-- /maintainer-only --&gt;" is dropped.
    ///
    /// The repository itself stays on .NET 8 (the framework's own target). The exported application is moved to the
    /// current .NET (net10.0) and its NuGet packages to the latest stable versions (queried from nuget.org at export
    /// time; offline the versions are kept). --no-upgrade keeps the repository's frameworks and versions.
    /// </summary>
    public class AppExporter
    {
        readonly string _root;
        public const string DefaultVariant = "Cookie";
        public const string TargetNet = "net10.0";

        public AppExporter(string root)
        {
            _root = root;
        }

        public void Run(string dest, bool withMaui, bool upgrade = true)
        {
            var sourceDir = Path.Combine(dest, Repository.SourceDir);
            if (Directory.Exists(sourceDir) && Directory.EnumerateFileSystemEntries(sourceDir).Any())
                throw new InvalidOperationException($"{sourceDir} already exists and is not empty.");
            Directory.CreateDirectory(sourceDir);

            var variant = Variant.All.Single(v => v.Name == DefaultVariant);
            var projects = variant.Projects.Where(p => p.InVsix).Select(p => (project: p, variant: variant.Name)).ToList();
            if (withMaui)
            {
                var maui = Variant.All.Single(v => v.Name == "Maui");
                foreach (var p in maui.Projects.Where(p => p.InVsix && projects.All(x => x.project.Name != p.Name)))
                    projects.Add((p, maui.Name));
            }

            var upgrader = upgrade ? new PackageUpgrader() : null;
            var entries = new List<(string csproj, string folder, bool deploy)>();
            foreach (var (project, variantName) in projects)
            {
                var src = project.MasterDir(_root, variantName);
                var dst = Path.Combine(sourceDir, project.ProjectName);
                Repository.CopyProject(src, dst, (relative, text) =>
                {
                    if (!relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return text;
                    text = Repository.FlattenProjectReferences(text);
                    return upgrader == null ? text : upgrader.Upgrade(text, project.Name);
                });
                entries.Add((Path.Combine(dst, project.ProjectName + ".csproj"), project.SolutionFolder, project.Deploy));
                Console.WriteLine($"exported {project.ProjectName}");
            }
            Assembler.CreateSolution(sourceDir, Repository.AppName, entries);
            Console.WriteLine($"created Source/{Repository.AppName}.sln");
            if (upgrader != null) Console.WriteLine(upgrader.Summary());

            //Documents and settings that belong to the application folder (not to this repository).
            foreach (var relative in new[] { "CLAUDE.md", Path.Combine("ClaudeCodeForDeveloper", "claude-code-setup.md"), ".gitignore", "LICENSE" })
                CopyText(relative, dest, upgrade);
            foreach (var file in Directory.EnumerateFiles(Path.Combine(_root, ".vscode"), "*.json"))
                CopyText(Path.Combine(".vscode", Path.GetFileName(file)), dest, upgrade);
            Console.WriteLine("exported CLAUDE.md, ClaudeCodeForDeveloper/, .vscode/, .gitignore, LICENSE");
        }

        void CopyText(string relative, string dest, bool upgrade)
        {
            var src = Path.Combine(_root, relative);
            if (!File.Exists(src)) return;
            var target = Path.Combine(dest, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, ToAppLayout(File.ReadAllText(src), upgrade), new UTF8Encoding(false));
        }

        /// <summary>Repository layout → application layout, maintainer-only blocks removed, .NET version in paths/text updated.</summary>
        public static string ToAppLayout(string text, bool upgrade)
        {
            text = Regex.Replace(text, @"<!--\s*maintainer-only\s*-->.*?<!--\s*/maintainer-only\s*-->\r?\n?", "", RegexOptions.Singleline);
            //Source/Hosts/Cookie/X, Source/Hosts/Common/X, Source/Hosts/<VARIANT>/X, Source/Hosts/Maui/X -> Source/X (both slash styles, escaped or not)
            text = Regex.Replace(text, @"Source((?:\\\\|\\|/))Hosts\1(?:Cookie|Common|Maui|<VARIANT>|<Variant>)\1", "Source$1");
            if (upgrade)
            {
                //bin/Debug/net8.0(-windows) in launch settings and documents, ".NET 8" / "SDK 8" in prose
                text = text.Replace("net8.0", TargetNet);
                text = Regex.Replace(text, @"\.NET (?:SDK )?8\b", m => m.Value.Replace("8", TargetNet.Substring(3, 2)));
                text = text.Replace("Microsoft.DotNet.SDK.8", "Microsoft.DotNet.SDK." + TargetNet.Substring(3, 2));
                text = text.Replace("-Channel 8.0", "-Channel " + TargetNet.Substring(3));
            }
            return text;
        }
    }

    /// <summary>
    /// Moves a project file to the current .NET and its packages to the latest stable versions on nuget.org.
    /// Rules: Codeer.* untouched (they are the framework versions this Starter was tested with); Microsoft.* runtime
    /// packages follow the target .NET major; Microsoft.CodeAnalysis stays on its current major (the framework compiles
    /// scripts against it); everything else takes the latest stable. Known vulnerable transitive packages are pinned by
    /// adding direct references. Offline: versions are left as they are.
    /// </summary>
    class PackageUpgrader
    {
        static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
        readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);
        readonly List<string> _log = new();
        bool _offline;

        //Direct references added to override vulnerable versions pulled in transitively by the framework packages.
        static readonly (string project, string package, int? major)[] Pins =
        {
            ("Server", "SQLitePCLRaw.bundle_e_sqlite3", null),
            ("Designer", "SQLitePCLRaw.bundle_e_sqlite3", null),
            ("Designer", "Azure.Identity", null),
            ("Designer", "Microsoft.Extensions.Caching.Memory", 10),
        };

        public string Upgrade(string csproj, string projectName)
        {
            csproj = csproj.Replace("<TargetFramework>net8.0-windows</TargetFramework>", $"<TargetFramework>{AppExporter.TargetNet}-windows</TargetFramework>")
                           .Replace("<TargetFramework>net8.0</TargetFramework>", $"<TargetFramework>{AppExporter.TargetNet}</TargetFramework>");
            csproj = Regex.Replace(csproj, @"<PackageReference Include=""([^""]+)"" Version=""([^""]+)""", m =>
            {
                var (package, current) = (m.Groups[1].Value, m.Groups[2].Value);
                var latest = Latest(package, MajorRule(package, current));
                if (latest == null || latest == current) return m.Value;
                _log.Add($"{projectName}: {package} {current} -> {latest}");
                return $@"<PackageReference Include=""{package}"" Version=""{latest}""";
            });
            foreach (var (project, package, major) in Pins)
            {
                if (project != projectName || csproj.Contains($@"Include=""{package}""")) continue;
                var version = Latest(package, major);
                if (version == null) continue;
                var index = csproj.LastIndexOf("</ItemGroup>", StringComparison.Ordinal);
                if (index < 0) continue;
                csproj = csproj.Insert(index, $@"  <PackageReference Include=""{package}"" Version=""{version}"" />{Environment.NewLine}  ");
                _log.Add($"{projectName}: + {package} {version} (pins a vulnerable transitive version)");
            }
            return csproj;
        }

        public string Summary()
            => _offline ? "nuget.org unreachable: package versions kept as in the repository (target framework upgraded)."
             : _log.Count == 0 ? "packages already up to date" : "upgraded packages:" + Environment.NewLine + string.Join(Environment.NewLine, _log.Select(l => "  " + l));

        static int? MajorRule(string package, string current)
        {
            if (package.StartsWith("Codeer.", StringComparison.OrdinalIgnoreCase)) return -1;            // never touch
            if (package.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase)) return int.Parse(current.Split('.')[0]);
            if (package.Equals("Microsoft.AspNetCore.Http.Abstractions", StringComparison.OrdinalIgnoreCase)) return null; // 2.x line, not versioned with the runtime
            if (package.StartsWith("Microsoft.AspNetCore.", StringComparison.OrdinalIgnoreCase)
                || package.StartsWith("Microsoft.Extensions.Configuration", StringComparison.OrdinalIgnoreCase)
                || package.StartsWith("Microsoft.Extensions.Http", StringComparison.OrdinalIgnoreCase)
                || package.StartsWith("Microsoft.Extensions.Logging", StringComparison.OrdinalIgnoreCase)
                || package.StartsWith("Microsoft.Extensions.Caching", StringComparison.OrdinalIgnoreCase)
                || package.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
                || package.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                return int.Parse(AppExporter.TargetNet.Substring(3, 2));                                  // follow the runtime
            if (package.Equals("NUnit", StringComparison.OrdinalIgnoreCase)) return int.Parse(current.Split('.')[0]);
            return null;                                                                                    // latest stable
        }

        string? Latest(string package, int? major)
        {
            if (major == -1) return null;
            if (_offline) return null;
            var key = package + "|" + major;
            if (_cache.TryGetValue(key, out var cached)) return cached;
            try
            {
                var index = Http.GetFromJsonAsync<VersionIndex>($"https://api.nuget.org/v3-flatcontainer/{package.ToLowerInvariant()}/index.json").Result;
                var stable = (index?.versions ?? new List<string>()).Where(v => !v.Contains('-')).ToList();
                if (major != null) stable = stable.Where(v => v.StartsWith(major + ".", StringComparison.Ordinal)).ToList();
                var latest = stable.LastOrDefault();
                _cache[key] = latest;
                return latest;
            }
            catch
            {
                _offline = true;
                return null;
            }
        }

        class VersionIndex { public List<string>? versions { get; set; } }
    }
}
