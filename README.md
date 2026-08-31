# Codeer.LowCode.Blazor.Starter

Ready-to-build starter solutions for [Codeer.LowCode.Blazor](https://www.nuget.org/packages/Codeer.LowCode.Blazor),
one folder per application variant under `Source/Hosts/`. Each folder is a complete solution named `LowCodeApp` with the framework
referenced as NuGet packages.

## Fastest way: let Claude Code set everything up

Open an empty folder in [Claude Code](https://claude.com/claude-code) and say:

> このURLを見て指示に従って https://github.com/Codeer-Software/Codeer.LowCode.Blazor.Starter

Claude Code reads [docs/claude-code-setup.md](docs/claude-code-setup.md) and does the rest: checks the .NET SDK (installs it with
winget if you agree), downloads this repository, builds the solution, creates a design project from a template with sample data,
sets up its own workspace for editing the design, and starts the server (opens in your browser) and the designer.
Windows only (the designer is a WPF application). Visual Studio is not required; VS Code launch settings are included.
No license registration is needed to try it — the designer runs as a trial.

After that, in the same folder, you can ask Claude Code to add or change screens ("商品マスタの画面を追加して").

> **Disclaimer**: the setup procedure and the workspace documents in this repository are instructions executed by an AI
> (Claude Code). The AI installs software, edits files and runs commands on your machine, and its behavior is not
> deterministic — review what it proposes and use it at your own risk. Everything here is provided AS IS, without
> warranty of any kind (see LICENSE); Codeer is not liable for any damage caused by AI operations.

## What is in this repository

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

You can also:

- start a new application from a variant in `Source/Hosts/` (clone the repository, or install the Visual Studio extension),
- `git diff` two tags (for example `git diff v1.3.20 v1.3.23 -- Source/Hosts/Cookie`) to see what changed between framework versions and apply it to your own application,
- use the repository as the entry point for Claude Code (see `CLAUDE.md` and `docs/claude-code-setup.md`).

## Getting started by hand

1. Clone the repository (a variant folder alone is not enough: its solution references `Source/Hosts/Common/`). Alternatively install
   the Visual Studio extension, whose templates are self-contained.
2. Open `LowCodeApp.sln`. Requirements: .NET 8 SDK (all variants), .NET 10 SDK with the `maui-android` / `maui-ios`
   workloads for `Source/Hosts/Maui/`. Without Visual Studio: `dotnet build Source/Hosts/Cookie/LowCodeApp.sln`, and the
   `.vscode/` folder has launch settings for VS Code (C# Dev Kit).
3. Check `appsettings.Development.json` in the server project (`LowCodeApp.Server`, or `LowCodeApp.Wpf` / `LowCodeApp.WinForms`
   for the desktop variants): connection strings, `DesignFileDirectory`, file storage directory. The defaults point at
   `C:\Codeer.LowCode.Blazor.Local\...`; create those folders or change the paths. The reference of every setting is in `CLAUDE.md`.
4. Create the design project: run `LowCodeApp.Designer` and pick a template, or without the GUI
   `LowCodeApp.Designer.exe template-create --name PatternShowcaseAuth --out-dir <Design> --data-dir <Local\Data> --deploy-dir <DesignFileDirectory>`.
   Then run the server. For `Source/Hosts/Maui/`, run the `Cookie` server first and point the app at it.

Rename: the solution and projects are named `LowCodeApp`. Renaming is a plain find-and-replace of `LowCodeApp`
in file names, folder names and file contents (namespaces, `x:Class`, `*.styles.css` links).

## Versions

The repository is tagged with the framework version it was generated from (for example `v1.3.23`).
The `Codeer.LowCode.Blazor*` package versions referenced by each tag are the ones that version was tested with.

Maintainers: see [MAINTAINERS.md](MAINTAINERS.md) for how the solutions, the Visual Studio extension and the debug copies are generated.

## License

MIT for the files in this repository. Codeer.LowCode.Blazor itself is a commercial product with its own license.
