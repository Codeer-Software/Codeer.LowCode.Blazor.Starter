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
| `Source/Hosts/Maui/` | .NET MAUI (Android / iOS) client only; a thin client of a `Cookie` server (create the server, designer and tools from `Cookie`) |
| `Source/Hosts/Wpf/` | Standalone desktop application (WPF + BlazorWebView, server code in-process) |
| `Source/Hosts/WinForms/` | Standalone desktop application (WinForms + BlazorWebView, server code in-process) |
| `Source/Hosts/MultiTenant/` | Multi-tenant web host (ASP.NET Core Identity, per-tenant design and data). Buildable, but not shipped as a Visual Studio template yet |

Every variant's solution also includes the projects in `Source/Hosts/Common/`: `LowCodeApp.Designer` (the visual designer, run it to
edit the design files), `LowCodeApp.Client.Shared` (client services shared by the browser, desktop and mobile clients) and
a license tool. `Source/Hosts/Maui/` is the exception: it holds only the mobile app and `Client.Shared`, and expects a running
`Cookie` server (its URL is entered in the app's settings page). Each project exists once in the repository; the solutions reference them in place.

## Getting started

1. Clone the repository (a variant folder alone is not enough: its solution references `Source/Hosts/Common/`). Alternatively install
   the Visual Studio extension, whose templates are self-contained.
2. Open `LowCodeApp.sln`. Requirements: .NET 8 SDK (all variants), .NET 10 SDK with the `maui-android` / `maui-ios`
   workloads for `Source/Hosts/Maui/`.
3. Check `appsettings.Development.json` in the server project (`LowCodeApp.Server`, or `LowCodeApp.Wpf` / `LowCodeApp.WinForms`
   for the desktop variants): connection strings, `DesignFileDirectory`, file storage directory. The defaults point at
   `C:\Codeer.LowCode.Blazor.Local\...`; create those folders or change the paths.
4. Run `LowCodeApp.Designer` to create the design project, then run the server. For `Source/Hosts/Maui/`, run the `Cookie`
   server first and point the app at it.

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
| `Source/Hosts/<Variant>/LowCodeApp.sln` | **Generated**; references the variant's projects and `Source/Hosts/Common/` in place |
| `Source/Tools/StarterTool` | `assemble` (regenerate the solutions), `pack-vsix` (Visual Studio template zips), `export-debug` (debug copies for the framework repository) |
| `Source/Tools/Codeer.LowCode.Blazor.Templates` | The Visual Studio extension (VSIX) that ships the variants as project templates |

```
dotnet run --project Source/Tools/StarterTool -- assemble
dotnet run --project Source/Tools/StarterTool -- pack-vsix
dotnet run --project Source/Tools/StarterTool -- export-debug <Codeer.LowCode.Blazor/Source>
```

Run `assemble` after adding or removing a project. Pull requests are welcome against the masters.

`pack-vsix` only refreshes the template zips. The extension itself is built with MSBuild (Visual Studio required):

```
msbuild Source/Tools/Codeer.LowCode.Blazor.Templates/Codeer.LowCode.Blazor.Templates.csproj -t:BuildSplitVsix -p:Configuration=Release
```

This produces one VSIX per Visual Studio major version — `Codeer.LowCode.Blazor.Templates.VS2022.vsix` and
`Codeer.LowCode.Blazor.Templates.VS2026.vsix` in `bin/Release` — in addition to the combined
`Codeer.LowCode.Blazor.Templates.vsix`. The split builds exist as a workaround for older VS Installers
(VSIXInstaller 18.3 and earlier): on a machine with both VS 2022 and VS 2026, a single VSIX targeting both made the
second install fail with "the stream must be seekable" until run a second time. VSIXInstaller 18.9 (VS 2026 18.9+)
installs the combined VSIX to both instances in one run, so shipping the single file is fine for up-to-date machines.

## License

MIT for the files in this repository. Codeer.LowCode.Blazor itself is a commercial product with its own license.
