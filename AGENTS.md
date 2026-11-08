# Codeer.LowCode.Blazor.Starter - Codex / GitHub Copilot 向けの入口

## まず CLAUDE.md を読む

**説明と作業ルールの本体は同じフォルダの `CLAUDE.md`。着手前に全文を読み、それに従う**（名前は Claude だが、どのエージェントにも共通。
`AIForDeveloper/` と `DesignProjects/<デザイン名>/` の文書も同じ）。末尾の「Claude Code 固有」の節だけは読み飛ばし、代わりにこのファイルに従う。

- セットアップ（「この URL を見て指示に従って」「セットアップして」）は `AIForDeveloper/claude-code-setup.md`、最新化は `AIForDeveloper/claude-code-update.md` を上から実行する
- デザインを触るときは `DesignProjects/<デザイン名>/CLAUDE.md` と `AIForDesigner/CLAUDE.md` も読む（ルートで起動したときは自動では読み込まれない）

## Codex

- 許可とフックはデザイナの `developer-workspace`（セットアップの Step 5）が、このマシンの exe パス入りで生成する。手で編集しない
  - `.codex/hooks.json` — セッション開始と各プロンプトで `DesignProjects/*/` の各ワークスペースを最新化する。ファイル編集の前に、デザインを編集できないライセンスならデザインの編集を止める
  - `.codex/rules/codeer-lowcode.rules` — デザイナ exe の CLI・`dotnet build` / `test` / `run`・`node`・`npm install`・`npx playwright` を確認なしで実行する
- デザイナ exe の場所が変わったら `developer-workspace` を実行し直す（この 2 つと `.claude/settings.local.json` の hooks 節・CLI 許可が新しい exe に合わせて更新される。セットアップの Step 5）
- この 2 つは **Codex がこのフォルダ（プロジェクト）を信頼済みにしたときだけ読まれる**。フックはさらに `/hooks` で中身を確認して信頼するまで動かない。CLI のたびに確認が出る・デザインの `AIForDesigner/` が古いなら、この 2 点をユーザーに案内する
- ファイルの作成・編集は apply_patch、読み込み・検索はシェル（`rg` 等）で行う

## GitHub Copilot

- フックは Claude Code と同じ `.claude/settings.local.json` のものを Copilot（VS Code のエージェント・Copilot CLI）が読んで動かす。ただしフックの出力は Copilot には届かないので、デザインを触るときは `DesignProjects/<デザイン名>/AGENTS.md` の手順で最新化とライセンスを自分で確かめる
- デザイナ exe の CLI は PowerShell から `Start-Process -FilePath "<exe>" -Wait -ArgumentList ...` の形で呼ぶ（`& "<exe>" ...` では終了を待てない）
- **`.claude/settings.local.json` の許可リストは Copilot では効かない。** コマンドの許可は Copilot の承認で行う（Copilot 用の許可設定は生成しない）。承認を減らす設定（VS Code のユーザー設定の `chat.tools.terminal.autoApprove`）の中身と案内の仕方は `DesignProjects/<デザイン名>/AGENTS.md` の「GitHub Copilot」にある。セットアップの完了メッセージでも案内する
- ファイルの作成・編集は Copilot の作成・編集ツールで行う（手順書の `[IO.File]::WriteAllText` は、シェルの中で作る小さなファイル用）。作ったファイルを使うコマンドは、作成が終わってから実行する（同じ並列の呼び出しに入れない）
- 作業物（`--out` の JSON・検証スクリプト等）は `<ROOT>\Local\scratch\` に置く（Copilot にはセッションの一時領域が無い。`Local/` は gitignore 済みで、作業フォルダの外に書くより承認が少ない）

## 共通の注意

- `appsettings.Development.json` と `DesignProjects/*/design/designer.settings.Development.json`（接続文字列などの秘密情報）は、Claude Code と違って読み書きが仕組みでは止まらない。`CLAUDE.md` の規則どおり、セットアップ手順で必要なとき以外は読まない
- 許可やフックの設定ができた・作り直したときは、次のセッションから効く。新しいセッションを始めるよう案内する
