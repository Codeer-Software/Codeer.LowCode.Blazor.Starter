# ジョブ（バッチ）

`LowCodeApp.Server` は Web サーバーのほかに、コマンドラインでジョブを 1 つ実行して終了する起動モードを持っています。

```
dotnet LowCodeApp.Server.dll job <ジョブ名> [引数...]
dotnet LowCodeApp.Server.dll job CloseOrders
```

Web ホストは立てず、同じ `appsettings` と同じ `App.zip` でデザイン・接続・`ModuleDataIO` を組み立ててジョブを走らせます。
モジュール名・フィールド名で読み書きでき、権限の条件・パスワードのハッシュ・`CustomizedModuleDataIO` の override・編集履歴・意味検索の索引付けなど、
Web から保存したときと同じ処理を通ります。独自フィールド型（`LowCodeApp.Client.Shared`）もそのまま使えます。

終了コードは 成功 0 / 失敗（例外）1 / ジョブが無い 2。`job` だけで実行すると使い方とジョブの一覧を出します。
ライセンスの確認はしません（実行時にライセンスで止まりません）。

ジョブは 2 種類あります。このフォルダに C# で書く `IJob` と、デザインに置く Job スクリプト（`Jobs/<ジョブ名>.job.cs`）です。どちらも同じ「ジョブ」で、起動のしかた（コマンドライン・画面のボタン・WebJobs / タスクスケジューラ）も同じです。同じ名前のジョブが両方にあれば `IJob` が動きます。

## ジョブの足し方

このフォルダに `IJob` を実装したクラスを足します（引数なしの public コンストラクタ）。登録は要りません。次の 1 ファイルで動きます。

```csharp
using Codeer.LowCode.Blazor.Repository.Match;
using LowCodeApp.Server.Infrastructure;

namespace LowCodeApp.Server.Jobs
{
    /// <summary>
    /// 例: dotnet LowCodeApp.Server.dll job CloseOrders
    /// </summary>
    public class CloseOrdersJob : IJob
    {
        public string Name => "CloseOrders";

        public async Task<string?> RunAsync(DataService data, string[] args)
        {
            var page = await data.ModuleDataIO.GetListAsync(new SearchCondition("Order"), 0);
            //... 締めの処理。保存も data.ModuleDataIO で行う
            return $"{page.TotalCount} 件を締めました";
        }
    }
}
```

- `Name` がコマンドラインのジョブ名（大文字小文字は区別しない）。`args` はジョブ名の後ろの引数
- 戻り値は成功したときの文言（画面のボタンから起動したときは成功の通知に出る。`null` なら既定の文言）。標準出力の最後に 1 行で書かれる
- `data` は Web のリクエストと同じ `DataService`。`data.ModuleDataIO` で読み書きする（`GetListAsync` / `SubmitAsync` など）。`data.DbAccess` は同じ接続、`data.Design.DesignData` は読み込んだデザイン
- 失敗は例外を投げる。終了コードが 1 になり、例外のメッセージが標準エラー（画面のボタンから起動したときは 1 行目が画面に出る文言）、例外の全文が標準出力に出る
- ジョブの書き込みは `data.ModuleDataIO` で行う。生 SQL では編集履歴・意味検索の索引が付かない

## デザインの Job スクリプト

C# を書かずに、デザインプロジェクトの `Jobs/` にスクリプトを置いてジョブにできます。ホストのビルドは要らず、デザインを送信（App.zip）すれば動きます。

- 置き場所はデザインプロジェクトの `Jobs/` の直下（サブフォルダは不可）。ファイル名 `<ジョブ名>.job.cs` の最初のドットより前がジョブ名
- 入口は `Run`。戻り値は成功したときの文言で、`IJob` の戻り値と同じ扱い（画面のボタンから起動したときは成功の通知に出る。標準出力の最後に 1 行で書かれる）。文言が要らなければ `void Run(JobArguments args)`

```csharp
// Jobs/CloseOrders.job.cs   →  dotnet LowCodeApp.Server.dll job CloseOrders
string Run(JobArguments args)
{
    var search = new ModuleSearcher<Order>();
    search.AddEquals(e => e.Status.Value, "Open");
    var rows = search.ExecuteRaw();
    var submit = new ModuleSubmitter();
    foreach (var row in rows)
    {
        var change = new Order.Data();
        change.Id = new IdFieldData();
        change.Id.Value = row.Id.Value;
        change.Status = new TextFieldData();
        change.Status.Value = "Closed";
        submit.Update(change);
    }
    var result = submit.Execute();
    if (!result.Succeeded) Fail(result.Error);
    Logger.Log($"{rows.Count} 件");
    return $"{rows.Count} 件を締めました";
}
```

- 読み書きは `IJob` と同じ `data.ModuleDataIO` を通る（権限は `Jobs:UserId` のユーザー、画面のボタンから起動したときは押したユーザー。編集履歴・意味検索の索引も付く）
- スクリプトの `Logger` は標準出力に出る（`Logger.Warn` / `Logger.Error` は行の先頭に `warn:` / `error:` が付く）。サーバー内実行（下の「実行先」）ではアプリのログに出る
- `Fail("理由")` を呼んだとき、またはスクリプトの実行時エラーのときはジョブの失敗になり、終了コードは 1。理由は標準エラーに出る（画面のボタンから起動したときは画面に出る文言）
- 書き込みはジョブの 1 回の実行全体が 1 つのトランザクション。正常に終われば確定し、失敗すれば取り消す。`DBService.Commit()` でそこまでを確定できる（その後に失敗しても確定した分は残る）。`Execute()` が失敗したときは、最後の確定（無ければ実行の始め）以降の書き込みが、前の `Execute()` で成功した分も含めて取り消される
- `JobTriggerField` から起動したときは、ジョブに渡る JSON（下の「ジョブに渡る JSON」）が解かれて `args.Data`（画面の現在値）と `args.Submit`（保存ボタンが送るのと同じ内容）に入る。無ければ `null`
- コマンドラインの引数（ジョブ名の後ろ）は `args.Options`（`string[]`）
- メールの送信（`MailService.Send`）と Excel（テンプレートへの書き込み・xlsx / PDF の添付）、デザインのリソース（`Resources`）が使える。メールの設定（送信インフラ・`DebugRedirectAllTo`・送信履歴）は画面のメールと同じ `appsettings` の `Mail`。使えるようにしているのは `Jobs/JobRunner.cs` の `JobScripts.UseMail` / `UseExcel`。送ったメールはジョブが後で失敗しても取り消せないので、書き込みを `DBService.Commit()` で確定してから送る
- スクリプトの書き方（使える型・データの読み書き・`Fail`・他のスクリプトの呼び出し）は、デザイナの AI ワークスペースの `AIForDesigner/_specs/Jobs.md`

## 画面のボタンから起動する（JobTriggerField）

モジュールに `JobTriggerField`（Codeer.LowCode.Blazor.Extras）を置くと、そのボタン（またはスクリプトの `Run()`）でジョブが動きます。どこで動かすか（実行先）はジョブごとに決めます。フィールドの振る舞い（渡る JSON の形・通知）はフィールドの仕様（デザイナのフィールドカタログ）にあり、ここはこのテンプレートの実装と制限です。

### 実行先

| 実行先 | 動き方 |
|---|---|
| 別プロセス（既定） | サーバーがこのモードを別プロセスとして起動する（`job <ジョブ名> --stdin`） |
| サーバー内実行 | Web サーバーのプロセスの中でそのまま動かす |

`appsettings.json` の `Jobs:RunIn` に `"ジョブ名": "Server"` と書いたジョブがサーバー内実行になります。書いていないジョブは別プロセスです。`IJob` も Job スクリプトもどちらでも動きます。

```json
"Jobs": {
  "RunIn": { "CloseOrders": "Server" }
}
```

サーバー内実行の性質:

- 起動が速い（プロセスを起こさない）
- Web サーバーと同じプロセスなので、メモリと CPU を画面の処理と分け合う
- サーバーの再起動・デプロイで実行中のジョブは止まる（Job スクリプトなら確定前の書き込みは取り消される）
- スクリプトの `Logger` はアプリのログに出る。標準出力は使わない
- `SqlLog` などの設定は Web サーバーのものがそのまま効く
- ジョブは押したユーザーの Id で自分の `DataService` を作って動く（権限・編集履歴・意味検索の索引は別プロセスと同じ）
- 待つ / 待たない（`IsAsynchronous`）・同時実行の判定（`AllowsConcurrentRuns`）・状態の問い合わせは別プロセスと同じ。同時実行の判定は実行先をまたいでジョブ名で数える

短く軽いジョブはサーバー内実行、長く重いジョブは別プロセスを勧めます。

### 仕組み（どこに何があるか）

- クライアント: Extras の `JobTriggerField` が共通の API（`ExtrasTransport.EndPoint` = `/api/extras`。`Controllers/ExtrasController.cs`）に `JobRunRequest { ModuleName, FieldName, Arguments }` を送り（Feature `Job` / Action `Run`）、`JobRunResponse { Succeeded, Error, Started, JobId, Message }` を受け取る（どちらも `Codeer.LowCode.Blazor.Extras.Jobs`）
- サーバー: `Extras/ExtrasApiTable.cs` に並べた Extras.Server の `JobApi` が、そのモジュールとフィールドに書けるユーザーか（`AuthorizationChecker.CheckFieldUserWriteAsync`。`ExecuteSqlField` の実行と同じ軸）を確かめ、**デザインの `JobTriggerField`** の `JobName` のジョブを `IsAsynchronous` に従って起動する（クライアントからジョブ名や待つかどうかは受け取らない）。実行先は `JobApi` に渡した `JobRunner.RunsInServer` で決まり、サーバー内実行は `JobRunner.RunAsync` を呼ぶ。`AllowsConcurrentRuns` が `false`（既定）なら、同じ名前のジョブが実行中のときは起動せず `Error`（「ジョブ 'X' は実行中です」）を返す（`JobProcess` がジョブ名で判定する。同期・非同期とも、実行先によらず）。起動の前に要求の DB 接続をプールへ返す（同期でジョブを待つ間に接続を握らない）
  - `JobName` のジョブが無ければ起動せず `Succeeded = false` と `Error`（「ジョブ 'X' がありません」）を返す（同期・非同期とも）
  - 同期: 終わるまで待ち、成功なら `Succeeded = true` と `Message`（`IJob.RunAsync` または Job スクリプトの `Run` の戻り値。画面の成功の通知になる）、失敗なら `Error`（例外のメッセージ）を返す
  - 非同期: 起動したら `Started = true` と `JobId` を返す（画面は「開始しました」を通知し、フィールドの `JobId` / `JobIds` に持つ）。状態はスクリプトがフィールドの `GetJobStatus()` で問い合わせ、共通の API（Action `Status`）が `JobStatusResponse { Status, Message, Error }` を返す。状態の置き場は `JobProcess.Cache`（`IDistributedCache`。`Program.cs` でつないでいる。テンプレートはメモリキャッシュなのでサーバーを再起動すると消える。複数インスタンスでは Redis 等の共有キャッシュに差し替える）。終わった結果は終了から 30 分（`JobProcess.FinishedRetention`）で消え、消えた後は応答なし（`GetJobStatus` は null）。状態を返すのは起動したユーザーにだけ
- 別プロセスの起動（親）とジョブモード（子）は Codeer.LowCode.Blazor.Extras.Server の `JobProcess` と `JobMode` が対で持つ（引数の並び `job <ジョブ名> --stdin`・画面の内容は標準入力・押したユーザーの Id は環境変数 `Jobs__UserId`・終了コード・UTF-8・結果の文言の取り出し）。このテンプレートにあるのは、ジョブの一覧（`IJob` とデザインの Job スクリプト）と実行先の判定と `DataService` の組み立てと Job スクリプトの実行（`Jobs/JobRunner.cs`）と、`Program.cs` の `job` の分岐だけ。約束の詳細は Codeer.LowCode.Blazor.Extras の `docs/Jobs.md`
- サーバー内実行では、画面の内容は `args[0]`、押したユーザーの Id は `JobRunner.RunAsync` の引数で渡る（ジョブから見た `args` と権限は別プロセスと同じ）
- 失敗（同期・非同期とも）はサーバーのログにも出る（`Program.cs` で `JobProcess.LoggerFactory` をつないでいる。別プロセスは標準エラーの全文と標準出力をまとめて、サーバー内実行は例外の全文を出す）
- 別プロセスで起動する実行ファイルは `LowCodeApp.Server.dll` の隣の `LowCodeApp.Server.exe`（apphost。IIS の下や PATH に `dotnet` が無いサーバーでも動く）、無ければ `dotnet LowCodeApp.Server.dll`。変えるときは `JobProcess.HostExecutable` / `EntryDll` / `DotnetPath`
- 手で動かすときは `echo {"data":{},"submit":null} | dotnet LowCodeApp.Server.dll job CloseOrders --stdin`

### ジョブに渡る JSON

`args[0]` は JSON 1 つです（`UseSubmitData` が既定の `true` のとき。`false` なら `{}`）。

```json
{"data": ModuleData, "submit": [ModuleSubmitData] または null}
```

- `data` は画面の現在値。`JsonConverterEx.DeserializeObject<ModuleData>(data の中身)` で戻す。`data` はボタン起点のときだけ渡る。将来の別の起点（保存時・スケジュール）では無いことがあるので、ジョブは `data` の有無を見て書く
- `submit` は保存ボタンが送るのと同じ内容。送るものが無い（変更なし・表を持たないモジュール）なら `null`。`JsonConverterEx.DeserializeObject<List<ModuleSubmitData>>(submit の中身)` で戻す

```csharp
using var json = JsonDocument.Parse(args[0]);
if (json.RootElement.TryGetProperty("data", out var current))
{
    var moduleData = JsonConverterEx.DeserializeObject<ModuleData>(current.GetRawText())!;
    //画面の内容を読んで処理する
}
```

コマンドラインから起動したときの `args` はジョブ名の後ろの引数です。両方から起動するジョブは、`args[0]` が `{` または `[` で始まるかで見分けます。

### 制限

- JSON はクライアントが作ったもの。`data.ModuleDataIO` に渡す分（`SubmitWithTransactionAsync` 等）は権限で守られるが、それ以外（ファイルのパス・SQL・外部 API の引数など）に使うならジョブで検証する
- ジョブの中の書き込みは、実行先によらず属性ベースの監査ログ（`[Audit]`）に載らない。記録されるのは `JobApi` の呼び出し 1 件（誰が・どのモジュールのどのジョブを・成否（非同期では起動の成否））だけ。ジョブの中で何をしたかの記録はジョブ本体で行う
- 同期では画面がジョブの終わりまで待つ。途中で通信が切れると結果は分からない（ジョブは動き続けている）。長い処理は非同期に（画面を待たせず、結果はスクリプトが `GetJobStatus` で問い合わせる）
- `AllowsConcurrentRuns` が `false` のときの実行中の判定は 1 台のサーバーの中だけ（複数台では止まらない）。複数台で排他が要るならジョブが自分で取る。非同期の状態は共有キャッシュにすれば複数台でも問い合わせられる
- ジョブが保存するときは `data.ModuleDataIO` で（編集履歴・意味検索の索引が付く）
- 別プロセスでは、成功したジョブの標準出力はどこにも残らない（画面の通知に使うのは最後の 1 行だけ。失敗したときは上のとおり全文がサーバーのログに出る）。進捗を残すならジョブで ILogger や DB に書き、受け取った JSON や秘密情報を標準出力に出さない
- サーバー内実行では標準出力は使わない（`IJob` が `Console` に書いた分は Web サーバーの標準出力に混ざる）。進捗はスクリプトの `Logger`（アプリのログ）か DB に書く
- サーバー内実行のジョブは Web サーバーの再起動・デプロイで止まる。非同期の状態は、テンプレートのメモリキャッシュなら消え、共有キャッシュなら `Running` のまま残る

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

    public async Task<string?> RunAsync(DataService data, string[] args)
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
        return $"{orders.Count} 件を締めました";
    }
}
```

- `GetListAsync<T>` は条件に当たる行を全部返す（`SearchCondition<T>` に `Take(n)` を付けたときは先頭 n 件）。並び・件数・ページ分け・集計（`AggregateCondition<T>`）は
  `AIForDeveloper/_specs/HostCustomization.md` の「C# からデータを読み書きする」
- 書き込みは `SubmitWithTransactionAsync` 1 回が 1 トランザクション。失敗は結果の `ExceptionMessage` に入る（例外にはならない）ので、上のように確かめて投げる

## 設定

Web と同じ `appsettings.json` / `appsettings.<環境>.json` / 環境変数を読みます（環境は `ASPNETCORE_ENVIRONMENT`、無ければ `Production`）。
設定は exe の隣から読むので、タスクスケジューラ等でカレントフォルダが違っても動きます。

| キー | 意味 |
|---|---|
| `Jobs:UserId` | ジョブを実行するユーザーの Id（ユーザーモジュールの Id）。権限の条件はこのユーザーで判定される。空なら匿名（アプリにログインが要るデザインでは読み書きできない） |
| `Jobs:RunIn` | 画面のボタンから起動したジョブの実行先（ジョブ名 → 実行先）。`"Server"` でサーバー内実行。書いていないジョブは別プロセス（上の「実行先」）。Web サーバーが読む設定で、コマンドラインの起動には関係しない |

Visual Studio からは起動プロファイル `job`（`Properties/launchSettings.json`。引数 `job`。そのままではジョブの一覧が出るので、引数にジョブ名を足して実行します）で実行できます。

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
