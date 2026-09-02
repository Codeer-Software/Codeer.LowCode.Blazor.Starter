# Codeer.LowCode.Blazor.Starter - Claude Code 向けガイド

このリポジトリは Codeer.LowCode.Blazor（Blazor 向けローコードフレームワーク、NuGet 配布の商用製品）で
アプリケーションを始めるための**ビルド可能な完成形**の集まり。`Source/Hosts/` 配下のフォルダ 1 つ＝ホストアプリ 1 種類。

用語: **ホスト**＝デザインプロジェクト（画面・データ・スクリプト＝ローコードで作る中身）を動かす C# アプリケーション。
このリポジトリにあるのはホスト。デザインプロジェクトはデザイナ（またはテンプレート CLI）で作り、ホストの `DesignFileDirectory` にデプロイする。
中身は Visual Studio テンプレートが `LowCodeApp` という名前で生成する結果そのもの（フレームワークは NuGet 参照）。

## 最初にやること（空フォルダから始めたとき）

ユーザーが「この URL（リポジトリ）を見て指示に従って」「セットアップして」と言ってきて、まだビルドも起動もしていない状態なら、
**[ClaudeCodeForDeveloper/claude-code-setup.md](ClaudeCodeForDeveloper/claude-code-setup.md) を読んで、その手順を上から実行する**（環境確認 → 取得 → ビルド →
デザインプロジェクト作成 → Claude Code ワークスペース展開 → サーバーとデザイナの起動）。質問は最小限、既定値で一本道。

## このフォルダで Claude Code ができること（優先順）

セットアップ後のフォルダ構成。**ホストを触る人はリポジトリのルートで、デザインを作る人は `DesignProjects/<デザイン名>/` で** Claude Code を起動する。
ルートで起動してもデザインは編集できる（`.claude/settings.local.json` に同じ許可があり、フックが各デザインワークスペースの自動更新を代行する）。

```
<ROOT>/
├── CLAUDE.md                        ← この文書 (ホスト側の説明・作業ルール)
├── .claude/                         ← Claude Code の設定。settings.json (共通・コミット済み) と settings.local.json (デザイナ exe パス入りの許可・フック。developer-workspace が生成。gitignore)
├── ClaudeCodeForDeveloper/          ← ホスト (C#) を触る Claude Code 向けの文書
│   ├── claude-code-setup.md         ←   セットアップ手順 (コミット済み・Claude Code が実行する)
│   ├── _specs/                      ←   ライブラリの拡張点リファレンス (デザイナの developer-workspace が生成。gitignore)
│   └── _hooks/                      ←   ルート用フック (DesignProjects/*/ のワークスペースを巡回して最新化。同上)
├── Source/Hosts/<Variant>/          ← ホストのソース (C#)。最後の手段
├── Source/Hosts/Common/LowCodeApp.SeleniumTest/ ← Selenium (NUnit) テスト。PageObject は生成、Scenario を書く
├── DesignProjects/                  ← デザインプロジェクト (画面・データ・スクリプト)。1 フォルダ = 1 デザイン
│   └── <デザイン名>/                ←   Claude Code ワークスペース (デザイン担当者はここで起動)
│       ├── CLAUDE.md                ←     デザイン作業の規約 (デザイナが生成)
│       ├── ClaudeCodeForDesigner/   ←     デザインの作り方・仕様・カタログ (デザイナが自動生成。手で編集しない)
│       ├── Project.md               ←     このデザイン固有のルール (ユーザー所有。守る)
│       ├── ddl/  docs/              ←     テーブル定義 SQL / このデザイン固有の文書
│       └── design/                  ←     デザイン本体 (app.clprj / Modules / PageFrames …)。デザイナが開く・デプロイされる範囲
├── Local/                           ← ローカル実行用 (Data = SQLite / Designs = デプロイ先 App.zip / Storages / Font)
└── .vscode/                         ← VS Code の起動・ビルド設定 (Visual Studio が無い環境向け)
```

1. **画面・データ構造・業務ルールの追加変更 → `DesignProjects/<デザイン名>/design/` を編集する（ローコード）。** やり方は
   そのフォルダの `CLAUDE.md` と `ClaudeCodeForDesigner/CLAUDE.md`（**着手前に必ず読む**。ルートで起動した Claude Code がデザインを触るときも同じ）。
   デザインの変更に C# の再ビルドは要らない。反映はデザイナの「送信」か CLI の `deploy`
2. **設定で表せないことだけスクリプト（`*.mod.cs`）。最小限に。** スクリプトを書く前に「既存フィールド／プロパティで代替できないか」を必ず問う
3. **C#（このリポジトリの `Source/`）を触るのは最後の手段。** フィールド型そのものが無い・性能をコードで改善したい・独自 API / 外部連携が要る、
   などローコードの範囲で不可能と確定したときだけ。やり方は下の「ホストのカスタマイズ」と `ClaudeCodeForDeveloper/_specs/HostCustomization.md`

**サンプルを土台にしない**: セットアップ直後の `DesignProjects/<テンプレート名>/` はサンプル集（ショーケース）。ユーザーが自分の業務アプリを求めたら、
サンプルに増築せず、空のプロジェクト（`Empty` / 認証付きは `Empty`）から別のデザインプロジェクトを作ることを提案して確認する（下の「デザインプロジェクトの切り替え」）。

**自動テスト (Selenium)**: ユーザーが求めたら `DesignProjects/<デザイン名>/ClaudeCodeForDesigner/Docs/SeleniumTestGuide.md` の手順で。テストプロジェクトは
`Source/Hosts/Common/LowCodeApp.SeleniumTest`（Web バリアントの `LowCodeApp.sln` に含まれている。`selenium-test-init` で新規展開しなくてよい）。
`pageobject "DesignProjects/<デザイン名>/design" --out-dir Source/Hosts/Common/LowCodeApp.SeleniumTest/PageObject --namespace LowCodeApp.SeleniumTest.PageObject` で PageObject を生成し、
`testsettings.json` の `BaseUrl` / `DataSources`、`testsettings.local.json`（gitignore）の `ConnectionStrings` を合わせて `dotnet test`。テストデータは同梱の DataManager。

`ClaudeCodeForDeveloper/_specs/` が無い（セットアップ前・パッケージ更新後）なら `developer-workspace`、デザインプロジェクトの `ClaudeCodeForDesigner/` が無いなら
`claude-workspace` で展開する（コマンドは `ClaudeCodeForDeveloper/claude-code-setup.md` の Step 5 / Step 7）。

## バリアントの選び方

公開しているホストは 2 つ。**既定は `Cookie`**（Visual Studio テンプレート名も接尾辞なしの `Codeer.LowCode.Blazor`）。

| フォルダ | 用途 | 選ぶ基準 |
|---|---|---|
| `Source/Hosts/Cookie/` | Web（WASM クライアント + ASP.NET Core サーバー）、Cookie 認証（ユーザーテーブルでパスワード検証） | 業務アプリの既定。Entra ID / OIDC / TOTP など独自の認証を組むときもここを土台にする（cookie スキームと `[Authorize]`、現在ユーザー解決はそのまま使え、ログインの発行方法だけ差し替える） |
| `Source/Hosts/Maui/` | `Cookie` + .NET MAUI（Android/iOS）クライアント | スマホアプリとして配布したい。デザイン変更はストア更新なしで反映される |

`Source/Hosts/` にはほかに `Normal`（認証なし）/ `Wpf` `WinForms`（デスクトップ単体）/ `MultiTenant`（マルチテナント）も**保守用**に置いてあるが、
テンプレートとしては提供していない。ユーザーに勧めない（認証なしが要るときは下の「認証を外す」）。

### 認証を外す（前段のリバースプロキシや Easy Auth で守られている等、ログイン画面が要らないとき）

`Cookie` から引き算する。触るのは 6 か所: `Server/CookieAuthentication.cs`（cookie スキームの登録。`Program.cs` の呼び出しごと）、
`Server/Controllers/AccountController.cs`、`Server/PasswordCheckUser.cs`（+ `appsettings*.json` の `PasswordCheckUserTableInfo` と `SystemConfig` の該当プロパティ）、
各 Controller の `[Authorize]`、`Client/wwwroot/login.html` と `Client/LoginInfo.cs`（`Program.cs` / `NavigationService.cs` のログイン遷移）、
`Services/DataService.cs` の現在ユーザー解決（前段認証のヘッダから取るか固定値にする）。
差分の正解は `Source/Hosts/Normal/` にあるので、迷ったら `git diff --no-index Source/Hosts/Cookie/LowCodeApp.Server Source/Hosts/Normal/LowCodeApp.Server` で見比べる。

## 共通の構成

- `LowCodeApp.Server`（デスクトップは `LowCodeApp.Wpf` / `LowCodeApp.WinForms`）: サーバー。`Program.cs` で `SystemConfig` に appsettings を流し込み、
  Controller（`ModuleDataController` 等）が API を出す。DB は `Codeer.LowCode.Blazor.DbAccess`
  - `Services/CustomizedModuleDataIO.cs` — 保存・更新・一括 INSERT のフック（パスワードハッシュはここ）
  - `Services/FileStorageTable.cs` — appsettings の保存先設定 → `IFileStorage`（FileSystem / Azure Blob / S3）
  - `Services/MailSenderTable.cs` — 送信インフラ名 → `IMailSender`（Smtp / GraphApi / Gmail）
  - `Services/DataService.cs` — 1 リクエスト分の DB アクセス・現在ユーザー（`IAuthenticationContext`）
  - `Controllers/TestAPIController.cs` — 独自 Web API を足すときの雛形
- `LowCodeApp.Client`: Blazor WebAssembly。`Pages/LowCodePage.razor` がローコードページのホスト
- `LowCodeApp.Client.Shared`: クライアント共通サービス（`AppInfoService`/`ModuleDataService`/`NavigationServiceBase`）。
  Web・デスクトップ・モバイルで共有。**独自フィールド型・ProCode コンポーネント・スクリプト用サービスはここに置く**
  （Server / Client / Designer の 3 プロセスすべてから参照されているため）。`Samples/` に ProCode とコードビハインドの実例
- `LowCodeApp.Designer`: ビジュアルデザイナ（WPF）。デザインファイル（JSON/SQL/C# スクリプト）を編集する。
  `App.xaml.cs` が拡張の登録場所（テンプレート・ツールメニュー・スクリプト型・AI チャット）。
  exe は `Source/Hosts/Common/LowCodeApp.Designer/bin/Debug/net8.0-windows/LowCodeApp.Designer.exe`。
  GUI を出さない CLI（`designcheck` / `sql` / `rename-*` / `template-create` / `deploy` / `api` / `claude-workspace` …）を持つ。
  **GUI サブシステムの exe なので PowerShell からは `Start-Process -Wait` で呼び、結果は `--out` の JSON で受ける**
- `LowCodeApp.Maui`（Maui のみ）: MAUI クライアント。`README.md` に接続先・証明書・起動手順

## ビルドと起動（Visual Studio が無くてもよい）

```powershell
dotnet build Source/Hosts/Cookie/LowCodeApp.sln                                   # 初回は NuGet 復元で数分
dotnet run --project Source/Hosts/Cookie/LowCodeApp.Server --launch-profile https  # https://localhost:7137
Source/Hosts/Common/LowCodeApp.Designer/bin/Debug/net8.0-windows/LowCodeApp.Designer.exe DesignProjects/<デザイン名>/design   # 引数のフォルダのプロジェクトを開いて起動
```

- Visual Studio 2026: `LowCodeApp.sln` を開き `LowCodeApp.Server` を F5。デザイナは `LowCodeApp.Designer` を「新しいインスタンスを開始」
- VS Code: ルートの `.vscode/launch.json` に **Server** / **Designer** 構成がある（Cookie と `DesignProjects/PatternShowcase/design`。別のデザインを使うならフォルダを置換する）。拡張は C# Dev Kit
- **サーバーは Claude Code が勝手に再起動しない**（ユーザーが VS / VS Code で起動していることが多い）。再起動が必要なときはその旨を伝える
- C# を変えたら Server / Designer の再ビルドと再起動が必要。デザインの変更はデプロイだけで反映（スクリプト変更はサーバー再起動）

## appsettings リファレンス（`appsettings.json` / `appsettings.Development.json`）

`Program.cs` が読んで `Services/SystemConfig.cs` に入れる。**接続文字列・鍵など秘密情報は `appsettings.Development.json`（gitignore 対象）か環境変数**。
`appsettings.json` には名前と種別だけ書く。

| キー | 型 | 意味 |
|---|---|---|
| `ConnectionStrings:<Name>` | string | `DataSources[].Name` と同名で接続文字列。SQLite なら `Data Source=<パス>;`、PostgreSQL なら `Host=...;Username=...;Password=...;Database=...` 等（Npgsql / SqlClient / MySqlConnector / Oracle の書式） |
| `DataSources[]` | `{ Name, DataSourceType }` | データソース。`DataSourceType` は `SQLite` / `PostgreSQL` / `SQLServer` / `MySQL` / `Oracle`。デザインプロジェクトの `designer.settings.json` の `DataSources[].Name` と一致させる。テンプレの既定には標準テンプレート 6 種のデータソース名（`SampleSQLite` / `PatternsSQLite` / `Inventory` / `Sfa` / `ProjectManagement`）が入っている |
| `DesignFileDirectory` | string | デザイン（`App.zip`）を読むフォルダ。デザイナのデプロイ先と同じにする |
| `FontFileDirectory` | string | PDF 出力用フォント（`NotoSansJP.ttf` 等）。PDF を使わないなら空フォルダでよい |
| `FileSystemStorages[]` | `{ Name, Directory }` | FileField の保存先（ローカルフォルダ）。`designer.settings.json` の `FileStorageNames` と `Name` を一致させる |
| `AzureBlobStorages[]` / `S3Storages[]` / `FileStorages[]` | | 保存先の他方式。項目は `api --type Codeer.LowCode.Blazor.Extras.Server.FileManagement.AzureBlobStorageSettings --assembly <Server の bin にある Codeer.LowCode.Blazor.Extras.Server.dll のパス>` で確認 |
| `TemporaryFileTableInfo[]` | `{ DataSourceName, Table, GuidColumn, CreatedDateTimeColumn }` | アップロード一時ファイルの管理テーブル（データソースごと） |
| `UseHotReload` | bool | デザイン変更を SignalR でブラウザに即反映（開発時 true） |
| `CanScriptDebug` | bool | ブラウザでスクリプトのデバッグ情報を出す（開発時 true） |
| `SqlLog` | bool | 実行 SQL をログに出す |
| `IsLicenseAutoUpdate` | bool | ライセンスの自動更新。`DomainLicense`（string）はドメイン単位ライセンスのキー。**トライアルは何も設定しなくても動く** |
| `Mail` | `{ DefaultInfraName, DefaultBulkInfraName, HistoryModuleName }` | メール送信の既定インフラ名（`MailSenderTable` のキー）と送信履歴モジュール |
| `Smtp` / `GraphApi` / `Gmail` | | 送信インフラごとの設定。項目は `api --type Codeer.LowCode.Blazor.Extras.Server.Mail.SmtpSettings --assembly <同上の dll パス>` 等で確認 |
| `AISettings` | `{ OpenAIEndPoint, OpenAIKey, ChatModel, DocumentAnalysisEndPoint, DocumentAnalysisKey }` | AI 文書解析（Azure OpenAI / Document Intelligence）。未使用なら空 |
| `PasswordCheckUserTableInfo`（Cookie） | `{ TableName, IdColumn, UserNameColumn, HashColumn, SaltColumn }` | ログイン検証に使うユーザーテーブルの列。テンプレ既定は `app_users`。初回起動時にユーザーが 0 件なら `admin`/`admin` を作る |
| `Logging` / `AllowedHosts` | | ASP.NET Core 標準 |

**DB を SQLite から PostgreSQL 等に変える手順**: ① `appsettings.json` の `DataSources[].DataSourceType` を変える ② `appsettings.Development.json` の接続文字列を差し替える
③ デザインプロジェクト側 `DesignProjects/<デザイン名>/design/designer.settings.json` の `DataSourceType` と `designer.settings.Development.json` の接続文字列も同じに ④ テーブルはデザイナの
Tools メニュー（DDL 生成）か CCFD の `sql` CLI で作る。DB プロバイダは `Codeer.LowCode.Blazor.DbAccess` に同梱済みで追加パッケージは不要。

## デザインプロジェクトの切り替え（サンプルから自分のアプリへ）

ホストが配信するデザインは 1 つ（`DesignFileDirectory` の `App.zip`）。デザインプロジェクトは `DesignProjects/` にいくつ置いてもよいが、
サーバーが読むのは最後にデプロイしたもの。新しいデザインプロジェクトを作って切り替える手順（DESIGNER_EXE = `Source/Hosts/Common/LowCodeApp.Designer/bin/Debug/net8.0-windows/LowCodeApp.Designer.exe`。GUI 系 exe なので `Start-Process -Wait` + `--out`）:

1. 作る: `template-create --name Empty --out-dir DesignProjects/<アプリ名>/design --data-dir Local/Data --deploy-dir Local/Designs --out Local/tc.json`
   （テンプレートはすべて Cookie 認証向けで AppUser と admin/admin を含む。`template-list` で一覧。テンプレート付属の SQLite が `Local/Data` に置かれ、`design/designer.settings.Development.json` の接続文字列がそこを指し、`Local/Designs/App.zip` が**この新しいデザインで上書き**される）
2. ワークスペースを展開: `claude-workspace DesignProjects/<アプリ名> --project design --out Local/cw.json`（`CLAUDE.md` / `ClaudeCodeForDesigner/` / `Project.md` / `ddl/` / `docs/` ができる）
3. サーバーの `appsettings.Development.json` の `ConnectionStrings` を確認する。標準テンプレートのデータソース名（`SampleSQLite` / `PatternsSQLite` / `Inventory` / `Sfa` / `ProjectManagement`）と DB ファイル名は既に入っているので、標準テンプレートから作ったデザインなら変更不要。自前のデータソース名や DB にしたときだけ、`DataSources[]`（`appsettings.json`）と接続文字列を足す。サーバーを再起動
4. 以後のデザイン作業は `DesignProjects/<アプリ名>/` で Claude Code を起動して行う。`.vscode/launch.json` の Designer 構成のフォルダも差し替える

元のサンプルに戻すときは、そのデザインを `deploy "DesignProjects/<テンプレート名>/design"` し直し、接続文字列を戻す。サンプルのデザインプロジェクトが不要なら `DesignProjects/<テンプレート名>/` ごと削除してよい（`Local/Data` の DB ファイルはそのままでも害はない）。

## ホストのカスタマイズ（C#・最後の手段）

やり方の詳細と正確な API は `ClaudeCodeForDeveloper/_specs/HostCustomization.md`（デザイナの `developer-workspace` が生成。ライブラリ側の拡張点。無ければ `Start-Process -Wait` で `LowCodeApp.Designer.exe developer-workspace <ROOT> --out <json>` を実行して生成する）。
シグネチャは推測せず `LowCodeApp.Designer.exe api --type <FullName> --out <md>` で確認する（`Start-Process -Wait` で呼ぶ）。デザイナに載っていないサーバー側ライブラリ（`Codeer.LowCode.Blazor.Extras.Server` / `Codeer.LowCode.Blazor.DbAccess`）は `--assembly` に Server の `bin/Debug/net8.0/` にある dll のパスを渡す。

| やりたいこと | 場所 |
|---|---|
| 独自フィールド型 | `LowCodeApp.Client.Shared` に 4 ファイル（`XxxFieldDesign` / `XxxFieldData` / `XxxField` / `XxxFieldComponent.razor`）。登録不要（リフレクション検出）。デザイナのプレビューに CSS を出すには `App.xaml.cs` の `BlazorRuntime.InstallBundleCss("LowCodeApp.Client.Shared")`（既に呼んでいる） |
| 画面の一部を Blazor で書く / スクリプトの代わりに C# | `Client.Shared/Samples/ProCodeComponentSample.razor`（ProCodeField）/ `CodeBehindSample.cs`（コードビハインド。クラス名＝モジュール名）を写す |
| スクリプトから呼べるサービス・型 | `Client.Shared/Services/AppInfoService.cs` と `Designer/App.xaml.cs` の両方で `ScriptRuntimeTypeManager.AddService / AddType` |
| 保存・更新時の処理 | `Server/Services/CustomizedModuleDataIO.cs` の override |
| 独自 Web API | `Server/Controllers/` に Controller を追加（`TestAPIController` が雛形）。スクリプトからは Extras の `WebApiService` で呼ぶ（`_script_catalog.md` に載る） |
| ファイル保存先・メール送信の独自実装 | `IFileStorage` → `FileStorageTable`、`IMailSender` → `MailSenderTable` に 1 行 |
| デザイナのメニュー・チェック・テンプレート | `Designer/App.xaml.cs`（`DesignerEnvironment.AddMainMenu` / `AddCustomDesignCheckHandler` / `ProjectCatalog.Add`）。テンプレート・headless verb の登録は `base.OnStartup(e)` より前 |

## Claude Code が作業するときの原則

- **テンプレのソースは自由に書き換えてよい。** 接着コードであり、ユーザーの所有物。守るべき境界は「NuGet パッケージの中」だけ
- **ライブラリで済むことを自前実装しない。** メール・承認・Excel/PDF・ファイル保存（S3/Azure）・AI 連携などは
  `Codeer.LowCode.Blazor.Extras` / `Extras.Server` にある。まず該当パッケージの API を探す（`api --assembly Codeer.LowCode.Blazor.Extras` / `api --assembly <Server の bin の Codeer.LowCode.Blazor.Extras.Server.dll>`）
- **秘密情報を書かない。** 接続文字列・API キーは `appsettings.Development.json` か環境変数。コミットに含めない。
  `appsettings.Development.json` / `DesignProjects/*/design/designer.settings.Development.json` の中身は、セットアップ手順で必要なとき以外は読まない
- **ビルドが通る状態で止める。** C# を触ったら `dotnet build Source/Hosts/<Variant>/LowCodeApp.sln` を最後に実行する
- リネームは `LowCodeApp` の一括置換（ファイル名・フォルダ名・中身）。`x:Class`、`*.styles.css` のリンクも対象

## 初回セットアップの手順（手動で行う場合の要約。Claude Code は ClaudeCodeForDeveloper/claude-code-setup.md）

1. サーバープロジェクトの `appsettings.Development.json`: 接続文字列、`DesignFileDirectory`、`FileSystemStorages`。
   既定は `C:\Codeer.LowCode.Blazor.Local\...` を指す
2. `LowCodeApp.Designer` を起動してデザインプロジェクトを作る（テンプレートから選べる）か、
   `LowCodeApp.Designer.exe template-create --name <テンプレ> --out-dir DesignProjects\<デザイン名>\design --data-dir <Local\Data> --deploy-dir <DesignFileDirectory>`
3. サーバーを起動。`Source/Hosts/Cookie/` は初回起動時に `admin`/`admin` のユーザーが自動作成される
4. `Source/Hosts/Maui/` はサーバーを先に起動してからアプリを起動（`LowCodeApp.Maui/README.md`）

## このリポジトリの性質（保守者向け）

保守の手順（sln の再生成・VSIX・本体リポへのデバッグ用コピー）は [MAINTAINERS.md](MAINTAINERS.md)。要点だけ:
**このリポジトリがテンプレートの正本。** プロジェクトはそれぞれ 1 か所にだけある：`Source/Hosts/Common/*`（全バリアント共通）と
`Source/Hosts/<Variant>/LowCodeApp.<Own>`。各バリアントの `LowCodeApp.sln` は `Source/Tools/StarterTool assemble` の生成物で手で編集しない。
タグ＝フレームワークのバージョン。`git diff v1.3.20 v1.3.23 -- Source/Hosts/Cookie/` で追従差分が見える。
