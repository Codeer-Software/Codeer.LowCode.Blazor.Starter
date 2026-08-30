# Codeer.LowCode.Blazor.Starter

Ready-to-build starter solutions for [Codeer.LowCode.Blazor](https://www.nuget.org/packages/Codeer.LowCode.Blazor),
one folder per application variant under `Source/Hosts/`. Each folder is a complete solution named `LowCodeApp` with the framework
referenced as NuGet packages, so you can:

- start a new application from a variant in `Source/Hosts/` (clone the repository, or install the Visual Studio template),
- `git diff` two tags (for example `git diff v1.3.20 v1.3.23 -- Source/Hosts/Cookie`) to see what changed between framework versions and apply it to your own application,
- use the repository as the entry point for Claude Code (see `CLAUDE.md`).

| Folder | What it is |
|---|---|
| `Source/Hosts/Normal/` | Blazor WebAssembly client + ASP.NET Core server, no authentication |
| `Source/Hosts/Cookie/` | Blazor WebAssembly client + ASP.NET Core server with Cookie (ASP.NET Core Identity style) authentication |
| `Source/Hosts/Maui/` | `Cookie` plus a .NET MAUI (Android / iOS) client that talks to the same server |
| `Source/Hosts/Wpf/` | Standalone desktop application (WPF + BlazorWebView, server code in-process) |
| `Source/Hosts/WinForms/` | Standalone desktop application (WinForms + BlazorWebView, server code in-process) |
| `Source/Hosts/MultiTenant/` | Multi-tenant web host (ASP.NET Core Identity, per-tenant design and data). Buildable, but not shipped as a Visual Studio template yet |

Every variant's solution also includes the projects in `Source/Hosts/Common/`: `LowCodeApp.Designer` (the visual designer, run it to
edit the design files), `LowCodeApp.Client.Shared` (client services shared by the browser, desktop and mobile clients) and
a license tool. `Source/Hosts/Maui/` additionally includes the server and browser client of `Source/Hosts/Cookie/`. Each project exists
once in the repository; the solutions reference them in place.

## Getting started

1. Clone the repository (a variant folder alone is not enough: its solution references `Source/Hosts/Common/`). Alternatively install
   the Visual Studio extension, whose templates are self-contained.
2. Open `LowCodeApp.sln`. Requirements: .NET 8 SDK (all variants), .NET 10 SDK with the `maui-android` / `maui-ios`
   workloads for `Source/Hosts/Maui/`.
3. Check `appsettings.Development.json` in the server project (`LowCodeApp.Server`, or `LowCodeApp.Wpf` / `LowCodeApp.WinForms`
   for the desktop variants): connection strings, `DesignFileDirectory`, file storage directory. The defaults point at
   `C:\Codeer.LowCode.Blazor.Local\...`; create those folders or change the paths.
4. Run `LowCodeApp.Designer` to create the design project, then run the server (and the client for `Source/Hosts/Maui/`).

Rename: the solution and projects are named `LowCodeApp`. Renaming is a plain find-and-replace of `LowCodeApp`
in file names, folder names and file contents (namespaces, `x:Class`, `*.styles.css` links).

## Versions

The repository is tagged with the framework version it was generated from (for example `v1.3.23`).
The `Codeer.LowCode.Blazor*` package versions referenced by each tag are the ones that version was tested with.

## Repository layout (for maintainers)

This repository is the source of truth for the application templates.

| Path | Role |
|---|---|
| `Source/` | Everything that is built; the repository root holds documentation. `Source/Codeer.LowCode.Blazor.Starter.sln` opens every project of every variant (maintainers; needs the MAUI workloads) |
| `Source/Hosts/` | The host applications: the C# solutions that run a low-code design project. (The design project itself — screens, data, scripts — is created with the designer and is not part of this repository.) |
| `Source/Hosts/Common/` | Masters of the projects every variant shares (`Client.Shared`, `Designer`, `LicenseRegister`, `LicenseRegisterCli`) |
| `Source/Hosts/<Variant>/LowCodeApp.<Own>` | Masters of the projects that belong to one variant (`Normal`/`Cookie` server and client, `Maui`, `Wpf`, `WinForms`) |
| `Source/Hosts/<Variant>/LowCodeApp.sln` | **Generated**; references the variant's projects, `Source/Hosts/Common/` and (for `Maui/`) the `Cookie` server/client in place |
| `Source/Tools/StarterTool` | `assemble` (regenerate the solutions), `pack-vsix` (Visual Studio template zips), `export-debug` (debug copies for the framework repository) |
| `Source/Tools/Codeer.LowCode.Blazor.Templates` | The Visual Studio extension (VSIX) that ships the variants as project templates |

```
dotnet run --project Source/Tools/StarterTool -- assemble
dotnet run --project Source/Tools/StarterTool -- pack-vsix
dotnet run --project Source/Tools/StarterTool -- export-debug <Codeer.LowCode.Blazor/Source>
```

Run `assemble` after adding or removing a project. Pull requests are welcome against the masters.

## License

MIT for the files in this repository. Codeer.LowCode.Blazor itself is a commercial product with its own license.
