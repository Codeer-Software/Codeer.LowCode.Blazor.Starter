using Codeer.LowCode.Bindings.ApexCharts;
using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras;
using Codeer.LowCode.Blazor.Json;
using Codeer.LowCode.Blazor.License;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using PdfSharp.Fonts;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using LowCodeApp.Client.Shared.Samples;
using LowCodeApp.Server.Infrastructure;
using LowCodeApp.Server.Extras;
using LowCodeApp.Server.Jobs;
using Codeer.LowCode.Blazor.Extras.Server.Jobs;
using Microsoft.Extensions.Caching.Distributed;
using Codeer.LowCode.Blazor.Extras.Server.Excel;
using Codeer.LowCode.Blazor.Extras.Server.Web;
using Codeer.LowCode.Blazor.Extras.Server.AuditLog;
using Microsoft.AspNetCore.SignalR;

//load dll.
typeof(CodeBehindSample).ToString();
ApexChartsServerInitializer.Initialize();
ExtrasServerInitializer.Initialize();

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

//ジョブ (バッチ) モード: dotnet LowCodeApp.Server.dll job <ジョブ名> [引数...] (Jobs/README.md)。
//Web ホストは立てず、同じ appsettings でジョブを 1 つ実行して終了コードを返す。
//設定は exe の隣から読む (タスクスケジューラ等ではカレントフォルダが exe の場所とは限らない)。ジョブの引数は設定に混ぜない
var isJob = args is ["job", ..];
//ジョブモードの標準出力・標準エラーは UTF-8 (起動した側の JobProcess がそのつもりで読む。JobMode より前 = 設定の読み込みで失敗したときの出力も含める)
if (isJob) Console.OutputEncoding = Encoding.UTF8;
var builder = isJob
    ? WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory })
    : WebApplication.CreateBuilder(args);

LicenseManager.DomainLicense = builder.Configuration.GetSection("DomainLicense").Get<string>() ?? string.Empty;
LicenseManager.IsAutoUpdate = builder.Configuration.GetSection("IsLicenseAutoUpdate").Get<bool>();
//appsettings → SystemConfig (セクション名 = プロパティ名。設定を足すときは SystemConfig にプロパティを足す。使うものだけ書けばよい)
builder.Configuration.Bind(SystemConfig.Instance);
//接続文字列は ConnectionStrings:<Name> に置く (Azure Blob とファイル保存先の簡易形式は設定に直接書いてもよい)
SystemConfig.Instance.DataSources.ToList().ForEach(e => e.ConnectionString = builder.Configuration.GetConnectionString(e.Name) ?? string.Empty);
foreach (var storage in SystemConfig.Instance.AzureBlobStorages) if (string.IsNullOrEmpty(storage.ConnectionString) && string.IsNullOrEmpty(storage.BlobServiceUri)) storage.ConnectionString = builder.Configuration.GetConnectionString(storage.Name) ?? string.Empty;
foreach (var storage in SystemConfig.Instance.FileStorages) if (string.IsNullOrEmpty(storage.ConnectionString)) storage.ConnectionString = builder.Configuration.GetConnectionString(storage.Name) ?? string.Empty;

GlobalFontSettings.FontResolver = new CustomFontResolver(SystemConfig.Instance.FontFileDirectory);

if (isJob) return await JobMode.RunAsync(args[1..], JobRunner.Names, (name, jobArgs) => JobRunner.RunAsync(builder.Configuration, name, jobArgs));

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

//Localize
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("ja-JP")
    };
    //set neutral as default
    options.DefaultRequestCulture = new RequestCulture(string.Empty);
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    options.RequestCultureProviders.Add(new CustomRequestCultureProvider(async context =>
    {
        //localization by the request header
        var userLanguages = context.Request.Headers["Accept-Language"].ToString();
        var firstLanguage = userLanguages.Split(',').FirstOrDefault();
        if (firstLanguage == "ja") firstLanguage = "ja-JP";

        return await Task.FromResult(new ProviderCultureResult(firstLanguage));
    }));
});

builder.Services.AddHttpContextAccessor();
//リクエストが使うデザインは最初に参照した時点の版に固定する
builder.Services.AddScoped<RequestDesign>();
builder.Services.AddScoped<DataService>();
//監査ログ (appsettings の AuditLog で有効化)。出力先は Extras/AuditSinkTable
builder.Services.AddAuditLog(SystemConfig.Instance.AuditLog, AuditSinkTable.Create(),
    http => http?.RequestServices.GetRequiredService<RequestDesign>().Version ?? DesignerService.GetCurrent().Version);

var app = builder.Build();
//意味検索の警告ログ (埋め込みの失敗など) をアプリのログに出す
SemanticSearchTable.Service.LoggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
//画面のボタン (JobTriggerField) から起動したジョブの失敗をアプリのログに出す
JobProcess.LoggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
//非同期で起動したジョブの状態の置き場 (複数インスタンスでは共有キャッシュに差し替える。CookieAuthentication.cs の AddDistributedMemoryCache)
JobProcess.Cache = app.Services.GetRequiredService<IDistributedCache>();

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

//サーバーライセンスの状態はデザインを返す前に更新する (このリクエストがデザインを読むより前に)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/module_data/design")) await LicenseService.UpdateAsync(context.Request);
    await next();
});

app.UseRouting();
//監査ログは認証・認可の前に置く (401/403 も記録するため)
app.UseAuditLog();

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
return 0;
