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

## モジュールを C# のクラスで扱う

モジュール名・フィールド名を文字列で書く代わりに、モジュールに対応したクラス（POCO。クラス名＝モジュール名、プロパティ名＝フィールド名、型＝`TextFieldData` などの FieldData 型）で読み書きできます。
クラスは手で書かず、デザイナの `poco` コマンドでデザインから生成します。**デザインを変えたら生成し直します**（生成したファイルは手で編集しない）。

```powershell
Start-Process -FilePath "Source/LowCodeApp.Designer/bin/Debug/net10.0-windows/LowCodeApp.Designer.exe" -Wait -ArgumentList @(
  "poco", "DesignProjects/Project/design",
  "--namespace", "LowCodeApp.Client.Shared.Modules",
  "--out-file", "Source/LowCodeApp.Client.Shared/Generated/Modules.g.cs",
  "--out", "Local/poco.json")
Get-Content "Local/poco.json"
```

- 1 つ目の引数はデザインプロジェクトのフォルダ（`app.clprj` のあるところ）。`--namespace` は `LowCodeApp.Client.Shared` のルート名前空間 + `.Modules`
- `Local/poco.json` の `modules` が生成したクラス、`warnings` が生成しなかったモジュール・フィールド（データを持たないモジュールなど）。失敗したときは `error`
- リンク越しの項目は生成されません。クラスは `partial` なので、別ファイルに同じ名前空間で `public partial class <モジュール名>` を書き、`[Link] public TextFieldData? Customer_Name { get; set; }`（`Customer.Name`）のように足します

```csharp
public class CloseOrdersJob : IJob
{
    public string Name => "CloseOrders";

    public async Task RunAsync(DataService data, string[] args)
    {
        var orders = await data.ModuleDataIO.GetListAsync<Order>(e => e.Status!.Value == "Open");

        var submit = new ModuleSubmitData { ModuleName = nameof(Order) };
        foreach (var order in orders)
        {
            submit.Update.Add(ModuleData.Create(new Order { Id = order.Id, Status = new() { Value = "Closed" } }));
        }
        var results = await data.ModuleDataIO.SubmitWithTransactionAsync([submit]);
        var error = results.FirstOrDefault(e => !string.IsNullOrEmpty(e.ExceptionMessage))?.ExceptionMessage;
        if (error != null) throw new InvalidOperationException(error);
        Console.WriteLine($"{orders.Count} 件を締めました");
    }
}
```

- `GetListAsync<T>` は条件に当たる行を全部返す（`SearchCondition<T>` に `Take(n)` を付けたときは先頭 n 件）。並び・件数・ページ分け・集計（`AggregateCondition<T>`）は
  `ClaudeCodeForDeveloper/_specs/HostCustomization.md` の「C# からデータを読み書きする」
- 書き込みは `SubmitWithTransactionAsync` 1 回が 1 トランザクション。失敗は結果の `ExceptionMessage` に入る（例外にはならない）ので、上のように確かめて投げる

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
