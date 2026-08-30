namespace StarterTool
{
    public static class Repository
    {
        public const string AppName = "LowCodeApp";
        /// <summary>Everything that is built lives under Source/; the repository root holds documentation.</summary>
        public const string SourceDir = "Source";
        /// <summary>Folder holding the host applications (one folder per variant) and Common/.</summary>
        public const string HostsDir = "Hosts";
        public const string CommonDir = "Common";
        public const string ToolsDir = "Tools";
        /// <summary>Maintainer solution with every project of every variant.</summary>
        public const string AllSolutionName = "Codeer.LowCode.Blazor.Starter";

        public static string Source(string root) => Path.Combine(root, SourceDir);
        public static string Hosts(string root) => Path.Combine(root, SourceDir, HostsDir);
        public static string Tools(string root) => Path.Combine(root, SourceDir, ToolsDir);

        /// <summary>
        /// In the repository the masters reference each other across folders ("..\..\Common\X\X.csproj",
        /// "..\..\Cookie\X\X.csproj"). In a generated solution (VS template, debug copy) all projects sit side by side,
        /// so the references become "..\X\X.csproj".
        /// </summary>
        public static string FlattenProjectReferences(string csproj)
            => System.Text.RegularExpressions.Regex.Replace(csproj, @"Include=""\.\.\\\.\.\\[^\\""]+\\", @"Include=""..\");

        /// <summary>Text files that carry names (namespaces, project references, css bundle links) to substitute.</summary>
        public static readonly string[] TextExtensions =
            { ".cs", ".razor", ".json", ".xaml", ".html", ".cshtml", ".csproj", ".md", ".css", ".xml", ".plist", ".manifest", ".config" };

        public static string FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, SourceDir, HostsDir, CommonDir)) && Directory.Exists(Path.Combine(dir.FullName, SourceDir, ToolsDir)))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("Starter repository root not found (expected Source/Hosts/Common and Source/Tools folders).");
        }

        public static bool IsBuildArtifactDir(string name)
            => name.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".vs", StringComparison.OrdinalIgnoreCase);

        public static bool IsExcludedFile(string name)
            => name.EndsWith(".user", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
            || name.Contains("_wpftmp", StringComparison.OrdinalIgnoreCase);

        /// <summary>Copies a project folder without build artifacts. Optional per-file text transform.</summary>
        public static void CopyProject(string src, string dst, Func<string, string, string>? transform = null)
        {
            foreach (var file in EnumerateProjectFiles(src))
            {
                var relative = Path.GetRelativePath(src, file);
                var target = Path.Combine(dst, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (transform != null && IsTextFile(file))
                {
                    var text = File.ReadAllText(file);
                    File.WriteAllText(target, transform(relative, text), new System.Text.UTF8Encoding(false));
                }
                else
                {
                    File.Copy(file, target, true);
                }
            }
        }

        public static IEnumerable<string> EnumerateProjectFiles(string projectDir)
        {
            foreach (var file in Directory.EnumerateFiles(projectDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(projectDir, file);
                var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (parts.Take(parts.Length - 1).Any(IsBuildArtifactDir)) continue;
                if (IsExcludedFile(parts[^1])) continue;
                yield return file;
            }
        }

        public static bool IsTextFile(string file)
            => TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);

        public static void DeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            for (var i = 0; ; i++)
            {
                try { Directory.Delete(path, true); return; }
                catch (IOException) when (i < 5) { Thread.Sleep(200); }
                catch (UnauthorizedAccessException) when (i < 5) { Thread.Sleep(200); }
            }
        }

        public static void RunDotnet(string workingDirectory, string arguments)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi)!;
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"dotnet {arguments}\n{output}");
        }
    }
}
