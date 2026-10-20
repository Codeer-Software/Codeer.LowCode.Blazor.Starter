# ジョブ（バッチ）

`LowCodeApp.Server` は Web サーバーのほかに、コマンドラインでジョブを 1 つ実行して終了する起動モードを持っています。

```
dotnet LowCodeApp.Server.dll job <ジョブ名> [引数...]
dotnet LowCodeApp.Server.dll job Sample AppUser
```

Web ホストは立てず、同じ `appsettings` と同じ `App.zip` でデザイン・接続・`ModuleDataIO` を組み立ててジョブを走らせます。
モジュール名・フィールド名で読み書きでき、権限の条件・パスワードのハッシュ・`CustomizedModuleDataIO` の override・編集履歴・意味検索の索引付けなど、
Web から保存したときと同じ処理を通ります。独自フィールド型（`LowCodeApp.Client.Shared`）もそのまま使えます。

終了コードは 成功 0 / 失敗（例外）1 / ジョブが無い 2。`job` だけで実行すると使い方とジョブの一覧を出します。
ライセンスの確認はしません（実行時にライセンスで止まりません）。

## ジョブの足し方

このフォルダに `IJob` を実装したクラスを足します（引数なしの public コンストラクタ）。登録は要りません。

```csharp
public class CloseOrdersJob : IJob
{
    public string Name => "CloseOrders";

    public async Task RunAsync(DataService data, string[] args)
    {
        var page = await data.ModuleDataIO.GetListAsync(new SearchCondition("Order"), 0);
        //...
    }
}
```

- `Name` がコマンドラインのジョブ名（大文字小文字は区別しない）。`args` はジョブ名の後ろの引数
- `data` は Web のリクエストと同じ `DataService`。`data.ModuleDataIO` で読み書きする（`GetListAsync` / `SubmitAsync` など）。`data.DbAccess` は同じ接続、`data.Design.DesignData` は読み込んだデザイン
- 失敗は例外を投げる。終了コードが 1 になり、例外の内容が標準エラーに出る
- 例は `SampleJob.cs`

## 設定

Web と同じ `appsettings.json` / `appsettings.<環境>.json` / 環境変数を読みます（環境は `ASPNETCORE_ENVIRONMENT`、無ければ `Production`）。
設定は exe の隣から読むので、タスクスケジューラ等でカレントフォルダが違っても動きます。

| キー | 意味 |
|---|---|
| `Jobs:UserId` | ジョブを実行するユーザーの Id（ユーザーモジュールの Id）。権限の条件はこのユーザーで判定される。空なら匿名（アプリにログインが要るデザインでは読み書きできない） |

Visual Studio からは起動プロファイル `job`（`Properties/launchSettings.json`。引数 `job Sample AppUser`）で実行できます。

## Azure WebJobs から起動する

Web を公開した App Service にトリガー WebJob を足し、`run.cmd` 1 本の zip をアップロードします。中身は公開済みのサイトの dll をそのまま叩きます。

```
dotnet %HOME%\site\wwwroot\LowCodeApp.Server.dll job <ジョブ名> [引数...]
```

定期実行は種類を「トリガー」にして CRON 式（または `settings.job` の `schedule`）で指定します。接続文字列などは Web と同じ App Service の「構成」（アプリ設定・接続文字列）が環境変数として読まれるので、ジョブ用に別の設定は要りません。
ジョブを分けるときは WebJob を複数作り、それぞれの `run.cmd` でジョブ名を変えます。

## タスクスケジューラから起動する

「操作」の「プログラム/スクリプト」に `dotnet`（または `dotnet.exe` のフルパス）、「引数の追加」に `<配置先>\LowCodeApp.Server.dll job <ジョブ名> [引数...]` を指定します。
結果は終了コード（タスクの「前回の実行結果」）で確認でき、出力を残したいときは `cmd /c dotnet ... > log.txt 2>&1` の形で起動します。
