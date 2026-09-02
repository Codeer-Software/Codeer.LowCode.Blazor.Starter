# Codeer.LowCode.Blazor.Starter

[Codeer.LowCode.Blazor](https://www.nuget.org/packages/Codeer.LowCode.Blazor) でアプリケーションを始めるための、すぐビルドできるスターターソリューション集です。
`Source/Hosts/` 配下のフォルダ 1 つがアプリケーションのバリアント 1 種類で、それぞれ `LowCodeApp` という名前の完全なソリューションになっています（フレームワークは NuGet パッケージ参照）。

## いちばん早い始め方: Claude Code に全部やらせる

空のフォルダで [Claude Code](https://claude.com/claude-code) を起動し、こう伝えます:

> このURLを見て指示に従って https://github.com/Codeer-Software/Codeer.LowCode.Blazor.Starter

Claude Code が [ClaudeCodeForDeveloper/claude-code-setup.md](ClaudeCodeForDeveloper/claude-code-setup.md) を読んで、残りを実行します。
.NET SDK の確認（無ければ承諾のうえ winget でインストール）、このリポジトリの取得、ソリューションのビルド、サンプルデータ付きテンプレートからのデザインプロジェクト作成、
デザイン編集用のワークスペース展開、サーバー起動（ブラウザが開く）とデザイナの起動まで通ります。
Windows 専用です（デザイナが WPF アプリケーション）。Visual Studio は必須ではなく、VS Code 用の起動設定を同梱しています。
試用にライセンス登録は不要で、デザイナはトライアルとして動作します。

そのあとは Claude Code に画面の追加・変更を頼めます（「商品マスタの画面を追加して」など）。起動する場所は `DesignProjects/<デザイン名>/`
（デザインプロジェクトごとの作業場。セットアップで作られるサンプルは `DesignProjects/PatternShowcaseAuth/`）です。
C# のホスト側を変えたいときはリポジトリのルートで起動します。配置は、`DesignProjects/<名前>/design/` がデザインプロジェクト本体で、隣に `Project.md`・`ddl/`・`docs/` が並びます。
`ClaudeCodeForDeveloper/` にはセットアップ手順と、ホスト開発向けに生成されるリファレンスが入ります。

> **免責事項**: このリポジトリのセットアップ手順とワークスペースの文書は、AI（Claude Code）が実行する指示書です。AI はソフトウェアのインストール、
> ファイルの変更、コマンドの実行をあなたのマシン上で行い、その動作は決定的ではありません。提案内容を確認のうえ、自己責任でご利用ください。
> ここにあるものはすべて現状有姿（AS IS）で提供され、いかなる保証もありません（LICENSE を参照）。AI の操作に起因する損害について Codeer は責任を負いません。

## このリポジトリに入っているもの

| フォルダ | 内容 |
|---|---|
| `Source/Hosts/Normal/` | Blazor WebAssembly クライアント + ASP.NET Core サーバー。認証なし |
| `Source/Hosts/Cookie/` | Blazor WebAssembly クライアント + ASP.NET Core サーバー。Cookie 認証（ASP.NET Core Identity 方式） |
| `Source/Hosts/Maui/` | .NET MAUI（Android / iOS）クライアントのみ。`Cookie` サーバーのシンクライアント（サーバー・デザイナ・ツールは `Cookie` から作る） |
| `Source/Hosts/Wpf/` | 単体で動くデスクトップアプリ（WPF + BlazorWebView。サーバー機能を同一プロセスに内包） |
| `Source/Hosts/WinForms/` | 単体で動くデスクトップアプリ（WinForms + BlazorWebView。サーバー機能を同一プロセスに内包） |
| `Source/Hosts/MultiTenant/` | マルチテナントの Web ホスト（ASP.NET Core Identity、テナント別のデザインとデータ）。ビルドできるが Visual Studio テンプレートとしては未提供 |

各バリアントのソリューションには `Source/Hosts/Common/` のプロジェクトも含まれます: `LowCodeApp.Designer`（ビジュアルデザイナ。デザインファイルの編集に使う）、
`LowCodeApp.Client.Shared`（ブラウザ・デスクトップ・モバイルのクライアントで共有するサービス）、ライセンスツール。
`Source/Hosts/Maui/` だけは例外で、モバイルアプリと `Client.Shared` のみを持ち、起動中の `Cookie` サーバーを前提にします（URL はアプリの設定画面で入力）。
各プロジェクトはリポジトリ内に 1 か所だけあり、ソリューションはそれをその場で参照します。

ほかにできること:

- `Source/Hosts/` のバリアントから新しいアプリケーションを始める（リポジトリをクローンするか、Visual Studio 拡張機能をインストール）
- タグ間の `git diff`（例: `git diff v1.3.20 v1.3.23 -- Source/Hosts/Cookie`）でフレームワークのバージョン間の差分を確認し、自分のアプリケーションに反映する
- Claude Code の入口としてこのリポジトリを使う（`CLAUDE.md` と `ClaudeCodeForDeveloper/claude-code-setup.md` を参照）

## 手動で始める

1. リポジトリをクローンします（バリアントのフォルダ単体では不足です。ソリューションが `Source/Hosts/Common/` を参照しています）。
   代わりに Visual Studio 拡張機能をインストールすれば、そのテンプレートは自己完結しています。
2. `LowCodeApp.sln` を開きます。必要なもの: .NET 8 SDK（全バリアント）、`Source/Hosts/Maui/` は .NET 10 SDK と `maui-android` / `maui-ios` ワークロード。
   Visual Studio が無い場合は `dotnet build Source/Hosts/Cookie/LowCodeApp.sln`。`.vscode/` に VS Code（C# Dev Kit）用の起動設定があります。
3. サーバープロジェクト（`LowCodeApp.Server`。デスクトップは `LowCodeApp.Wpf` / `LowCodeApp.WinForms`）の `appsettings.Development.json` を確認します:
   接続文字列、`DesignFileDirectory`、ファイル保存先。既定は `C:\Codeer.LowCode.Blazor.Local\...` を指しているので、そのフォルダを作るかパスを変えます。
   各設定の意味は `CLAUDE.md` にあります。
4. デザインプロジェクトを作ります。`LowCodeApp.Designer` を起動してテンプレートを選ぶか、GUI なしなら
   `LowCodeApp.Designer.exe template-create --name PatternShowcaseAuth --out-dir DesignProjects\<名前>\design --data-dir <Local\Data> --deploy-dir <DesignFileDirectory>`。
   そのあとサーバーを起動します。`Source/Hosts/Maui/` は先に `Cookie` サーバーを起動し、アプリからその URL を指定します。

リネーム: ソリューションとプロジェクトの名前は `LowCodeApp` です。ファイル名・フォルダ名・ファイル内容（名前空間、`x:Class`、`*.styles.css` のリンク）の
`LowCodeApp` を一括置換すればリネームできます。

## バージョン

リポジトリには、生成元のフレームワークのバージョンでタグが付いています（例: `v1.3.23`）。
各タグが参照している `Codeer.LowCode.Blazor*` パッケージのバージョンが、そのバージョンで動作確認した組み合わせです。

保守者向け: ソリューション・Visual Studio 拡張機能・デバッグ用コピーの生成方法は [MAINTAINERS.md](MAINTAINERS.md) にあります。

## ライセンス

このリポジトリのファイルは MIT ライセンスです。Codeer.LowCode.Blazor 本体は商用製品で、独自のライセンスがあります。
