using StarterTool;

// Maintenance tool of the Starter repository. Run from anywhere inside the repository:
//   dotnet run --project Source/Tools/StarterTool -- assemble
//   dotnet run --project Source/Tools/StarterTool -- pack-vsix
//   dotnet run --project Source/Tools/StarterTool -- export-debug <path to Codeer.LowCode.Blazor/Source>
//   dotnet run --project Source/Tools/StarterTool -- export-app <destination folder> [--maui] [--no-upgrade]   (customer-shaped application folder = Cookie host + Claude Code documents; moved to net10.0 + latest packages unless --no-upgrade)
//
// Every project exists once:
//   Source/Hosts/Common/*                        projects shared by every variant
//   Source/Hosts/<Variant>/LowCodeApp.<Own>       projects that belong to one variant (Normal/Cookie Server+Client, Maui, Wpf, WinForms)
// Generated (never edited by hand):
//   Source/Hosts/<Variant>/LowCodeApp.sln         references the projects in place (Common/, and Cookie's server/client for Maui)
//   Source/Codeer.LowCode.Blazor.Starter.sln      maintainer solution with everything
//   Source/Tools/Codeer.LowCode.Blazor.Templates/ProjectTemplates/*.zip
//   Codeer.LowCode.Blazor/Source/App/{Normal,Cookie,Common} (debug copies with project references)

var root = Repository.FindRoot();
var command = args.Length > 0 ? args[0] : "";
switch (command)
{
    case "assemble":
        new Assembler(root).Run();
        break;
    case "pack-vsix":
        new Assembler(root).Run();
        new VsixPacker(root).Run();
        break;
    case "export-debug":
        if (args.Length < 2) return Usage();
        new DebugExporter(root).Run(Path.GetFullPath(args[1]));
        break;
    case "export-app":
        if (args.Length < 2) return Usage();
        new AppExporter(root).Run(Path.GetFullPath(args[1]), args.Contains("--maui"), upgrade: !args.Contains("--no-upgrade"));
        break;
    default:
        return Usage();
}
return 0;

static int Usage()
{
    Console.Error.WriteLine("usage: StarterTool assemble | pack-vsix | export-debug <Codeer.LowCode.Blazor/Source> | export-app <dest> [--maui] [--no-upgrade]");
    return 1;
}
