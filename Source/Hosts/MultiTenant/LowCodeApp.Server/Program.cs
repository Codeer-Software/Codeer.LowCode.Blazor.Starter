using Codeer.LowCode.Bindings.ApexCharts;
using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras;
using Codeer.LowCode.Blazor.Json;
using Codeer.LowCode.Blazor.License;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Fonts;
using System.Text;
using System.Text.Json.Serialization;
using LowCodeApp.Client.Shared.Samples;
using LowCodeApp.Server.Data;
using LowCodeApp.Server.Infrastructure;
using Codeer.LowCode.Blazor.Extras.Server.Excel;
using Codeer.LowCode.Blazor.Extras.Server.Web;
using Microsoft.AspNetCore.SignalR;

//load dll.
typeof(CodeBehindSample).ToString();
ApexChartsServerInitializer.Initialize();
ExtrasServerInitializer.Initialize();

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

LicenseManager.DomainLicense = builder.Configuration.GetSection("DomainLicense").Get<string>() ?? string.Empty;
LicenseManager.IsAutoUpdate = builder.Configuration.GetSection("IsLicenseAutoUpdate").Get<bool>();
//appsettings → SystemConfig (セクション名 = プロパティ名。設定を足すときは SystemConfig にプロパティを足す。使うものだけ書けばよい)
builder.Configuration.Bind(SystemConfig.Instance);
//接続文字列は ConnectionStrings:<Name> に置く (Azure Blob とファイル保存先の簡易形式は設定に直接書いてもよい)
SystemConfig.Instance.DataSources.ToList().ForEach(e => e.ConnectionString = builder.Configuration.GetConnectionString(e.Name) ?? string.Empty);
foreach (var storage in SystemConfig.Instance.AzureBlobStorages) if (string.IsNullOrEmpty(storage.ConnectionString) && string.IsNullOrEmpty(storage.BlobServiceUri)) storage.ConnectionString = builder.Configuration.GetConnectionString(storage.Name) ?? string.Empty;
foreach (var storage in SystemConfig.Instance.FileStorages) if (string.IsNullOrEmpty(storage.ConnectionString)) storage.ConnectionString = builder.Configuration.GetConnectionString(storage.Name) ?? string.Empty;

GlobalFontSettings.FontResolver = new CustomFontResolver(SystemConfig.Instance.FontFileDirectory);
SystemConfig.Instance.DefaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Cookie authentication.
builder.UseCookieAuthentication();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddControllers()
      .AddJsonOptions(options =>
      {
          options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
          options.JsonSerializerOptions.Converters.AddJsonConverters();
      });

if (SystemConfig.Instance.UseHotReload)
{
    builder.Services.AddSignalR();
    builder.Services.AddHostedService(sp => new FileWatcherService(sp.GetRequiredService<IHubContext<HotReloadHub>>(), SystemConfig.Instance.DesignFileDirectory));
}

// Compress dynamic responses (e.g. the design data fetched on every reload).
// Brotli/Gzip are decoded by the browser's native network stack, so this adds
// no decompression cost to the WASM runtime.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes
        .Concat(["application/octet-stream"]);
});

//画面の言語 (ブラウザの Accept-Language で日本語 / 英語)
builder.Services.AddLowCodeLocalization();

builder.Services.AddScoped<DataService>();

var app = builder.Build();

//SQL debug log: dump executed SQL and parameters (enable via appsettings.Development.json or the SqlLog app setting).
//ILogger output reaches the local console, Azure App Service Log Stream and Application Insights
//(raw Console.WriteLine is discarded on Windows App Service).
if (builder.Configuration.GetSection("SqlLog").Get<bool>())
{
    var sqlLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SqlLog");
    DbAccessor.SqlLog = message => sqlLogger.LogInformation("{SqlLog}", message);
}

app.UseResponseCompression();
app.UseRequestLocalization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();

// Always revalidate .html entry pages so a deployed update takes effect
// immediately instead of being served stale from the browser cache.
var htmlNoCache = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache";
        }
    }
};
app.UseStaticFiles(htmlNoCache);

// API responses are never served from the browser cache unless the action set its own
// Cache-Control (FileWithETag uses no-cache + ETag so downloads can still be revalidated).
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.OnStarting(() =>
        {
            if (string.IsNullOrEmpty(context.Response.Headers.CacheControl))
            {
                context.Response.Headers.CacheControl = "no-store";
            }
            return Task.CompletedTask;
        });
    }
    await next();
});

app.UseRouting();

if (SystemConfig.Instance.UseHotReload)
{
    app.MapHub<HotReloadHub>("/hot_reload_hub");
}

// Cookie authentication.
app.UseCookieAuthentication();

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html", htmlNoCache);

// Exception handling.
app.UseExceptionHandlerSendToFront();

app.Run();
