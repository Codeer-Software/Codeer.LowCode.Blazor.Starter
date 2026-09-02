namespace StarterTool
{
    public enum ProjectSource
    {
        /// <summary>Master lives in the variant folder itself.</summary>
        Own,
        /// <summary>Master lives in Hosts/Common (used by every variant).</summary>
        Common,
        /// <summary>Master lives in another variant (Maui reuses the Cookie server and client).</summary>
        Borrowed
    }

    /// <summary>One project of a variant. Name is the suffix after "LowCodeApp." (project "LowCodeApp.Server" has Name "Server").
    /// InVsix = false keeps the project in the repository solutions but out of the Visual Studio template
    /// (the Selenium test project tests this repository's hosts; customers test their design projects with the designer's selenium-test-init).</summary>
    public record VariantProject(string Name, ProjectSource Source, string SolutionFolder, string? BorrowedFrom = null, bool Deploy = false, bool InVsix = true)
    {
        public string ProjectName => $"{Repository.AppName}.{Name}";

        public string MasterDir(string root, string variant) => Source switch
        {
            ProjectSource.Own => Path.Combine(Repository.Hosts(root), variant, ProjectName),
            ProjectSource.Common => Path.Combine(Repository.Hosts(root), Repository.CommonDir, ProjectName),
            ProjectSource.Borrowed => Path.Combine(Repository.Hosts(root), BorrowedFrom!, ProjectName),
            _ => throw new InvalidOperationException()
        };
    }

    /// <summary>One application variant and how the VS template describes it.</summary>
    /// <param name="IsTemplate">false = kept in the repository (solution generated, debug copy exported) but not shipped as a VS template.
    /// Public templates are Cookie (the default, no suffix) and Maui only; Normal / Wpf / WinForms / MultiTenant stay as hosts for maintenance.</param>
    public record Variant(string Name, string TemplateName, string ZipName, string Description, string[] PlatformTags, string[] ProjectTypeTags, VariantProject[] Projects, bool IsTemplate = true)
    {
        static VariantProject Own(string name, string folder, bool deploy = false) => new(name, ProjectSource.Own, folder, Deploy: deploy);
        static VariantProject Common(string name, string folder, bool inVsix = true) => new(name, ProjectSource.Common, folder, InVsix: inVsix);
        static VariantProject From(string variant, string name, string folder) => new(name, ProjectSource.Borrowed, folder, variant);

        static Variant Web(string name, string templateName, string zipName, string authType, bool isTemplate = true) => new(
            name, templateName, zipName, $"Create Codeer.LowCode.Blazor with {authType} authorization.",
            Array.Empty<string>(), new[] { "web" },
            new[]
            {
                Own("Server", "WebApp"),
                Own("Client", "WebApp"),
                Common("LicenseRegisterCli", "Tools"),
                Common("Designer", "Tools"),
                Common("Client.Shared", "WebApp"),
                //Selenium (NUnit) tests of the running application. Same content as the designer's `selenium-test-init` template.
                //Repository only: not shipped in the VS template (customers test their own design project, not this host).
                Common("SeleniumTest", "Tests", inVsix: false),
            },
            IsTemplate: isTemplate);

        static Variant Desktop(string name) => new(
            name, $"Codeer.LowCode.Blazor.{name}", $"Codeer.LowCode.Blazor.Template.{name}.zip", $"Create Codeer.LowCode.Blazor on {name}",
            new[] { "windows" }, new[] { "desktop" },
            new[]
            {
                Own(name, "DesktopApp"),
                Common("LicenseRegister", "Tools"),
                Common("Designer", "Tools"),
                Common("Client.Shared", "DesktopApp"),
            },
            IsTemplate: false);

        public static readonly Variant[] All =
        {
            //No authentication. Kept as a host (debug copy for the framework repository's Selenium tests), not shipped as a template:
            //an application without a login is made by removing the authentication from Cookie (see CLAUDE.md), not by starting here.
            Web("Normal", "Codeer.LowCode.Blazor.Normal", "Codeer.LowCode.Blazor.Template.Normal.zip", "No", isTemplate: false),
            //The default template: no suffix in the template name / zip.
            Web("Cookie", "Codeer.LowCode.Blazor", "Codeer.LowCode.Blazor.Template.zip", "Cookie"),
            //.NET MAUI (Android/iOS) client only. It is a thin client of an existing Cookie-variant server, so the app content
            //follows the server's design files without a store release. The server, designer and license tools come from the
            //Cookie template; this one adds just the mobile app (and Client.Shared, which it references).
            new("Maui", "Codeer.LowCode.Blazor.Maui", "Codeer.LowCode.Blazor.Template.Maui.zip",
                "Create a .NET MAUI (Android/iOS) client for a Codeer.LowCode.Blazor server with Cookie authorization.",
                new[] { "android", "ios" }, new[] { "mobile" },
                new[]
                {
                    Own("Maui", "MobileApp", deploy: true),
                    Common("Client.Shared", "MobileApp"),
                }),
            Desktop("Wpf"),
            Desktop("WinForms"),
            //Multi-tenant (ASP.NET Core Identity + per-tenant design/data). Kept here as a host, not templated yet.
            new("MultiTenant", "Codeer.LowCode.Blazor.MultiTenant", "Codeer.LowCode.Blazor.Template.MultiTenant.zip",
                "Create Codeer.LowCode.Blazor with multi-tenant authorization.",
                Array.Empty<string>(), new[] { "web" },
                new[]
                {
                    Own("Server", "WebApp"),
                    Own("Client", "WebApp"),
                    Common("Designer", "Tools"),
                    Common("Client.Shared", "WebApp"),
                    Common("SeleniumTest", "Tests"),
                },
                IsTemplate: false),
        };

        /// <summary>Variants whose masters are copied into the main repository as debug copies.</summary>
        public static readonly string[] DebugVariants = { "Normal", "Cookie" };

        /// <summary>Common projects that are not needed for debugging the framework (license registration front ends).</summary>
        public static readonly string[] NotForDebug = { "LicenseRegister", "LicenseRegisterCli", "SeleniumTest" };
    }
}
