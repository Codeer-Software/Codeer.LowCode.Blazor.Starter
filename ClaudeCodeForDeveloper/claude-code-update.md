# Claude Code 向け: アプリを最新のパッケージとテンプレートに更新する手順

**この文書は Claude Code（AI）が読んで実行するための手順書です。**（利用者向けの注意: この手順は AI が実行します。
AI の動作は決定的ではなく、ファイルの変更・コマンド実行を伴います。内容を確認のうえ自己責任でご利用ください。
本リポジトリは LICENSE のとおり AS IS で提供され、AI の操作に起因する損害について Codeer は責任を負いません。）
ユーザーが「最新化して」「パッケージを最新にして」「この URL を見て最新化して」などと言ったら、上から順に実行してください。

対象: [claude-code-setup.md](claude-code-setup.md) で作ったアプリのフォルダ（以下 **ROOT**。`Source\LowCodeApp.sln` と `CLAUDE.md` があるフォルダ）。

ゴール: Codeer.LowCode.Blazor 系の NuGet パッケージを最新にし、**ユーザーが望む範囲で**ホストのコード（`Source\` の C#・設定）を
最新のテンプレートに揃え、ビルドが通り、Claude Code 用の文書・ワークスペースも新しい版になっている状態にする。

## 進め方の原則

- **パッケージの版を上げることと、ホストのコードを変えることを分けて考える。** パッケージの版は上げてよい（頼まれたこと）。
  **コード（`Source\` の `.cs` / `.razor` / `appsettings*.json` など）を変えるかはユーザーに聞いてから決める**（Step 4）。
  ユーザーが書き換えたファイルを、テンプレートの内容で黙って上書きしない
- ユーザーが足したファイル（テンプレートに無いファイル）は触らない。`update-diff` の一覧にも出ない
- 変更する前に、変更するファイルを退避する（Step 5）。失敗したら退避から戻せる状態で進める
- 各ステップの終わりに 1 行で進捗を報告する。長い説明はしない。ユーザーへの応答はユーザーの言語で
- サーバーやデザイナがユーザーの手で起動中ならビルドがファイルをロックする。Step 6 の前に「サーバーとデザイナを終了してください」と頼む（勝手に止めない）

## Step 1. 元の版と最新の版を確かめる

`<ROOT>\ClaudeCodeForDeveloper\starter-origin.json` を読む（セットアップの `export-app` が書いたもの）:

```json
{ "starterCommit": "<書き出し元の Starter のコミット>", "maui": false }
```

- `starterCommit` がある → 3 者比較（元の版・今のアプリ・最新の版）ができる。「誰が何を変えたか」が分かる
- ファイルが無い、または `starterCommit` が `null` → 元の版が分からない。2 者比較（今のアプリと最新の版）で進め、Step 4 で**全部の差分をユーザーの判断に回す**

最新の Starter のコミットを取る:

```powershell
$latest = (Invoke-RestMethod https://api.github.com/repos/Codeer-Software/Codeer.LowCode.Blazor.Starter/commits/main).sha
```

`api.github.com` に届かないとき（社内プロキシ・回数制限）は `$latest = "refs/heads/main"` として続ける（zip の展開先は `Codeer.LowCode.Blazor.Starter-main`。
Step 2 の `$repo` をそれに読み替え、`--starter-commit` は付けない）。この場合は次の最新化でも元の版が分からない（2 者比較になる）。

`starterCommit` と `$latest` が同じなら、テンプレートは変わっていない。Step 6（ビルド）以降のパッケージの確認だけ行う
（`dotnet list "<ROOT>\Source\LowCodeApp.sln" package --outdated` で Codeer 系に新しい版が無ければ「最新です」と伝えて終わる）。

## Step 2. 元の版と最新の版を書き出す

作業は `<ROOT>\Local\Update\` で行う（`Local\` は gitignore 済み）。前回の残りがあれば、退避が入っているので消さずに日時つきの名前に変えて残す。

```powershell
$work = "<ROOT>\Local\Update"
if (Test-Path $work) { Rename-Item $work ("Update_" + (Get-Date -Format "yyyyMMdd_HHmmss")) }
New-Item -ItemType Directory $work | Out-Null
$maui = @()   # starter-origin.json の maui が true なら @("--maui")
function Export-Starter($commit, $dest) {
  Invoke-WebRequest "https://github.com/Codeer-Software/Codeer.LowCode.Blazor.Starter/archive/$commit.zip" -OutFile "$work\$commit.zip"
  Expand-Archive "$work\$commit.zip" -DestinationPath "$work\zip" -Force
  $repo = "$work\zip\Codeer.LowCode.Blazor.Starter-$commit"
  dotnet run --project "$repo\Source\Tools\StarterTool" -- export-app $dest @maui --starter-commit $commit | Out-Host   # 出力を戻り値に混ぜない
  if ($LASTEXITCODE -ne 0) { throw "export-app failed: $commit" }
  return $repo
}
$newRepo = Export-Starter $latest "$work\new"
Export-Starter "<starterCommit>" "$work\base" | Out-Null      # starterCommit が無いときは実行しない
```

`$work\base` と `$work\new` は、それぞれの版でセットアップ直後に書き出されたものと同じ形になる。

## Step 3. 差分を分類する

最新の Starter の `update-diff` で、テンプレートが書き出すファイルを 1 つずつ 3 者比較する（改行コードと BOM の違いは無視される）:

```powershell
dotnet run --project "$newRepo\Source\Tools\StarterTool" -- update-diff "<ROOT>" --base "$work\base" --new "$work\new" --out "$work\update-diff.json"
# starterCommit が無いときは --base を付けない
```

`update-diff.json` の `entries[]` が見るべきファイル（`path` は ROOT からの相対）。何も変わっていないファイルは出ない。

| `kind` | 意味 | 扱い |
|---|---|---|
| `update` | テンプレートだけが変わった（ユーザーは触っていない） | 最新の内容をそのまま入れられる |
| `merge` | テンプレートもユーザーも変えた | ユーザーの変更を残し、テンプレートの変更（base → new の差分）を当てる。ぶつかる箇所はユーザーに見せて決めてもらう |
| `add` | テンプレートに増えたファイル | 追加できる（新しい機能の結線であることが多い） |
| `addConflict` | テンプレートに増えたファイルと同じ名前のファイルをユーザーが作っていた | 内容を比べてユーザーに聞く |
| `delete` | テンプレートから消えたファイル（ユーザーは触っていない） | 消せる |
| `deleteModified` | テンプレートから消えたが、ユーザーが変えていた | 消さない。消えた理由（最新の版で何に置き換わったか）を調べてユーザーに伝える |
| `userDeleted` | ユーザーが消したファイルを、テンプレートが変えた | 戻さない。ユーザーが意図して消したものとして扱い、報告だけする |
| `differs` | （元の版が無いとき）今のアプリと最新の版が違う | 誰の変更か分からない。差分を見せてユーザーに聞く。元の版が無いときの `add` は、ユーザーが消したファイルのこともある。テンプレートから消えたファイルは元の版が無いと分からないので一覧に出ない（ビルドエラーの原因になっていたら疑う） |

`area` はファイルの種類: `package`（`.csproj`。パッケージの版・ターゲットフレームワーク）/ `settings`（`appsettings*.json`・`launchSettings.json`）/
`docs`（`Source\` の外: `CLAUDE.md`・`ClaudeCodeForDeveloper\*.md`・`.vscode\`・`.gitignore`・`LICENSE`）/ `code`（それ以外。C#・Razor・sln など）。`Local\`（フォント）は比べない。

前回「パッケージだけ」にしていると、`.csproj` はこの手順が版を上げたせいで `merge` になる。元の版との違いが Codeer 系パッケージの `Version` だけなら、ユーザーの変更ではないので `update` と同じに扱ってよい。

各ファイルの中身の差分は `$work\base\<path>` / `<ROOT>\<path>` / `$work\new\<path>` を比べて読む。
**テンプレートが何のために変えたのか**は、最新の Starter の git 履歴が無いので、差分の中身（足された API・コメント）と
`$work\new\CLAUDE.md` の説明から読み取る。

## Step 4. ユーザーに確認する（コードを変えるかどうか）

差分を読んで、次の形で**短く**まとめて伝える（ファイルごとに 1 行。何のための変更かを利用者の言葉で）:

```
パッケージ: Codeer.LowCode.Blazor 1.3.50 → 1.3.54、Codeer.LowCode.Blazor.Extras 0.22.0 → 0.23.1 …（.NET のターゲットが変わるときはそれも）
テンプレートのコードの変更（あなたは触っていないファイル）:
  - Server/Controllers/JobController.cs（追加）: 画面のボタンからジョブ（バッチ）を起動する受け口
  - Server/Program.cs: …
あなたが変更したファイルにも、テンプレートの変更があります:
  - Server/Services/DataService.cs: テンプレートは … を足した。あなたの … の変更とは別の箇所
```

そのうえで聞く（AskUserQuestion などで選べる形に。既定は先頭）:

1. **パッケージの版だけ上げる（コードは変えない）** — `.csproj` の Codeer 系パッケージの版だけを最新の版に合わせる
2. **パッケージを上げて、テンプレートのコードの変更も取り込む** — `update` / `add` / `delete` はそのまま反映し、`merge` などユーザーが変えたファイルは 1 つずつ差分を見せて確認する
3. **取り込むものを選ぶ** — 一覧から選んでもらう

補足として 1 行添える: 「テンプレートのコードの変更を取り込まないと、新しい版で増えた機能（例: 上の JobController）の一部が使えないことがあります。あとから取り込むこともできます」。
`docs`（Codeer が配る文書）は、ユーザーが変えていなければ（`update` / `add`）聞かずに最新にする（Claude Code の知識を新しい版に合わせるため）。
ユーザーが変えていた `docs`（`merge`）は、コードと同じく確認する。

## Step 5. 退避して反映する

変更・削除するファイルを `<ROOT>\Local\Update\backup\<path>` にコピーしてから変える。

- **パッケージだけ**（選択 1）: `area` が `package` のファイルについて、ROOT の `.csproj` にある `PackageReference` のうち、最新の版の `.csproj` にも同じ
  パッケージがあるものの `Version` だけを最新の値にする。パッケージの追加・削除、`TargetFramework` の変更はしない。
  ただし最新の版で `TargetFramework` が変わっていてパッケージがそれを要求する（Step 6 のビルドで `NU1202` などが出る）ときは、ユーザーに
  「.NET の版を上げる必要があります（.NET SDK <版> が必要）」と伝えて確認する
- **コードも取り込む**（選択 2・3）: `update` / `add` は `$work\new\<path>` をコピーする。`delete` は消す。
  `merge` は base → new の差分をユーザーのファイルに手で当てる（ユーザーの変更は残す）。確認したもの以外は変えない。
  `appsettings*.json` は**上書きしない**: 最新の版で増えたキーだけを足す（接続文字列・パス・ユーザーが足したデータソースはそのまま）
- `.claude\settings.local.json`・`Local\`・`DesignProjects\` はこの手順では触らない（下の Step 7 で各コマンドが更新する）

## Step 6. ビルドする

```powershell
dotnet build "<ROOT>\Source\LowCodeApp.sln"
```

成功の判定は終了コード 0。失敗したら:

- パッケージだけを上げてコンパイルエラーになったときは、最新のテンプレートの対応する箇所に答えがあることが多い。直すために必要な変更だけを
  最新の版から選び、**ユーザーに見せて確認してから**入れる（「ビルドを通すには <ファイル> の <箇所> を最新の形にする必要があります」）
- `Obsolete` の警告は止めずに報告だけする（動作する。テンプレートの新しい書き方は最新の版を見れば分かる）
- 直せないときは退避（`Local\Update\backup`）から戻して、何が起きたかを伝える

## Step 7. Claude Code 用の文書・ワークスペースを作り直す

デザイナ exe（`<ROOT>\Source\LowCodeApp.Designer\bin\Debug\<ターゲット>\LowCodeApp.Designer.exe`。以降 **DESIGNER_EXE**）は新しい版になっている。
GUI サブシステムの exe なので必ず `Start-Process -Wait` で呼び、結果は `--out` の JSON で受ける。

```powershell
Start-Process -FilePath "<DESIGNER_EXE>" -Wait -ArgumentList @(
  "developer-workspace", "<ROOT>", "--out", "<ROOT>\Local\developer-workspace.json")
# DesignProjects の下のフォルダそれぞれについて
Start-Process -FilePath "<DESIGNER_EXE>" -Wait -ArgumentList @(
  "claude-workspace", "<ROOT>\DesignProjects\<名前>", "--project", "design", "--out", "<ROOT>\Local\claude-workspace.json")
Start-Process -FilePath "<DESIGNER_EXE>" -Wait -ArgumentList @(
  "designcheck", "<ROOT>\DesignProjects\<名前>\design", "--out", "<ROOT>\Local\designcheck.json")
```

判定: `developer-workspace.json` に `"error"` が無い。`claude-workspace.json` の `aiRefresh` が `ok`。
`designcheck.json` にエラーがあれば一覧をユーザーに伝える（新しい版で増えた検査で見つかった設計の問題。直すかはユーザーに聞く）。

## Step 8. 元の版の記録を更新する

- コードの変更を取り込んだ（選択 2・3）: `<ROOT>\ClaudeCodeForDeveloper\starter-origin.json` を `$work\new\ClaudeCodeForDeveloper\starter-origin.json` で置き換える
  （次の最新化は今回の版から比べる。取り込まなかった変更は、ユーザーが変えたものとして扱われ、次回は提案されない）
- パッケージだけ（選択 1）: `starter-origin.json` は変えない（次回も今回と同じテンプレートの変更を提案できる）

`<ROOT>\Local\Update\` は残しておく（退避が入っている）。動作を確かめてもらってから、ユーザーが不要と言えば消す。

## Step 9. 完了メッセージ

短く伝える:

- 上げたパッケージの版（前 → 後）
- 取り込んだテンプレートの変更と、取り込まなかった変更（あとから取り込むときは「最新化して」と言えばよい。パッケージだけにしたときは同じ変更をもう一度提案する）
- サーバーとデザイナを起動し直して確かめてほしいこと（C# が変わったので再起動が必要）。デザインはそのまま動く。新しいデザイナで開いて「送信」するとデプロイされる
- 退避の場所（`<ROOT>\Local\Update\backup`）
