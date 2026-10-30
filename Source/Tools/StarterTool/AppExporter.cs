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
    ///     ClaudeCodeForDeveloper/claude-code-setup.md  claude-code-update.md
    ///     ClaudeCodeForDeveloper/starter-origin.json   (the exported Starter commit; the base of an update)
    ///     .vscode/  .gitignore  LICENSE
    ///     Source/LowCodeApp.sln
    ///     Source/LowCodeApp.Server  LowCodeApp.Client  LowCodeApp.Client.Shared  LowCodeApp.Designer  LowCodeApp.LicenseRegisterCli
    ///     (--maui adds LowCodeApp.Maui)
    ///
    /// The projects are the Cookie variant's template projects (VariantProject.InVsix) with project references flattened
    /// to side-by-side folders, exactly like the VSIX template. Paths in the documents are rewritten from the repository
    /// layout (Source/Hosts/Cookie/X, Source/Hosts/Common/X) to the application layout (Source/X).
    /// Text between "&lt;!-- maintainer-only --&gt;" and "&lt;!-- /maintainer-only --&gt;" is dropped.
    /// Target frameworks and package versions are exported as they are in the repository.
    /// </summary>
    public class AppExporter
    {
        readonly string _root;
        public const string DefaultVariant = "Cookie";

        public AppExporter(string root)
        {
            _root = root;
        }

        /// <param name="starterCommit">The Starter commit being exported (the setup downloads the zip of that commit). Recorded in
        /// ClaudeCodeForDeveloper/starter-origin.json so that an update can export the same commit again as the base of a 3-way comparison.</param>
        public void Run(string dest, bool withMaui, string? starterCommit)
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

            var entries = new List<(string csproj, string folder, bool deploy)>();
            foreach (var (project, variantName) in projects)
            {
                var src = project.MasterDir(_root, variantName);
                var dst = Path.Combine(sourceDir, project.ProjectName);
                Repository.CopyProject(src, dst, (relative, text) =>
                {
                    if (!relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return text;
                    return Repository.FlattenProjectReferences(text);
                }, renameSample: true);
                entries.Add((Path.Combine(dst, project.ProjectName + ".csproj"), project.SolutionFolder, project.Deploy));
                Console.WriteLine($"exported {project.ProjectName}");
            }
            Assembler.CreateSolution(sourceDir, Repository.AppName, entries);
            Console.WriteLine($"created Source/{Repository.AppName}.sln");

            //Documents and settings that belong to the application folder (not to this repository).
            foreach (var relative in new[] { "CLAUDE.md", Path.Combine("ClaudeCodeForDeveloper", "claude-code-setup.md"), Path.Combine("ClaudeCodeForDeveloper", "claude-code-update.md"), ".gitignore", "LICENSE" })
                CopyText(relative, dest);
            foreach (var file in Directory.EnumerateFiles(Path.Combine(_root, ".vscode"), "*.json"))
                CopyText(Path.Combine(".vscode", Path.GetFileName(file)), dest);
            var origin = new { starterCommit, maui = withMaui };
            File.WriteAllText(Path.Combine(dest, UpdateDiff.OriginFile),
                System.Text.Json.JsonSerializer.Serialize(origin, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
            Console.WriteLine("exported CLAUDE.md, ClaudeCodeForDeveloper/, .vscode/, .gitignore, LICENSE");

            //Fonts for PDF output (FontFileDirectory): Noto Sans JP (SIL Open Font License 1.1; OFL.txt goes with them).
            var fontSrc = Path.Combine(Repository.Hosts(_root), Repository.CommonDir, "Font");
            if (Directory.Exists(fontSrc))
            {
                var fontDst = Path.Combine(dest, "Local", "Font");
                Directory.CreateDirectory(fontDst);
                foreach (var file in Directory.EnumerateFiles(fontSrc))
                    File.Copy(file, Path.Combine(fontDst, Path.GetFileName(file)), true);
                Console.WriteLine("exported Local/Font (" + string.Join(", ", Directory.EnumerateFiles(fontSrc).Select(Path.GetFileName)) + ")");
            }
        }

        void CopyText(string relative, string dest)
        {
            var src = Path.Combine(_root, relative);
            if (!File.Exists(src)) return;
            var target = Path.Combine(dest, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, ToAppLayout(File.ReadAllText(src)), new UTF8Encoding(false));
        }

        /// <summary>Repository layout → application layout, maintainer-only blocks removed.</summary>
        public static string ToAppLayout(string text)
        {
            text = Regex.Replace(text, @"<!--\s*maintainer-only\s*-->.*?<!--\s*/maintainer-only\s*-->\r?\n?", "", RegexOptions.Singleline);
            //Source/Hosts/Cookie/X, Source/Hosts/Common/X, Source/Hosts/<VARIANT>/X, Source/Hosts/Maui/X -> Source/X (both slash styles, escaped or not)
            text = Regex.Replace(text, @"Source((?:\\\\|\\|/))Hosts\1(?:Cookie|Common|Maui|<VARIANT>|<Variant>)\1", "Source$1");
            return text;
        }
    }
}
