# LowCodeApp.Batch

デザインプロジェクト（`App.zip`）の設定を使って、C# から DB の読み書きをするバッチ（コンソールアプリ）です。
Web サーバーを介さず、プロセスの中でデザイン・接続・`ModuleDataIO` を組み立てます。モジュール名・フィールド名で読み書きでき、
権限の条件・パスワードのハッシュ・編集履歴・意味検索の索引付けなど、Web から保存したときと同じ処理を通ります。

| プロジェクト | 役割 |
|---|---|
| `LowCodeApp.Batch` | 入口（`Program.cs`）。設定を読み、コマンドラインのジョブ名でジョブを 1 つ実行して終了コードを返す |
| `LowCodeApp.Jobs` | ジョブと実行の仕組み。**ジョブはここに足す** |
| `LowCodeApp.Client.Shared` | 独自フィールド型など（Web のホスト・デザイナと共通） |

## appsettings

`appsettings.json` と `appsettings.<環境>.json`（exe の隣。環境は環境変数 `DOTNET_ENVIRONMENT`、無ければ `Production`。Visual Studio から実行するときは `launchSettings.json` が `Development` にする）と環境変数から読みます。環境変数が最後に勝ちます（階層は `__` で区切る。例 `ConnectionStrings__SampleSQLite`）。
項目は Web のサーバー（`LowCodeApp.Server`）と同じ名前です。

| キー | 意味 |
|---|---|
| `ConnectionStrings:<Name>` / `DataSources[]` | データソースと接続文字列。デザインプロジェクトの `designer.settings.json` のデータソース名と合わせる |
| `DesignFileDirectory` | `App.zip` を読むフォルダ。デザイナのデプロイ先（Web のサーバーと同じ場所）にする |
| `FileSystemStorages[]` など | FileField の保存先 |
| `TemporaryFileTableInfo[]` | アップロード一時ファイルの管理テーブル |
| `Mail` / `Smtp` / `GraphApi` / `SendGrid` / `Gmail` | メール送信 |
| `SemanticSearch` / `AzureOpenAIEmbedding` | 意味検索の索引付けに使う埋め込みモデル |
| `FontFileDirectory` | PDF 出力用フォント |
| `SqlLog` | `true` で実行した SQL を標準出力に出す |
| `Batch:UserId` | ジョブを実行するユーザーの Id（ユーザーモジュールの Id）。権限の条件はこのユーザーで判定される。空なら匿名（アプリにログインが要るデザインでは読み書きできない） |

接続文字列・鍵は `appsettings.Development.json` か環境変数に書き、`appsettings.json` には名前と種別だけ書きます。

## ジョブの足し方

`LowCodeApp.Jobs` に `IJob` を実装したクラスを足します（引数なしの public コンストラクタ）。登録は要りません。

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
- `data.ModuleDataIO` で読み書きする（`GetListAsync` / `SubmitAsync` など）。`data.DbAccess` は同じ接続、`data.Design` は読み込んだデザイン
- 失敗は例外を投げる。終了コードが 1 になり、例外の内容が標準エラーに出る
- 例は `SampleJob.cs`

## 実行方法

```
dotnet LowCodeApp.Batch.dll <ジョブ名> [引数...]
dotnet LowCodeApp.Batch.dll Sample AppUser
```

終了コードは 成功 0 / 失敗（例外）1 / ジョブが無い 2。引数なしで実行すると使い方とジョブの一覧を出します。
ライセンスの確認はしません（実行時にライセンスで止まりません）。

## Azure WebJobs から起動する

`dotnet publish -c Release` の出力に、`run.cmd`（中身は `dotnet LowCodeApp.Batch.dll <ジョブ名> [引数...]`）を足して zip にし、App Service の WebJobs にアップロードします。
定期実行は種類を「トリガー」にして CRON 式（または `settings.job` の `schedule`）で指定します。接続文字列などは App Service の「構成」（アプリ設定・接続文字列）に置けば環境変数として読まれます。`DOTNET_ENVIRONMENT` を設定しなければ `appsettings.Development.json` は読まれません。
ジョブを分けるときは WebJob を複数作り、それぞれの `run.cmd` でジョブ名を変えます。

## タスクスケジューラから起動する

「操作」の「プログラム/スクリプト」に `dotnet`（または `dotnet.exe` のフルパス）、「引数の追加」に `<配置先>\LowCodeApp.Batch.dll <ジョブ名> [引数...]` を指定します。
設定は exe の隣から読むので「開始」フォルダは指定しなくても動きます。結果は終了コード（タスクの「前回の実行結果」）で確認でき、出力を残したいときは `cmd /c dotnet ... > log.txt 2>&1` の形で起動します。
