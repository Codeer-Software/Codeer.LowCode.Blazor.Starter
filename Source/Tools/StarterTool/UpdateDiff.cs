using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace StarterTool
{
    /// <summary>
    /// Compares an application folder written by export-app (and changed by its owner since) with two fresh exports:
    /// the Starter commit it was exported from (base) and the latest one (new). Used by ClaudeCodeForDeveloper/claude-code-update.md.
    /// Only the files export-app writes are compared; files the owner added are not listed.
    /// Local/ (fonts) is not compared. Text files are compared ignoring line endings and BOM (Visual Studio rewrites them).
    /// Without a base, files the template removed are not listed (nothing tells that they came from the template).
    ///
    ///   kind            base / app / new
    ///   update          app == base, new differs          the template changed, the owner did not touch the file
    ///   merge           all three differ                  both changed
    ///   add             only in new                       a file the template added
    ///   addConflict     not in base, app and new differ   the owner made a file of the same name
    ///   delete          app == base, not in new           the template removed it
    ///   deleteModified  app != base, not in new           the template removed a file the owner changed
    ///   userDeleted     base != new, not in app           the owner removed a file the template changed
    ///   differs         (no base) app != new              without a base nothing tells who changed what
    /// </summary>
    public class UpdateDiff
    {
        /// <summary>Written by export-app; the comparison skips it (it is rewritten at the end of the update).</summary>
        public static readonly string OriginFile = Path.Combine("ClaudeCodeForDeveloper", "starter-origin.json");

        public static int Run(string app, string? baseDir, string newDir, string outPath)
        {
            var paths = Enumerate(newDir).Concat(baseDir == null ? [] : Enumerate(baseDir))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(p => !p.Equals(OriginFile, StringComparison.OrdinalIgnoreCase) && !p.StartsWith("Local" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var entries = new List<object>();
            foreach (var path in paths)
            {
                var b = baseDir == null ? null : Read(baseDir, path);
                var a = Read(app, path);
                var n = Read(newDir, path);
                var kind = baseDir == null ? TwoWay(a, n) : ThreeWay(b, a, n);
                if (kind != null) entries.Add(new { path = path.Replace('\\', '/'), kind, area = Area(path) });
            }

            var result = new { app, @base = baseDir, @new = newDir, entries };
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            File.WriteAllText(outPath, json, new UTF8Encoding(false));
            Console.WriteLine($"{entries.Count} file(s) to look at → {outPath}");
            return 0;
        }

        static string? ThreeWay(byte[]? b, byte[]? a, byte[]? n)
        {
            if (Same(a, n)) return null;
            if (b == null) return n == null ? null : a == null ? "add" : "addConflict";
            if (n == null) return a == null ? null : Same(a, b) ? "delete" : "deleteModified";
            if (Same(b, n)) return null;
            if (a == null) return "userDeleted";
            return Same(a, b) ? "update" : "merge";
        }

        static string? TwoWay(byte[]? a, byte[]? n)
        {
            if (Same(a, n) || n == null) return null;
            return a == null ? "add" : "differs";
        }

        /// <summary>package = csproj (package versions, target framework) / settings = appsettings, launchSettings /
        /// docs = everything outside Source/ (CLAUDE.md, ClaudeCodeForDeveloper, .vscode, .gitignore, LICENSE) / code = the rest.</summary>
        static string Area(string path)
        {
            var name = Path.GetFileName(path);
            if (name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return "package";
            if (name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) || name.Equals("launchSettings.json", StringComparison.OrdinalIgnoreCase)) return "settings";
            if (!path.StartsWith(Repository.SourceDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return "docs";
            return "code";
        }

        static IEnumerable<string> Enumerate(string dir)
            => Repository.EnumerateProjectFiles(dir).Select(f => Path.GetRelativePath(dir, f));

        static byte[]? Read(string dir, string path)
        {
            var file = Path.Combine(dir, path);
            if (!File.Exists(file)) return null;
            var bytes = File.ReadAllBytes(file);
            if (Array.IndexOf(bytes, (byte)0) >= 0) return bytes;   //binary
            //Normalized as bytes, not decoded: a file in another encoding (CP932) must not collapse into replacement characters.
            var start = bytes.AsSpan().StartsWith(Utf8Bom) ? Utf8Bom.Length : 0;
            var normalized = new List<byte>(bytes.Length);
            for (var i = start; i < bytes.Length; i++)
            {
                if (bytes[i] == '\r' && i + 1 < bytes.Length && bytes[i + 1] == '\n') continue;
                normalized.Add(bytes[i]);
            }
            return normalized.ToArray();
        }

        static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        static bool Same(byte[]? x, byte[]? y)
            => x == null ? y == null : y != null && x.AsSpan().SequenceEqual(y);
    }
}
