using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace StarterTool
{
    /// <summary>
    /// Turns each assembled variant into a Visual Studio multi-project template zip
    /// (Tools/Codeer.LowCode.Blazor.Templates/ProjectTemplates). The zip is the variant folder with the concrete name
    /// "LowCodeApp" replaced by template parameters: "LowCodeApp.Server" becomes $safeprojectname$ inside the Server
    /// project (VS expands it to the project's own name) and every other "LowCodeApp" becomes $ext_safeprojectname$
    /// (the solution name).
    /// </summary>
    public class VsixPacker
    {
        readonly string _root;

        public VsixPacker(string root)
        {
            _root = root;
        }

        public void Run()
        {
            var work = Path.Combine(Path.GetTempPath(), "Codeer.LowCode.Blazor.Starter.Vsix", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            var outDir = Path.Combine(Repository.Tools(_root), "Codeer.LowCode.Blazor.Templates", "ProjectTemplates");
            Directory.CreateDirectory(outDir);
            try
            {
                foreach (var variant in Variant.All.Where(v => v.IsTemplate))
                {
                    var templateDir = Path.Combine(work, variant.Name);
                    BuildTemplate(variant, templateDir);
                    var zipPath = Path.Combine(outDir, variant.ZipName);
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    ZipFile.CreateFromDirectory(templateDir, zipPath, CompressionLevel.Optimal, false);
                    Console.WriteLine($"packed {variant.ZipName}");
                }
            }
            finally
            {
                Repository.DeleteDirectory(work);
            }
        }

        void BuildTemplate(Variant variant, string templateDir)
        {
            Directory.CreateDirectory(templateDir);
            foreach (var project in variant.Projects.Where(p => p.InVsix))
            {
                var src = project.MasterDir(_root, variant.Name);
                var dst = Path.Combine(templateDir, project.Name);
                var files = new List<(string path, bool replaceParameters)>();
                Repository.CopyProject(src, dst, (relative, text) =>
                    Tokenize(relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? Repository.FlattenProjectReferences(text) : text, project.ProjectName));
                foreach (var file in Repository.EnumerateProjectFiles(src))
                {
                    var relative = Path.GetRelativePath(src, file);
                    if (Path.GetExtension(file).Equals(".csproj", StringComparison.OrdinalIgnoreCase)) continue;
                    files.Add((relative, Repository.IsTextFile(file)));
                }
                File.WriteAllText(Path.Combine(dst, "MyTemplate.vstemplate"), ProjectTemplate(project, files), new UTF8Encoding(false));
            }

            File.WriteAllText(Path.Combine(templateDir, variant.TemplateName + ".vstemplate"), SolutionTemplate(variant), new UTF8Encoding(false));
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Icon.ico"), Path.Combine(templateDir, "Icon.ico"), true);
        }

        static string Tokenize(string text, string projectName)
        {
            //Own name first ("LowCodeApp.Server" -> $safeprojectname$), then the solution name. Neither token contains
            //"LowCodeApp", so the second pass cannot touch the first. Matches are whole identifiers not preceded by "."
            //(so "Foo.LowCodeApp" is left alone) and are case-sensitive ("com.companyname.lowcodeapp" is left alone).
            text = Regex.Replace(text, $@"(?<![\w.]){Regex.Escape(projectName)}\b", "$$safeprojectname$$");
            text = Regex.Replace(text, $@"(?<![\w.]){Regex.Escape(Repository.AppName)}\b", "$$ext_safeprojectname$$");
            return text;
        }

        static string ProjectTemplate(VariantProject project, List<(string path, bool replaceParameters)> files)
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<VSTemplate Version=""3.0.0"" xmlns=""http://schemas.microsoft.com/developer/vstemplate/2005"" Type=""Project"">");
            sb.AppendLine(@"  <TemplateData>");
            sb.AppendLine($"    <Name>{project.Name}</Name>");
            sb.AppendLine(@"    <Description></Description>");
            sb.AppendLine(@"    <ProjectType>CSharp</ProjectType>");
            sb.AppendLine(@"    <ProjectSubType></ProjectSubType>");
            sb.AppendLine(@"    <SortOrder>1000</SortOrder>");
            sb.AppendLine(@"    <CreateNewFolder>true</CreateNewFolder>");
            sb.AppendLine($"    <DefaultName>{project.Name}</DefaultName>");
            sb.AppendLine(@"    <ProvideDefaultName>true</ProvideDefaultName>");
            sb.AppendLine(@"    <LocationField>Enabled</LocationField>");
            sb.AppendLine(@"    <EnableLocationBrowseButton>true</EnableLocationBrowseButton>");
            sb.AppendLine(@"    <CreateInPlace>true</CreateInPlace>");
            sb.AppendLine(@"    <Hidden>true</Hidden>");
            sb.AppendLine(@"  </TemplateData>");
            sb.AppendLine(@"  <TemplateContent>");
            var csproj = project.ProjectName + ".csproj";
            sb.AppendLine($@"    <Project TargetFileName=""{csproj}"" File=""{csproj}"" ReplaceParameters=""true"">");
            AppendFiles(sb, "      ", "", files);
            sb.AppendLine(@"    </Project>");
            sb.AppendLine(@"  </TemplateContent>");
            sb.AppendLine(@"</VSTemplate>");
            return sb.ToString();
        }

        static void AppendFiles(StringBuilder sb, string indent, string folder, List<(string path, bool replaceParameters)> files)
        {
            var here = files.Where(f => (Path.GetDirectoryName(f.path) ?? "") == folder).ToList();
            foreach (var (path, replaceParameters) in here)
            {
                var fileName = Path.GetFileName(path);
                sb.AppendLine($@"{indent}<ProjectItem ReplaceParameters=""{(replaceParameters ? "true" : "false")}"" TargetFileName=""{fileName}"">{fileName}</ProjectItem>");
            }
            var subFolders = files
                .Select(f => Path.GetDirectoryName(f.path) ?? "")
                .Where(d => d.Length > folder.Length && (folder == "" || d.StartsWith(folder + Path.DirectorySeparatorChar)))
                .Select(d => folder == "" ? d.Split(Path.DirectorySeparatorChar)[0] : d.Substring(folder.Length + 1).Split(Path.DirectorySeparatorChar)[0])
                .Distinct()
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase);
            foreach (var sub in subFolders)
            {
                sb.AppendLine($@"{indent}<Folder Name=""{sub}"">");
                AppendFiles(sb, indent + "  ", folder == "" ? sub : Path.Combine(folder, sub), files);
                sb.AppendLine($@"{indent}</Folder>");
            }
        }

        static string SolutionTemplate(Variant variant)
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<VSTemplate Version=""3.0.0"" xmlns=""http://schemas.microsoft.com/developer/vstemplate/2005"" Type=""ProjectGroup"">");
            sb.AppendLine(@"  <TemplateData>");
            sb.AppendLine($"    <Name>{variant.TemplateName}</Name>");
            sb.AppendLine($"    <Description>{variant.Description}</Description>");
            sb.AppendLine(@"    <ProjectType>CSharp</ProjectType>");
            sb.AppendLine(@"    <LanguageTag>csharp</LanguageTag>");
            foreach (var tag in variant.PlatformTags) sb.AppendLine($"    <PlatformTag>{tag}</PlatformTag>");
            sb.AppendLine(@"    <ProjectTypeTag>Blazor</ProjectTypeTag>");
            sb.AppendLine(@"    <ProjectTypeTag>LowCode</ProjectTypeTag>");
            foreach (var tag in variant.ProjectTypeTags) sb.AppendLine($"    <ProjectTypeTag>{tag}</ProjectTypeTag>");
            sb.AppendLine(@"    <Icon>Icon.ico</Icon>");
            sb.AppendLine($"    <DefaultName>{Repository.AppName}</DefaultName>");
            sb.AppendLine(@"    <ProvideDefaultName>true</ProvideDefaultName>");
            sb.AppendLine(@"  </TemplateData>");
            sb.AppendLine(@"  <TemplateContent PreferredSolutionConfiguration=""Debug|Any CPU"">");
            sb.AppendLine(@"    <ProjectCollection>");
            foreach (var group in variant.Projects.Where(p => p.InVsix).GroupBy(p => p.SolutionFolder))
            {
                sb.AppendLine($@"      <SolutionFolder Name=""{group.Key}"">");
                foreach (var project in group)
                {
                    sb.AppendLine($@"        <ProjectTemplateLink ProjectName=""$safeprojectname$.{project.Name}"" CopyParameters=""true"">");
                    sb.AppendLine($@"          {project.Name}\MyTemplate.vstemplate");
                    sb.AppendLine(@"        </ProjectTemplateLink>");
                }
                sb.AppendLine(@"      </SolutionFolder>");
            }
            sb.AppendLine(@"    </ProjectCollection>");
            sb.AppendLine(@"  </TemplateContent>");
            sb.AppendLine(@"</VSTemplate>");
            return sb.ToString();
        }
    }
}
