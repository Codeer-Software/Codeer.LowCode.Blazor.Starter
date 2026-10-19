using LowCodeApp.Jobs;
using Microsoft.Extensions.Configuration;

//使い方: LowCodeApp.Batch <ジョブ名> [引数...]
//ジョブは LowCodeApp.Jobs に IJob を実装して足す
Console.OutputEncoding = System.Text.Encoding.UTF8;
if (args.Length == 0)
{
    Console.WriteLine("使い方: LowCodeApp.Batch <ジョブ名> [引数...]");
    Console.WriteLine($"ジョブ: {string.Join(", ", BatchRunner.GetJobNames())}");
    return BatchRunner.JobNotFound;
}

//タスクスケジューラ等ではカレントフォルダが exe の場所とは限らないので、設定は exe の隣から読む。
//appsettings.<環境>.json は環境変数 DOTNET_ENVIRONMENT (無ければ Production) のものだけ読む (Visual Studio からは launchSettings.json で Development)。
//環境変数 (Azure のアプリ設定を含む) が最後に勝つ。階層は "__" で区切る (例 ConnectionStrings__Main)
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

return await BatchRunner.RunAsync(config, args[0], args[1..]);
