# Codeer.LowCode.Blazor.Starter - Claude Code 向けガイド

このリポジトリは Codeer.LowCode.Blazor（Blazor 向けローコードフレームワーク、NuGet 配布の商用製品）で
アプリケーションを始めるための**ビルド可能な完成形**の集まり。`Source/Hosts/` 配下のフォルダ 1 つ＝ホストアプリ 1 種類。

用語: **ホスト**＝デザインプロジェクト（画面・データ・スクリプト＝ローコードで作る中身）を動かす C# アプリケーション。
このリポジトリにあるのはホスト。デザインプロジェクトはデザイナで作り、ホストの `DesignFileDirectory` に置く。
中身は Visual Studio テンプレートが `LowCodeApp` という名前で生成する結果そのもの（フレームワークは NuGet 参照）。

## バリアントの選び方

| フォルダ | 用途 | 選ぶ基準 |
|---|---|---|
| `Source/Hosts/Normal/` | Web（WASM クライアント + ASP.NET Core サーバー）、認証なし | 社内 LAN・前段で認証済み・デモ |
| `Source/Hosts/Cookie/` | Web、Cookie 認証（ユーザーテーブルでパスワード検証） | ログインが必要な業務アプリの既定 |
| `Source/Hosts/Maui/` | `Cookie` + .NET MAUI（Android/iOS）クライアント | スマホアプリとして配布したい。デザイン変更はストア更新なしで反映される |
| `Source/Hosts/MultiTenant/` | Web、マルチテナント（ASP.NET Core Identity、テナント別デザイン/データ） | 複数組織を 1 サーバーで。VS テンプレ化はまだ |
| `Source/Hosts/Wpf/` `Source/Hosts/WinForms/` | デスクトップ単体（サーバー機能を同一プロセスに内包） | サーバーを立てない・オフライン寄り |

迷ったら `Source/Hosts/Cookie/`。認証は後から足すより最初からある方が楽。

## 共通の構成

- `LowCodeApp.Server`（デスクトップは `LowCodeApp.Wpf` / `LowCodeApp.WinForms`）: サーバー。`Program.cs` で `SystemConfig` に appsettings を流し込み、
  Controller（`ModuleDataController` 等）が API を出す。DB は `Codeer.LowCode.Blazor.DbAccess`
- `LowCodeApp.Client`: Blazor WebAssembly。`Pages/LowCodePage.razor` がローコードページのホスト
- `LowCodeApp.Client.Shared`: クライアント共通サービス（`AppInfoService`/`ModuleDataService`/`NavigationServiceBase`）。
  Web・デスクトップ・モバイルで共有
- `LowCodeApp.Designer`: ビジュアルデザイナ（WPF）。デザインファイル（JSON/SQL/C# スクリプト）を編集する
- `LowCodeApp.Maui`（Maui のみ）: MAUI クライアント。`README.md` に接続先・証明書・起動手順

## Claude Code が作業するときの原則

- **テンプレのソースは自由に書き換えてよい。** 接着コードであり、ユーザーの所有物。守るべき境界は「NuGet パッケージの中」だけ
- **ライブラリで済むことを自前実装しない。** メール・承認・Excel/PDF・ファイル保存（S3/Azure）・AI 連携などは
  `Codeer.LowCode.Blazor.Extras` / `Extras.Server` にある。まず該当パッケージの API を探す
- **秘密情報を書かない。** 接続文字列・API キーは `appsettings.Development.json` か環境変数。コミットに含めない
- **ビルドが通る状態で止める。** `dotnet build LowCodeApp.sln` を最後に実行する
- リネームは `LowCodeApp` の一括置換（ファイル名・フォルダ名・中身）。`x:Class`、`*.styles.css` のリンクも対象

## 画面（デザイン）を作る

画面・データ構造・スクリプトはデザインファイルで定義する。Claude Code でデザインファイルを作る作業には
デザイナのメニュー Tools > Claude Code Workspace（`claude-workspace` サブコマンド）が展開する
ワークスペース（仕様書・サンプル・チェック CLI 同梱）を使う。ソースコードとデザインファイルは別物で、
デザインの変更にソースの再ビルドは要らない。

## 初回セットアップの手順

1. サーバープロジェクトの `appsettings.Development.json`: 接続文字列、`DesignFileDirectory`、`FileSystemStorages`。
   既定は `C:\Codeer.LowCode.Blazor.Local\...` を指す
2. `LowCodeApp.Designer` を起動してデザインプロジェクトを作る（テンプレートから選べる）
3. サーバーを起動。`Source/Hosts/Cookie/` は初回起動時に `admin`/`admin` のユーザーが自動作成される
4. `Source/Hosts/Maui/` はサーバーを先に起動してからアプリを起動（`LowCodeApp.Maui/README.md`）

## このリポジトリの性質（保守者向け）

**このリポジトリがテンプレートの正本。** プロジェクトはそれぞれ 1 か所にだけある：`Source/Hosts/Common/*`（全バリアント共通）と
`Source/Hosts/<Variant>/LowCodeApp.<Own>`（そのバリアント固有: Normal/Cookie の Server・Client、Maui、Wpf、WinForms）。
各バリアントの `LowCodeApp.sln` はそれらをその場で参照する（Maui は Cookie の Server/Client も参照）。sln は
`Source/Tools/StarterTool assemble` の生成物で手で編集しない。VSIX とデバッグ用コピーを作るときだけ、ツールが 1 フォルダに集めて
参照パスを書き換える。

- `dotnet run --project Source/Tools/StarterTool -- assemble` … 各バリアントの sln と全体 sln（`Source/Codeer.LowCode.Blazor.Starter.sln`、保守者用）を再生成（プロジェクトの追加・削除後）
- `dotnet run --project Source/Tools/StarterTool -- pack-vsix` … VSIX 用 zip を `Source/Tools/Codeer.LowCode.Blazor.Templates/ProjectTemplates` に生成
- `dotnet run --project Source/Tools/StarterTool -- export-debug <Codeer.LowCode.Blazor/Source>` … 本体リポにデバッグ用コピー（ProjectReference 化）を書き出す

タグ＝フレームワークのバージョン。`git diff v1.3.20 v1.3.23 -- Source/Hosts/Cookie/` で追従差分が見える。
