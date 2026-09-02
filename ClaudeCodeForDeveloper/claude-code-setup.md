# Claude Code 向け: Codeer.LowCode.Blazor を動かすまでの実行手順

**この文書は Claude Code（AI）が読んで実行するための手順書です。**（利用者向けの注意: この手順は AI が実行します。
AI の動作は決定的ではなく、ソフトウェアのインストール・ファイルの変更・コマンド実行を伴います。内容を確認のうえ自己責任でご利用ください。
本リポジトリは LICENSE のとおり AS IS で提供され、AI の操作に起因する損害について Codeer は責任を負いません。） ユーザーが「この URL を見て指示に従って」と言って
渡してきたら、上から順に実行してください。人間向けの説明は [README.md](../README.md) にあります。

ゴール: ユーザーの作業フォルダに、Codeer.LowCode.Blazor のアプリ（サーバー / クライアント / デザイナ）がビルド済みで、
サンプルの画面が入ったデザインプロジェクトができていて、**サーバーがブラウザで開き、デザイナも起動している**状態にする。
そのあと Claude Code が画面（デザイン）を追加・変更できるようにする（デザインの作業場は `<ROOT>\DesignProjects\<TEMPLATE>\`、ホストの C# の作業場は ROOT）。

## 進め方の原則

- **一本道で進める。迷ったらこの文書の既定値を採用する。** ユーザーに聞くのは下の「聞いてよいこと」の 1 つだけ。
  それ以外（フォルダ名・テンプレート・ポート・DB の種類）は聞かずに既定値で進める
- **難しいことを聞かない。** 「NuGet のソースをどうしますか」「証明書を信頼しますか」のような質問はしない。この文書に書いてある通りに処理する
- **失敗したら止まらず、この文書の「うまくいかないとき」を見て自分でリカバリする。** それでも無理なときだけ、ユーザーに「何を」「どう」してほしいか 1 行で頼む
- **各ステップの終わりに 1 行で進捗を報告する**（「.NET SDK 8 を確認しました」など）。長い説明はしない
- ユーザーへの応答はユーザーの言語で（日本語で話しかけられたら日本語）
- **Windows 専用。** macOS / Linux では「デザイナが Windows 専用のため、このセットアップは Windows で行ってください」と伝えて終了する
- 作業フォルダの外（`C:\` 直下や他のプロジェクト）には何も作らない

### 聞いてよいこと（この 1 つだけ・既定値を提示して Yes/No で答えられる形で）

1. 不足しているソフト（.NET SDK / Git / VS Code）を **winget でインストールしてよいか**（不足しているものがあるときだけ聞く）

アプリはログイン画面のある `Cookie` ホスト 1 種類（認証なしの構成は作らない。前段で認証済みなどの事情で不要なら、セットアップ後に `CLAUDE.md` の「認証を外す」で外す）。

---

## Step 0. 前提の確認

PowerShell で確認する。すべて非対話で判定できる。

| 確認 | コマンド | 判定 |
|---|---|---|
| OS | `[System.Environment]::OSVersion.Platform` | `Win32NT` 以外なら終了（上記） |
| .NET SDK 8 | `dotnet --list-sdks` | `8.` で始まる行があれば OK |
| Git | `git --version` | 無くてもよい（無ければ zip で取得する） |
| Visual Studio 2026 | `& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationVersion` | `18.` 以上なら「VS あり」。vswhere 自体が無い／エラーなら「VS なし」 |
| VS Code | `code --version` | VS なしのときだけ気にする |
| Claude Code の作業フォルダ | `Get-ChildItem -Force` | **空でなければ** サブフォルダ `LowCodeApp` を作ってそこを作業フォルダにする |

以降、作業フォルダの絶対パスを **ROOT** と呼ぶ（例: `C:\Users\taro\work\LowCodeApp`）。パスに日本語や空白が入っていても動くが、
すべてのコマンドでパスは `"` で囲む。

## Step 1. 不足しているものを入れる

不足があれば、ここで **1 回だけ**まとめて聞く: 「.NET SDK 8 / Git / VS Code を winget でインストールしてよいですか？」
（不足しているものだけ列挙する。ユーザーが No なら、何を手でインストールすればよいかを 1 行ずつ伝えて、入ったら続きをやると言って待つ）

```powershell
winget install --id Microsoft.DotNet.SDK.8 -e --accept-source-agreements --accept-package-agreements
winget install --id Git.Git -e --accept-source-agreements --accept-package-agreements          # Git は任意。無ければ入れなくてよい
winget install --id Microsoft.VisualStudioCode -e --accept-source-agreements --accept-package-agreements   # VS が無いときだけ
```

- インストール後は **PATH がこのシェルに反映されない**ことがある。`dotnet` が見つからなければ新しい PowerShell を起こす代わりに
  `$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")` を実行してから再確認する
- **winget が無い環境**（`winget` コマンド自体が見つからない）では .NET SDK を公式スクリプトで入れる（管理者権限不要）:
  ```powershell
  Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile "$env:TEMP\dotnet-install.ps1"
  & "$env:TEMP\dotnet-install.ps1" -Channel 8.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"
  [System.Environment]::SetEnvironmentVariable("Path", $env:Path + ";$env:LOCALAPPDATA\Microsoft\dotnet", "User")
  [System.Environment]::SetEnvironmentVariable("DOTNET_ROOT", "$env:LOCALAPPDATA\Microsoft\dotnet", "User")
  $env:Path += ";$env:LOCALAPPDATA\Microsoft\dotnet"; $env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
  ```
  VS Code も winget 無しなら、ユーザーに https://code.visualstudio.com/ からのインストールを頼む（VS があるなら不要）

## Step 2. このリポジトリを ROOT に取得する

Git があるとき（ROOT が空であること）:

```powershell
git clone https://github.com/Codeer-Software/Codeer.LowCode.Blazor.Starter.git "<ROOT>"
```

Git が無いとき（zip を展開して中身を ROOT 直下に移す）:

```powershell
Invoke-WebRequest https://github.com/Codeer-Software/Codeer.LowCode.Blazor.Starter/archive/refs/heads/main.zip -OutFile "$env:TEMP\starter.zip"
Expand-Archive "$env:TEMP\starter.zip" -DestinationPath "$env:TEMP\starter" -Force
Move-Item "$env:TEMP\starter\Codeer.LowCode.Blazor.Starter-main\*" "<ROOT>" -Force
```

取得後、`<ROOT>\Source\Hosts\Cookie\LowCodeApp.sln` と `<ROOT>\CLAUDE.md` があることを確認する。
**このフォルダが以後の作業場所。** Claude Code の知識源は 3 つ: ROOT の `CLAUDE.md`（ホスト側の説明）、`ClaudeCodeForDeveloper/`（この手順書と、Step 5 で
生成するホスト開発のリファレンス）、Step 7 で展開される `DesignProjects\<TEMPLATE>\ClaudeCodeForDesigner/`（デザインの作り方）。

> ユーザー自身のリポジトリにしたい場合は `.git` を削除して `git init` すればよい（聞かれたときだけ案内する。聞かない）。

## Step 3. 使う値を固定する（質問はしない）

| 名前 | 値 |
|---|---|
| `<VARIANT>` | `Cookie`（ログイン画面のある Web アプリ。公開しているホストはこれと、その Android/iOS クライアントの `Maui` だけ） |
| `<TEMPLATE>` | `PatternShowcase`（標準パターン集。ログイン `admin` / `admin`。alice / bob / carol / dave はパスワード `test`） |
| `<URL>` | `https://localhost:7137` |

ソリューションは `<ROOT>\Source\Hosts\<VARIANT>\LowCodeApp.sln`。`Source\Hosts\` には保守用に他のホスト（`Normal` / `Wpf` / `WinForms` / `MultiTenant`）も入っているが、この手順では使わない。

## Step 4. ローカルの置き場を作り、appsettings を書き換える

サーバーの設定 `<ROOT>\Source\Hosts\<VARIANT>\LowCodeApp.Server\appsettings.Development.json` は、パスが
`C:\Codeer.LowCode.Blazor.Local\...` という固定パスになっている。これを **ROOT 配下**に向ける。

1. フォルダを作る: `<ROOT>\Local\Data`、`<ROOT>\Local\Designs`、`<ROOT>\Local\Storages`、`<ROOT>\Local\Font`
2. `appsettings.Development.json` を編集する（JSON として読んで書き戻す。`\` は JSON 内で `\\`）:
   - `ConnectionStrings` の各値: `Data Source=C:\\Codeer.LowCode.Blazor.Local\\Data\\<ファイル名>` → `Data Source=<ROOT>\\Local\\Data\\<ファイル名>`（ファイル名はそのまま）
   - `FileSystemStorages[*].Directory` → `<ROOT>\\Local\\Storages`
   - `DesignFileDirectory` → `<ROOT>\\Local\\Designs`
   - `FontFileDirectory` → `<ROOT>\\Local\\Font`
   - 他の項目は触らない

要するに、文字列 `C:\\Codeer.LowCode.Blazor.Local` を `<ROOT>\\Local` に置換する（`DesignFileDirectory` は全ホストで `...\\Designs`）。
`ConnectionStrings` には全テンプレート分（`SampleSQLite` / `PatternsSQLite` / `Inventory` / `Sfa` / `ProjectManagement`）が入っているので、どのテンプレートで
デザインプロジェクトを作ってもサーバー側の追加設定は要らない（`template-create --data-dir` が置く DB ファイル名と一致している）。
DB は SQLite（ファイル）なので DB サーバーのインストールは不要。PostgreSQL 等に変えたいという話が出たら、
セットアップ完了後に `CLAUDE.md` の「appsettings リファレンス」を見て対応する（今はやらない）。

## Step 5. ビルド

```powershell
dotnet build "<ROOT>\Source\Hosts\<VARIANT>\LowCodeApp.sln"
```

- 初回は NuGet の復元で数分かかる。`Build succeeded` と `0 Error(s)` を確認する
- 警告（`warning`）は無視してよい
- 成功すると、デザイナの exe が `<ROOT>\Source\Hosts\Common\LowCodeApp.Designer\bin\Debug\net8.0-windows\LowCodeApp.Designer.exe` にできる。以降 **DESIGNER_EXE** と呼ぶ

続けて、ホスト（C#）を触るときに Claude Code が読むリファレンスを生成する。デザイナ exe には GUI を出さない CLI（headless）があり、
**GUI サブシステムの exe なので PowerShell の `&` では待たない。必ず `Start-Process -Wait` で呼び、結果は `--out` の JSON ファイルで受け取る**（以降の Step も同じ）:

```powershell
Start-Process -FilePath "<DESIGNER_EXE>" -Wait -ArgumentList @(
  "developer-workspace", "<ROOT>", "--out", "<ROOT>\Local\developer-workspace.json")
Get-Content "<ROOT>\Local\developer-workspace.json"
```

`<ROOT>\ClaudeCodeForDeveloper\_specs\HostCustomization.md` などができる（`_` で始まるものは生成物・gitignore 済み。この手順書などコミット済みの文書には触らない）。
同時に `<ROOT>\.claude\settings.local.json`（デザイナ exe のパスを焼き込んだ許可リストと、`DesignProjects\*\` の各ワークスペースを最新化するフック。既存なら触らない）ができる。
パッケージを更新したときは同じコマンドで作り直す。判定: JSON に `"error"` が無く `written` に `HostCustomization.md` が含まれる。

> `.claude/settings.local.json` は **Claude Code が次のセッションから読む**。今のセッションでは反映されないので、完了メッセージで
> 「一度 Claude Code を再起動（`/exit` して再度 `claude`）すると、デザイナのコマンドが許可済みになります」と伝える。

## Step 6. デザインプロジェクトをテンプレートから作る

デザインプロジェクトは **`<ROOT>\DesignProjects\<名前>\`** に 1 つずつ置く。`design\` がデザイン本体（デザイナが開く・デプロイされる範囲）で、
隣に `Project.md` / `ddl\` / `docs\`（Step 7 で生成）が並ぶ。ここではテンプレート名をフォルダ名にする（`DesignProjects\<TEMPLATE>\`）。
これは**サンプル（ショーケース）**であることを名前で示すためで、ユーザー自身の業務アプリはあとで別のデザインプロジェクトとして作る（`CLAUDE.md` の「デザインプロジェクトの切り替え」）。

```powershell
Start-Process -FilePath "<DESIGNER_EXE>" -Wait -ArgumentList @(
  "template-create", "--name", "<TEMPLATE>",
  "--out-dir", "<ROOT>\DesignProjects\<TEMPLATE>\design",
  "--data-dir", "<ROOT>\Local\Data",
  "--deploy-dir", "<ROOT>\Local\Designs",
  "--out", "<ROOT>\Local\template-create.json")
Get-Content "<ROOT>\Local\template-create.json"
```

これ 1 回で次が済む: `<ROOT>\DesignProjects\<TEMPLATE>\design` にデザインプロジェクト（`app.clprj` / `Modules` / `PageFrames` …）が展開され、
サンプル DB（SQLite）が `<ROOT>\Local\Data` に置かれ、`design\designer.settings.Development.json` の接続文字列がそのパスに
書き換わり、デプロイ先が `<ROOT>\Local\Designs` に設定されて **`App.zip` がそこに出力**される（サーバーはこの zip を読む）。

判定: JSON に `"error"` が無く、`deploy.success` が `true`。`<ROOT>\Local\Designs\App.zip` が存在する。

## Step 7. Claude Code 用ワークスペースを展開する（デザインを AI で編集できるようにする）

ワークスペース＝デザインプロジェクト 1 つ分のフォルダ `<ROOT>\DesignProjects\<TEMPLATE>`。その中の `design` がデザイン本体:

```powershell
Start-Process -FilePath "<DESIGNER_EXE>" -Wait -ArgumentList @(
  "claude-workspace", "<ROOT>\DesignProjects\<TEMPLATE>", "--project", "design", "--out", "<ROOT>\Local\claude-workspace.json")
Get-Content "<ROOT>\Local\claude-workspace.json"
```

`<ROOT>\DesignProjects\<TEMPLATE>\` に `CLAUDE.md`（デザイン作業の規約）、`ClaudeCodeForDesigner/`（デザインの作り方・仕様・カタログ。**自動生成、手で編集しない**）、
`Project.md`（このデザイン固有のメモ）、`ddl/`、`docs/`、`LocalEnvironment.md`、`.claude/settings.local.json`（デザイナ exe のパスを焼き込んだ許可リストとフック）ができる。
ROOT の `CLAUDE.md`（このリポジトリのもの）には触らない。

判定: JSON の `aiRefresh` が `ok`。`<ROOT>\DesignProjects\<TEMPLATE>\ClaudeCodeForDesigner\CLAUDE.md` と `_field_catalog.md` が存在する。

> デザインだけを扱う人は **`<ROOT>\DesignProjects\<TEMPLATE>` で Claude Code を起動**する（ホストのソースが視界に入らない）。ROOT で起動した Claude Code は
> ホスト（C#）とデザインの両方を扱える（Step 5 の `settings.local.json` に同じ許可とフックがある）。完了メッセージでこの使い分けを伝える。

## Step 8. HTTPS 開発証明書

```powershell
dotnet dev-certs https --check --trust
```

`A valid certificate was found` と `trusted` が出れば OK。信頼されていなければ `dotnet dev-certs https --trust` を実行する。
**Windows がセキュリティ警告のダイアログを出す**ので、ユーザーに「証明書のダイアログが出たら『はい』を押してください」と伝える。
押されなくても http（`<URL>` の代わりに `http://localhost:5085`）で動くので、そこで詰まらない。

## Step 9. サーバーとデザイナを起動する

サーバー（別プロセス・バックグラウンド。ブラウザは `launchSettings.json` の設定で自動で開く）:

```powershell
Start-Process -FilePath "dotnet" -WorkingDirectory "<ROOT>\Source\Hosts\<VARIANT>\LowCodeApp.Server" -ArgumentList @(
  "run", "--project", "<ROOT>\Source\Hosts\<VARIANT>\LowCodeApp.Server", "--launch-profile", "https", "--no-build")
```

デザイナ（デザインプロジェクトのフォルダを引数に渡すと、そのプロジェクトを開いた状態で起動する）:

```powershell
Start-Process -FilePath "<DESIGNER_EXE>" -ArgumentList @("<ROOT>\DesignProjects\<TEMPLATE>\design")
```

起動確認: 20〜60 秒待ってから `Invoke-WebRequest <URL> -UseBasicParsing -SkipCertificateCheck` が 200 を返せば OK
（PowerShell 5.1 には `-SkipCertificateCheck` が無い。その場合は `http://localhost:<httpポート>` で確認する）。
ブラウザが開かなければ `Start-Process "<URL>"` で開く。

- ログイン画面が出る。`admin` / `admin`
- デザイナ右上に「トライアル」と表示されるのは正常（ライセンス登録は不要。試用できる）

**起動がうまくいかないときはユーザーにやってもらってよい**: 「Visual Studio で `Source\Hosts\<VARIANT>\LowCodeApp.sln` を開き、
`LowCodeApp.Server` を F5 で起動してください（デザイナは `LowCodeApp.Designer` を右クリック > デバッグ > 新しいインスタンスを開始）」。
VS が無いときは VS Code（次項）。

## Step 10. VS 2026 が無いとき: VS Code で開ける状態にする

ROOT に `.vscode/`（`launch.json` / `tasks.json` / `extensions.json`）が同梱されている（このリポジトリの一部）。
`launch.json` の既定は Cookie + `DesignProjects/PatternShowcase/design`（この手順の値と同じ。そのまま使える）。

1. 拡張機能: `code --install-extension ms-dotnettools.csdevkit`（VS Code があるとき。無ければ `extensions.json` の推奨が VS Code 側で提示される）
2. 開く: `code "<ROOT>"`
3. 使い方をユーザーに伝える: 「実行とデバッグ」ビューで **Server** を選んで F5 でサーバー、**Designer** でデザイナが起動する。
   `Ctrl+Shift+B` でビルド

## Step 11. 完了メッセージ

以下を短く伝える（この文書の URL や内部手順は説明しない）:

- アプリの URL（`<URL>`）とログイン情報（`admin` / `admin`）
- デザインプロジェクトの場所（`<ROOT>\DesignProjects\<TEMPLATE>\design`）と、デザイナで編集 → 「送信」でサーバーに反映されること（サーバーの再起動は不要。スクリプトを変えたときだけ再起動）
- **次にできること**: 「Claude Code を再起動すると、画面の追加や変更を私（Claude）に頼めます。例:『商品マスタの画面を追加して』。デザインだけを扱うなら `DesignProjects\<TEMPLATE>` フォルダで起動するとホストのソースが視界に入らず身軽です」
- 今入っているのは**サンプル集**であること。**自分の業務アプリを作るときは、サンプルに増築せず、空のプロジェクトから別のデザインプロジェクト（`DesignProjects\<アプリ名>`）を作るのが既定**で、Claude に「自分のアプリを作りたい」と言えば確認しながら進めること
- ソースコード（C#）も同じフォルダ（ROOT）にあり、必要なら ROOT で Claude Code を起動して変更できるが、**ふつうの画面追加はデザインだけで済む**こと

---

## うまくいかないとき

| 症状 | 対処 |
|---|---|
| `dotnet build` で `NU1101` / `Unable to find package Codeer.LowCode.Blazor...` | nuget.org に到達できていない。プロキシ環境なら `$env:HTTPS_PROXY` を確認。社内 NuGet フィードしか使えない環境はユーザーに nuget.org のミラーがあるか聞く |
| `dotnet build` で `NETSDK1045`（必要な SDK が無い） | .NET SDK 8 が入っていない、または `global.json` の版とずれている。Step 1 をやり直す |
| ビルドは通るが `LowCodeApp.Designer.exe` が無い | `Source\Hosts\Common\LowCodeApp.Designer\bin\Debug\net8.0-windows\` を再確認。無ければ `dotnet build "<ROOT>\Source\Hosts\Common\LowCodeApp.Designer\LowCodeApp.Designer.csproj"` |
| `template-create` の JSON に `template not found` | `Start-Process "<DESIGNER_EXE>" -Wait -ArgumentList @("template-list","--out","<ROOT>\Local\tl.json")` で一覧を出し `folderName` を確認する |
| `template-create` の JSON に `--out-dir must be empty` | `<ROOT>\DesignProjects\<TEMPLATE>\design` に既にファイルがある。中身を確認し、ユーザーの物でなければ削除して再実行 |
| `template-create` や `claude-workspace` を実行するとデザイナの **ウィンドウが開いて** JSON ができない | デザイナのパッケージが古い（`template-create` は Codeer.LowCode.Blazor.Designer 1.3.24 以降）。`Source\Hosts\Common\LowCodeApp.Designer\LowCodeApp.Designer.csproj` の `Codeer.LowCode.Blazor.Designer` の版を確認する。このリポジトリを最新から取得していれば起きない |
| サーバー起動で `address already in use` | ポートが使用中。`launchSettings.json` の `applicationUrl` のポート番号を空いている番号に変えて再起動し、完了メッセージの URL も合わせる |
| ブラウザで開くと真っ白／`design not found` | `<ROOT>\Local\Designs\App.zip` が無い。Step 6 のデプロイが失敗している。`Start-Process "<DESIGNER_EXE>" -Wait -ArgumentList @("deploy","<ROOT>\DesignProjects\<TEMPLATE>\design","--out","<ROOT>\Local\deploy.json")` で作り直す |
| `developer-workspace` / `claude-workspace` で `--out` の JSON ができない（ウィンドウが開く） | デザイナのパッケージが古い（`developer-workspace` は Codeer.LowCode.Blazor.Designer.Standard 0.8.3 以降）。このリポジトリを最新から取得していれば起きない |
| ログインできない（Cookie） | サンプル DB に `admin` がいるはず。`appsettings.Development.json` の `ConnectionStrings` が `<ROOT>\Local\Data\...` を指しているか、ファイルが存在するかを確認 |
| `winget` が見つからない | Step 1 の dotnet-install.ps1 経路 |
| PowerShell で `&` でデザイナ exe を呼ぶと即戻って何も起きない | 正常（GUI サブシステム）。必ず `Start-Process -Wait` + `--out` |
| Excel / PDF 出力でフォントのエラー | `<ROOT>\Local\Font` に `NotoSansJP.ttf` を置く（PDF 出力を使うときだけ必要） |

## この手順が前提にしているもの（保守メモ）

- Codeer.LowCode.Blazor.Designer **1.3.24 以降**（`template-create` / `deploy` / `api` サブコマンド、起動引数でのプロジェクトオープン）
- Codeer.LowCode.Blazor.Designer.Standard **0.8.5 以降**（全テンプレートが Cookie 認証ホスト向け = AppUser + admin/admin 同梱、`template-create --data-dir` によるサンプル DB 配置、`developer-workspace`、デザインプロジェクトのフォルダ名の既定 `design`）
- 各バリアントの `appsettings.Development.json` の既定パスが `C:\Codeer.LowCode.Blazor.Local\...`（Step 4 の置換の前提）
- ポート: `Properties/launchSettings.json` の `https` プロファイル（Cookie 7137）
