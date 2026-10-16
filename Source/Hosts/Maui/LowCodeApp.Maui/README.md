# LowCodeApp (.NET MAUI クライアント)

Codeer.LowCode.Blazor サーバー（Cookie 認証バリアント）向けの Android / iOS クライアントです。
このアプリはシンクライアントで、起動時にサーバーからデザインファイルをダウンロードします。そのため、
アプリの内容を変えるにはサーバーに新しいデザインをデプロイするだけで済み、ストアへの再リリースは不要です。

## 前提条件

- .NET 10 SDK と `maui-android` / `maui-ios` ワークロード（`dotnet workload install maui-android maui-ios`、
  または Visual Studio インストーラーの「.NET Multi-platform App UI 開発」ワークロード）
- Android: Android SDK とエミュレータまたは実機（Visual Studio のワークロードでインストールされます）
- iOS: ペアリング済みの Mac と Xcode。Mac がなくても iOS ターゲットのコンパイルはできますが、パッケージ化や実行はできません。

## サーバーのアドレス

`appsettings.json` にサーバーの URL を記述します。これは既定値としてアプリに同梱され、実行時にアプリの *Settings* ページから
変更できます（MAUI の `Preferences` に保存されます）。

```json
{
  "Server": {
    "BaseUrl": "https://10.0.2.2:7137/"
  }
}
```

- `10.0.2.2` は、Android エミュレータから PC の `localhost` に到達するためのアドレスです。iOS シミュレータでは `localhost` をそのまま使えます。
- 既定値はサーバーの `https` 起動プロファイルを指しています。ASP.NET Core の開発用証明書は `localhost` 向けに発行されていて
  デバイスからは信頼されないため、**Debug ビルドはどのサーバー証明書も受け入れます**
  （`ServerConnection.CreateHttpClient`）。Release ビルドは証明書を通常どおり検証するので、そちらでは正規の証明書が
  必要です。
- `http://...:5085/` も使えますが、サーバーを `http` プロファイルで動かしている場合に限ります。`https` プロファイルでは
  `UseHttpsRedirection` が `https://localhost:7137` への 307 を返し、デバイスからはそこに到達できません。
- 実機では PC の LAN アドレスを使います。後述の *実機から LAN 経由で接続する* を参照してください。
- 開発用に、平文の `http` は `android:usesCleartextTraffic="true"`（AndroidManifest.xml）と
  `NSAllowsArbitraryLoads`（Info.plist）で許可されています。サーバーが `https` のみになったら、両方を削除してください。
- 設定は 3 層で、後のものが前のものを上書きします。`appsettings.json`（Git 管理、テンプレートの既定値）、
  `appsettings.Development.json`（マシン固有、**`Debug` のみ**）、`appsettings.local.json`
  （マシン固有、**すべての構成で同梱**、gitignore 対象）です。Release や TestFlight のビルドから実際のサーバーに
  接続する必要があるときは `local` を使ってください。Git 管理のファイルを編集せずにそこまで届くのはこの層だけです。
  形式は `appsettings.json` と同じで、すべて省略可能です。

### 実機から LAN 経由で接続する

`adb reverse` はデバイスが PC に接続され、adb が動作している間しか使えません。そのため、デバイスのホーム画面から起動した
Debug ビルドは、LAN アドレス（例: `https://192.168.5.150:7137/`）でサーバーに到達する必要があります。
次の 3 点を揃えてください。

1. **サーバーをすべてのインターフェースで待ち受けさせる。** 既定の起動プロファイルは `localhost` にバインドするため、
   ループバック専用のソケットが開かれ、デバイスからの接続は届きません。Cookie バリアントには `0.0.0.0` にバインドする
   `https-lan` プロファイルがあるので、こちらを使います。

   ```powershell
   dotnet run --project Source/Hosts/Cookie/LowCodeApp.Server --launch-profile https-lan
   ```

   http だけをバインドする `http-lan` プロファイル（ポート 5085）もあります。TLS を切り離したいときに使います。
   両方のスキームをバインドしていると、`UseHttpsRedirection` が https のポートを割り出して 307 を返すため、
   他に待ち受けがないときだけ平文の http のままになります。

2. **Windows ファイアウォールでそのポートの受信を許可する**（初回のみ、管理者権限の PowerShell で）:

   ```powershell
   New-NetFirewallRule -DisplayName "LowCodeApp Server (LAN)" -Direction Inbound -Protocol TCP -LocalPort 7137 -Profile Private -Action Allow
   ```

   `http-lan` プロファイルを使う場合は、5085 のルールも追加してください。

3. **URL は `https` のままにする。** `http` ではサーバーが 307 を返し（`UseHttpsRedirection`）、認証 cookie は `Secure` なので
   送り返されません。開発用証明書が `localhost` 向けに発行されていても、Debug ビルドは証明書の検証を省略するため
   `https` で動作します（`ServerConnection.CreateHttpClient` を参照）。

そのうえで、デバイス側で URL を設定します。タイトルバーの *Settings*（Android では ⋮ メニュー）か、
`appsettings.Development.json` で同梱の既定値にします。

これは Debug 専用の構成です。Release ビルドは証明書を検証するので、LAN アドレス上の開発サーバーとは一切通信できません。
ストア配布や内部テストのリリースには、正規の証明書を持つサーバーが必要です。

## 実行

サーバーはこのアプリをホストしません（WebAssembly クライアントとは異なります）。そのため、両方を起動する必要があります。
MAUI プロジェクトを他のスタートアッププロジェクトと一緒にデバッグするのは不安定なので、別々に起動してください。

1. 先に `Server` プロジェクトを起動します（起動プロファイル `https`、`https://localhost:7137`）。例: *デバッグ → デバッグなしで開始*。
2. このプロジェクトを Android エミュレータ / iOS シミュレータ / 実機で起動します。

サーバーの URL はアプリ内から実行時に変更できます。タイトルバーの *Settings* 項目（Android では ⋮ メニュー）を開き、
URL を入力して *Save* を押します。値は MAUI の `Preferences` に保存され、`appsettings.json` を上書きします。
*Reset to default* で同梱の値に戻ります。

## 仕組み

- `MauiProgram.cs` は、ブラウザクライアントと同じ共有サービス（`AddSharedServices`）と、`Server:BaseUrl` を指す
  `HttpClient` を 1 つ登録します。
- `Services/ServerConnection.cs` は、ブラウザなら自動で処理してくれる認証 cookie とアンチフォージェリトークン
  （`X-ANTIFORGERY-TOKEN`）を保持します。
- `Pages/Login.razor` は `api/account/login` を通じてサインインします。`Pages/LowCodePage.razor` はローコードのページをホストします。
- デザインファイルは WebView ごとに 1 回だけ読み込まれます。新しいデザインをデプロイしたら、タイトルバーの *Reload* を
  押してください（WebView を作り直します）。サーバー側のホットリロード（`UseHotReload`）には、デバイスが信頼できる
  SignalR 接続が必要です。開発用証明書では、何も表示されずにオフのままになります。

## 起動時間

Android のコールドスタートは数秒かかり、その大半はこのプロジェクトのコードが動く前に費やされます。.NET ランタイムが起動し、
次に MAUI、続いて Android WebView が起動し、その後ようやくその中で Blazor が起動します。低スペックの Android 12 タブレットで、
プロセス開始からページが描画されるまでを計測した結果です。

| ビルド | 最初のフレーム | ページ描画 |
|---|---|---|
| Debug | 3.4s | 6.4s |
| Release、AOT 無効 | 2.8s | |
| Release（Profiled AOT、既定） | **1.6s** | **4.1s** |

そのため、起動時間は Release ビルドで判断してください。Debug は JNI チェック、デバッグ用ヘルパーライブラリ、高速デプロイが加わり、
すべてのアセンブリを APK からではなくデバイス上から個別に読み込みます。

プロセスがまだ生きている状態での再起動（アプリをバックグラウンドにしただけの場合）は約 90ms です。上記のコストが発生するのは、
Android がプロセスを終了させたときだけです。

### TEMP に ASCII 以外の文字が含まれていると、Release ビルドが AOT の手順で失敗する

    Microsoft.Android.Sdk.Aot.targets(123,5): error : Precompiling failed for ...\linked\<assembly>.dll with exit code 1
    error : The specified response file can not be read

AOT コンパイラは、パスに ASCII 以外の文字が含まれているとレスポンスファイルを読めません。Windows のユーザー名が ASCII でない場合に
該当します（`C:\Users\<name>\AppData\Local\Temp`）。ビルド中は `TEMP`/`TMP` を ASCII のパスに向けてください。

```powershell
$env:TMP = "D:\aottmp"; $env:TEMP = "D:\aottmp"
dotnet build Source/Hosts/Maui/LowCodeApp.Maui/LowCodeApp.Maui.csproj -f net10.0-android -c Release
```

AOT を無効にしても（`-p:AndroidEnableProfiledAot=false -p:RunAOTCompilation=false`）ビルドは通りますが、上の表の
起動時間の改善の大半を手放すことになるため、`TEMP` を直す方を推奨します。

## 公開

`LowCodeApp.csproj` の `ApplicationId`、`ApplicationTitle`、アイコン（`Resources/AppIcon`）、スプラッシュ画面（`Resources/Splash`）を
変更し、そのうえで Android / iOS 向けの標準的な .NET MAUI の公開手順に従ってください。

### 他の人に渡すものをビルドする前に

ここまでに説明した開発用の便宜は、まさに出荷してはいけないものです。次のリストを順に確認してください。

1. **ビルドに使うサーバー URL を設定する。** `appsettings.Development.json` は `Debug` でのみ同梱されるので、Release ビルドに
   自分のマシンの URL を誤って含めてしまうことはなくなりました。ただしその分、テスターには `appsettings.json` の内容が
   渡り、その既定値は Android エミュレータのループバックで、実機では役に立ちません。アドレスは `appsettings.local.json`
   （gitignore 対象、すべての構成で同梱）に書くか、本番デプロイなら `appsettings.json` 自体に書いてください。
2. **サーバーが認証を行うこと。** 認証のないサーバーを使えるものとして扱う分岐は `#if DEBUG` のみです（`LowCodePage.razor` を参照）。
   そのため、本物の Release ビルドには Cookie バリアント（または認証 cookie を発行する別のホスト）が必要です。
3. **デバイスが信頼する証明書をサーバーが持つこと。** Release は証明書を検証します。回避処理も `#if DEBUG` です。LAN アドレス上の
   開発用証明書では通りません。その組み合わせはローカルでのデバッグでのみ動作します（上の *実機から LAN 経由で接続する* を参照）。
4. **サーバーが https のみになったら、平文通信の許可を削除する。** `Platforms/Android/AndroidManifest.xml` の
   `android:usesCleartextTraffic="true"` と、`Platforms/iOS/Info.plist` の `NSAllowsArbitraryLoads` です。
5. **自分の鍵で署名する。** ローカルの Release ビルドは自動生成された `CN=Android Debug` 証明書で署名されますが、これは
   Google Play に拒否されます。iOS は署名 ID がないとリンクすらできません。どちらも `LowCodeApp.Maui.local.props` で設定します。
   後述の *公開用の識別情報* を参照してください。
6. **アップロードのたびに `ApplicationVersion` を上げる**（`ApplicationDisplayVersion` はユーザーに見えるバージョン文字列です）。

2 や 3 を回避するために `-p:DefineConstants=DEBUG%3BTRACE` に頼らないでください。これは証明書の検証を完全に省略するアプリを
ビルドします。自分の机の上なら構いませんが、他人の手に渡すものとしては許されません。使ってよいのは、Release ビルドの起動時間を
開発サーバーに対して計測するときだけです。

### 公開用の識別情報 (`LowCodeApp.Maui.local.props`)

Git 管理のプロジェクトでは、中立なテンプレートの既定値（`com.companyname.lowcodeapp`、署名なし）をあえてそのままにしています。
組織に固有のものは `LowCodeApp.Maui.local.props` に書きます。csproj はこのファイルがあれば最後にインポートするので、
上記のプロパティを上書きします。`*.local.props` は gitignore 対象なので、バンドル ID、証明書名、キーストアのパスワードが
リポジトリに入ることはありません。

```powershell
copy LowCodeApp.Maui.local.props.sample LowCodeApp.Maui.local.props
```

そのうえで編集してください。`LowCodeApp.Maui.local.props.sample` にすべてのプロパティの説明があります。要点は、バンドル ID、
バージョンの組、そしてプラットフォームごとの署名 ID です。

### iOS: TestFlight ビルド

有料の Apple Developer Program のメンバーシップと、Xcode を入れた Mac が必要です。Visual Studio の *Pair to Mac* で
Windows からビルドできますが、アップロード自体は Mac 上で行います。

**Apple 側での準備（初回のみ）。** すべて [Apple Developer](https://developer.apple.com/account) ポータルと
[App Store Connect](https://appstoreconnect.apple.com) で行います。

1. *Certificates, Identifiers & Profiles > Identifiers*: バンドル ID で明示的な App ID を登録します。
   `ApplicationId` と完全に一致している必要があり、後から変更できません。
2. *Certificates*: **Apple Distribution** 証明書を作成し、Mac のキーチェーンにインストールします（Mac で行うのが
   最も簡単です。リクエストは Keychain Access が生成します）。その後 *Keychain Access > My Certificates* に完全な名前
   （例: `Apple Distribution: Example Inc. (ABCDE12345)`）が表示されます。この文字列が `CodesignKey` です。
3. *Profiles*: その App ID 向けの **App Store** プロビジョニングプロファイルを作成し、ダウンロードして Mac で
   ダブルクリックしてインストールします。その*名前*（ファイル名でも UUID でもありません）が `CodesignProvision` です。
4. App Store Connect: *My Apps > +* で、同じバンドル ID のアプリレコードを作成します。

**ビルドのたびに行うこと。**

1. `LowCodeApp.Maui.local.props` の `ApplicationVersion` を上げます。App Store Connect は、すでにアップロード済みの
   ビルド番号を拒否します。しかも、アップロードが終わった*後*に拒否します。
2. アーカイブします。Mac 上で行うのが最も確実です（Xcode のツールチェーンがローカルで動くため）。

   ```bash
   dotnet publish -f net10.0-ios -c Release
   ```

   Windows からペアリング経由で行う場合も、*Pair to Mac* が接続されていれば同じコマンドで動作します。Visual Studio の
   外でコマンドラインからビルドするには、`-p:ServerAddress=<mac> -p:ServerUser=<user>` を追加します。`local.props` で
   `ArchiveOnBuild` が設定されているため、結果として `.xcarchive` と `.ipa` が
   `bin/Release/net10.0-ios/ios-arm64/publish/` に出力されます。
3. `.ipa` を Mac にコピーし、**Transporter**（無料、Mac App Store にあります）でアップロードします。アプリレコードに
   アクセスできる Apple ID でサインインし、`.ipa` をドラッグして *Deliver* を押します。
4. App Store Connect > *TestFlight*。ビルドは数分間 *Processing* と表示されます。
5. **ビルドをテスターグループに割り当てます**（グループの *Builds* タブ）。招待メールが送られるのはこの手順です。そのため、
   前のビルドを割り当てた後で追加したテスターには、この手順をもう一度行うまで何も届きません。
   グループの設定 *Automatically distribute builds* の対象は Xcode からアップロードしたビルドだけなので、
   Transporter でアップロードした場合は効果がありません。内部テスター（チームの最大 100 人）は数分でビルドを受け取れ、
   ベータ版審査は不要です。外部テスターにはベータ版審査が必要です。

**つまずきやすい点。**

- **輸出コンプライアンス。** `Info.plist` で `ITSAppUsesNonExemptEncryption=false` を宣言しています。アプリが標準の
  HTTPS しか使わないためです。これがないと、アップロードのたびに App Store Connect での手動回答を待たないと、
  テスターがインストールできません。独自の暗号処理を追加する場合は `true` に設定し、`ITSEncryptionExportComplianceCode` を
  追加してください。
- **サーバーには正規の証明書が必要です。** Release ビルドは証明書を検証し、回避処理は `#if DEBUG` です。
  LAN アドレス上の開発サーバーを指した TestFlight ビルドは、まったく接続できません。テスターはアプリの *Settings* ページから
  アドレスを変更できますが、どのアドレスにしても、信頼されない証明書が通るようにはなりません。
- **`NSAllowsArbitraryLoads` は `Info.plist` で今も `true` です。** 内部 TestFlight では通りますが、App Store 審査
  （と外部 TestFlight）では、なぜ平文の http が必要なのかを尋ねられます。サーバーが https のみになったら削除してください。
- **iOS は常に AOT コンパイルします。** そのため、後述のトリミングに関する注意は、Android だけでなく iOS のすべての
  Release ビルドに当てはまります。

### Android: Play の内部テスト

Google Play Console のデベロッパーアカウントが必要です。内部テストは最大 100 人のテスターに対応し、審査の待ち時間がなく、
アップロードが終わって数分でビルドが利用可能になります。

**Play 側での準備（初回のみ）。**

1. アップロードキーを作成します。そうしないと、ローカルの Release ビルドは自動生成された `CN=Android Debug` 証明書で署名され、
   Play に拒否されます。キーストアはリポジトリの外に置き、バックアップを取ってください。紛失すると、新しいアップロードキーを
   登録するためにサポートへの依頼が必要になります。

   ```powershell
   keytool -genkeypair -v -keystore D:\keys\lowcodeapp-upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10950 -storetype pkcs12
   ```

   Play は 2033 年 10 月 22 日より後まで有効なキーを要求するので、`-validity` を短くしないでください。
   `LowCodeApp.Maui.local.props` の `AndroidSigningKeyStore` でこれを指定し、パスワードはファイルに書かず環境変数に
   入れてください。上の *公開用の識別情報* を参照してください。
2. Play Console: *Create app*。パッケージ名は `ApplicationId` と完全に一致している必要があり、後から変更できません。
   **Play App Signing** は有効のままにしてください（既定）。上のキーはアップロードの署名にだけ使われ、デバイスに届くものには
   Google が署名します。
3. *App content* を入力します。データセーフティ、コンテンツのレーティング、ターゲット層、広告、プライバシーポリシーの URL は
   すべて、どのトラックでも（内部テストを含め）リリースする前に必須です。このアプリはユーザーのサインインを行うため、
   データセーフティのフォームでは、扱う認証情報を申告する必要があります。
4. *Testing > Internal testing*: トラックを作成し、メールアドレス（またはメーリングリスト）でテスターを追加して、
   オプトインのリンクを共有します。

**ビルドのたびに行うこと。**

1. `LowCodeApp.Maui.local.props` の `ApplicationVersion` を上げます。これが Android のバージョンコードになり、Play は
   アップロード済みのバージョンコードを拒否します。
2. 公開します。

   ```powershell
   dotnet publish Source/Hosts/Maui/LowCodeApp.Maui/LowCodeApp.Maui.csproj -f net10.0-android -c Release
   ```

   結果は `bin/Release/net10.0-android/publish/<package>-Signed.aab` です。アップロードするのはこのファイルです。
   隣にある署名なしの `.aab` は拒否されます。
3. Play Console > *Internal testing > Create new release*。`.aab` をドロップしてロールアウトします。テスターはオプトインの
   リンクから入手します。最初のインストールが表示されるまでに数分かかることがあります。

**つまずきやすい点。**

- **バージョンコードは iOS と共有されます。** `local.props` の `ApplicationVersion` が両方に使われるため、TestFlight の
  アップロードと Play のアップロードは同じ番号を消費します。どちらのストアも自分側の番号が増えることしか求めないので
  問題はありませんが、両者が揃うとは期待しないでください。
- **`usesCleartextTraffic="true"`** は `Platforms/Android/AndroidManifest.xml` に今も残っています。内部テストでは
  通りますが、サーバーが https のみになったら削除してください。
- **LAN 上のサーバーでは、テスターもその LAN 内にいる必要があります**。サーバーも起動している必要があります。
  次のセクションを参照してください。

### Release ビルドから LAN 上の開発サーバーに接続する

TestFlight や Play の内部テストのビルドは Release ビルドなので、`ServerConnection.CreateHttpClient` の証明書回避処理は
コンパイルで除外され、ASP.NET Core の開発用証明書は拒否されます。そのため `https://<lan ip>:7137/` は全く機能しません。
オフィスのネットワーク上の PC に対して実際のストア向けビルドをテストするには、デバイスが信頼する証明書をサーバーに用意するか、
TLS を切り離して平文の `http` で通信するかのどちらかになります。後者の方がはるかに手間が少なく、必要な部品はすでに揃っています。

1. **サーバーを `http-lan` で動かす**（`0.0.0.0:5085` にバインドし、http のみ。そのため `UseHttpsRedirection` にはリダイレクト先の
   https ポートがなく、リクエストはそのまま通ります）:

   ```powershell
   dotnet run --project Source/Hosts/Cookie/LowCodeApp.Server --launch-profile http-lan
   ```

2. **デバイスが実際に接続しているネットワークプロファイルに対してポートを開ける。** Windows は各ネットワークを Private または
   Public に分類し、Private に限定したルールは、Public に設定された Wi-Fi では何の効果もありません。無線ネットワークでは
   これが既定です。`Get-NetConnectionProfile` で確認し、管理者権限の PowerShell から、そのネットワークを Private にする
   （推奨。自分が管理するネットワークなので）か:

   ```powershell
   Set-NetConnectionProfile -InterfaceAlias "Wi-Fi" -NetworkCategory Private
   New-NetFirewallRule -DisplayName "LowCodeApp Server http (LAN)" -Direction Inbound -Protocol TCP -LocalPort 5085 -Profile Private -Action Allow
   ```

   ネットワークを Public のままにする必要がある場合は、代わりにルールに `-Profile Public` を付けます。自分が管理していない
   ネットワークでは行わないでください。

3. **そのインターフェースにある PC のアドレスを `appsettings.local.json` に書く。** 例:

   ```json
   { "Server": { "BaseUrl": "http://192.168.3.11:5085/" } }
   ```

   アダプターが複数あるマシンにはアドレスも複数あります。スマートフォンと同じサブネットのものを使ってください
   （`Get-NetIPAddress -AddressFamily IPv4`）。多くは DHCP で割り当てられるため変わります。再ビルドせずに固定するには、
   アプリの *Settings* ページを使います。

4. **iOS がこれを通すのは `Info.plist` の `NSAllowsArbitraryLoads` のおかげで、Android は
   `android:usesCleartextTraffic="true"` のおかげです。** どちらかを削除すると（ストア向けリリースでは削除すべきです）、
   この構成も使えなくなります。

ここではアンチフォージェリ cookie が重要です。Cookie バリアントはこれを `Secure = ctx.Request.IsHttps` で設定するため、
https では `Secure` が付き、http では付きません。もし `true` に固定されていると、`CookieContainer` が http 経由での
送り返しを拒否し、すべての API 呼び出しが CSRF 検証に失敗します。これは cookie の問題ではなく、ログインが壊れているように
見えます。認証 cookie 自体はすでにこのように動作しています（`CookieSecurePolicy.SameAsRequest`）。

これはあくまで開発用の構成です。通信が暗号化されないので、自分が管理するネットワークに限定し、本当のテスターには
正規の証明書を持つサーバーを用意してください。

### iOS: フル AOT ではなく Mono インタープリタ

iOS 向けの `Release` ビルドは `MtouchInterpreter=all` を設定します。これは最適化ではなく、アプリが動作するための必須の設定です。
iOS デバイスは実行時にマシンコードを生成できず（Apple が許可していないため、JIT がありません）、ローコードのスクリプトエンジンは
アプリの実行中にスクリプトをコンパイルします。インタープリタがないと、デザインを読み込む間にアプリが未処理の例外で停止します。
読み込み画面が途中で止まり、`#blazor-error-ui` に "An unhandled error has occurred" と表示されます。Android には影響しません。
JIT が使え、上で述べたプロファイル AOT は起動パスにしか効かないためです。Debug ビルドは既定でインタープリタを使います
（この問題が公開ビルドでしか現れないのはそのためです）。

インタープリタは IL をコンパイル済みのマシンコードの代わりに実行するので遅くなりますが、最近のハードウェアでは気になるほどの
差はありません。古いデバイスで余裕が必要なら、設定を動的コードが実際に必要なアセンブリに絞り込んでください
（`MtouchInterpreter=A,B`）。インタープリタを有効にすること自体が動的コードのサポートを元に戻すので、残りは AOT コンパイルの
ままにできます。AOT を省く副作用として、デバイス向けのビルドは数十分ではなく数分で済みます。絞り込むと、長いビルドが
戻ってくると思ってください。

### トリミング

`Release` は `TrimMode=partial` と Profiled AOT を維持し、csproj は `Microsoft.AspNetCore.Components.Web` を
ルートに指定しています（`TrimmerRootAssembly`）。この最後の指定は省略できません。ルートコンポーネントはリフレクションで
インスタンス化されるため、指定がないとトリマーが `HeadOutlet` のコンストラクターを削除し、起動が `AttachToPageAsync` で
`CtorNotLocated` により失敗します。これはこのアプリのどのコンポーネントよりも前に起こるので、`try`/`catch` も
`ErrorBoundary` も捕捉できず、アプリは読み込み画面のまま永久に止まります。独自のルートコンポーネントを追加した場合や、
依存関係の更新後にこの症状が出た場合は、WebView コンソール（`chrome://inspect`。Debug では `AddBlazorWebViewDeveloperTools()` が
有効なので使えます）で例外を探し、そこで挙がっているアセンブリをルートに指定してください。

トリミング設定を変更するときに知っておくべきビルド上の注意が 2 つあります。csproj の `ItemGroup` を編集してもトリマーは
再実行されません（先に `obj/Release` と `bin/Release` を削除してください）。また `-t:Run` はパッケージ化を省略することがあります。
通常どおりビルドしてから `-t:Install` を使ってください。
