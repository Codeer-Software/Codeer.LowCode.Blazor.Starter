# 保守者向け

このリポジトリはアプリケーションテンプレートの正本です。以下はリポジトリを保守する人のためのもので、テンプレートの利用者には不要です。

| パス | 役割 |
|---|---|
| `Source/` | ビルド対象のすべて。リポジトリのルートは文書。`Source/Codeer.LowCode.Blazor.Starter.sln` は全バリアントの全プロジェクトを開く（MAUI ワークロードが必要） |
| `Source/Hosts/` | ホストアプリケーション＝ローコードのデザインプロジェクトを動かす C# ソリューション（デザインプロジェクト本体＝画面・データ・スクリプトはデザイナで作り、このリポジトリには含まれない） |
| `Source/Hosts/Common/` | 全バリアント共通プロジェクトのマスタ（`Client.Shared`、`Designer`、`LicenseRegister`、`LicenseRegisterCli`）。`Font/` は PDF 出力用の Noto Sans JP（SIL OFL 1.1、`OFL.txt` 同梱。`export-app` が `Local/Font` にコピーする。VSIX には含めない） |
| `Source/Hosts/<Variant>/LowCodeApp.<Own>` | バリアント固有プロジェクトのマスタ（`Normal` / `Cookie` のサーバーとクライアント、`Maui`、`Wpf`、`WinForms`）。公開テンプレートは `Cookie`（接尾辞なし・既定）と `Maui` だけ。`Normal` / `Wpf` / `WinForms` / `MultiTenant` は `Variants.cs` で `IsTemplate: false`（ソリューション生成とデバッグ用コピーの対象だが VSIX には入れない） |
| `Source/Hosts/<Variant>/LowCodeApp.sln` | **生成物**。バリアントのプロジェクトと `Source/Hosts/Common/` をその場で参照する |
| `Source/Tools/StarterTool` | `assemble`（ソリューション再生成）、`pack-vsix`（Visual Studio テンプレートの zip）、`export-debug`（フレームワーク本体リポジトリ向けのデバッグ用コピー）、`export-app <出力先> [--maui] [--no-upgrade]`（顧客のアプリフォルダ＝Cookie ホスト 5 プロジェクト + `CLAUDE.md` / `ClaudeCodeForDeveloper/` / `.vscode/` / `.gitignore` / `LICENSE`。文書は `Source/Hosts/Cookie|Common/X` → `Source/X` に書き換え、`<!-- maintainer-only -->` … `<!-- /maintainer-only -->` の間は落とす。**リポジトリは .NET 8 のまま**で、書き出すアプリだけ net10.0 と nuget.org の最新安定版に上げる（Codeer.* は据え置き、Microsoft.* は .NET のメジャーに合わせる、Roslyn は同メジャー、脆弱な推移的パッケージは直接参照でピン）。Claude Code のセットアップ Step 2 が使う） |
| `Source/Tools/Codeer.LowCode.Blazor.Templates` | バリアントをプロジェクトテンプレートとして配布する Visual Studio 拡張機能（VSIX） |
| `ClaudeCodeForDeveloper/` | C# ホストを触る Claude Code セッション向けの文書。`claude-code-setup.md` は、ユーザーがこのリポジトリの URL を渡したときに Claude Code が実行する手順書。ホストが参照するパッケージのバージョンと同期を保つ（前提にする Designer / Designer.Standard の最低バージョンを明記している）。`_` で始まるエントリ（`_specs/`）はデザイナの `developer-workspace` verb が生成するもので gitignore 対象 |
| `DesignProjects/<name>/` | リポジトリには含まれない。セットアップ手順（またはユーザー）が作る。デザインプロジェクト 1 つにつき 1 フォルダ: `design/`（デザイナが開き、デプロイされるデザインプロジェクト本体）、`Project.md`、`ddl/`、`docs/`、そしてデザイナの `claude-workspace` verb がその隣に展開する Claude Code ワークスペース |
| `.vscode/` | VS Code 用の起動・ビルド設定（既定バリアント: Cookie）。リポジトリの一部であり、VSIX テンプレートには含まない |
| `Source/Hosts/Common/LowCodeApp.SeleniumTest` | Web バリアント共通の Selenium テストプロジェクト（`Variants.cs` で `InVsix: false`＝リポジトリの sln には入るが VSIX テンプレートには入れない。顧客がテストするのは自分のデザインプロジェクトで、デザイナの `selenium-test-init` が雛形を出す）。マスタは Codeer.LowCode.Blazor.Designer.Standard リポジトリの `SeleniumTestTemplate/LowCodeApp.SeleniumTest`（デザイナの `selenium-test-init` が展開するもの）。変わったらここへコピーする |

```
dotnet run --project Source/Tools/StarterTool -- assemble
dotnet run --project Source/Tools/StarterTool -- pack-vsix
dotnet run --project Source/Tools/StarterTool -- export-debug <Codeer.LowCode.Blazor/Source>
dotnet run --project Source/Tools/StarterTool -- export-app <出力先> [--maui] [--no-upgrade]
```

プロジェクトを追加・削除したら `assemble` を実行する。プルリクエストはマスタに対して受け付ける。
`export-debug` は Visual Studio を閉じてから実行する（VS が開いていると、一時的に消えた csproj への参照を VS が勝手に削ることがある）。

`pack-vsix` はテンプレートの zip を更新するだけ。拡張機能本体は MSBuild でビルドする（Visual Studio が必要）:

```
msbuild Source/Tools/Codeer.LowCode.Blazor.Templates/Codeer.LowCode.Blazor.Templates.csproj -t:Rebuild -p:Configuration=Release
msbuild Source/Tools/Codeer.LowCode.Blazor.Templates/Codeer.LowCode.Blazor.Templates.csproj -t:BuildSplitVsix -p:Configuration=Release
```

1 行目で結合版 `Codeer.LowCode.Blazor.Templates.vsix`、2 行目で Visual Studio のメジャーバージョン別の
`Codeer.LowCode.Blazor.Templates.VS2022.vsix` と `Codeer.LowCode.Blazor.Templates.VS2026.vsix` が `bin/Release` にできる。
分割版は古い VS Installer（VSIXInstaller 18.3 以前）向けの回避策で、VS 2022 と VS 2026 が両方あるマシンでは、両方を対象にした
1 本の VSIX だと 2 つ目のインストールが "the stream must be seekable" で失敗し、もう一度実行しないと入らなかった。
VSIXInstaller 18.9（VS 2026 18.9 以降）は結合版 1 本で両インスタンスに一度で入るので、最新環境には結合版だけ配ればよい。

## リリースチェックリスト

1. パッケージが nuget.org に公開されたら、ホストの `.csproj` の `Codeer.LowCode.Blazor*` パッケージのバージョンを上げる（リポジトリのホストは .NET 8 のまま。顧客向けの .NET 10 化は export-app が行う）。
2. 全バリアントのソリューションを `dotnet build` する。
3. クリーンな Windows（Windows Sandbox でよい）の空フォルダと Claude Code で `ClaudeCodeForDeveloper/claude-code-setup.md` を通す。
4. `pack-vsix`、VSIX のビルド、フレームワーク本体リポジトリへの `export-debug`。
5. フレームワークのバージョンでタグを付ける（`v1.3.24` など）。
