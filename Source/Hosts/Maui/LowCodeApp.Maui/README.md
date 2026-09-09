# LowCodeApp (.NET MAUI client)

Android / iOS client for a Codeer.LowCode.Blazor server (Cookie authentication variant).
The app is a thin client: it downloads the design files from the server at startup, so changing the
application content only needs a new design deployment on the server, not a new store release.

## Requirements

- .NET 10 SDK with the `maui-android` / `maui-ios` workloads (`dotnet workload install maui-android maui-ios`,
  or the ".NET Multi-platform App UI development" workload in the Visual Studio installer)
- Android: Android SDK + emulator or device (installed by the Visual Studio workload)
- iOS: a paired Mac with Xcode. Without a Mac the iOS target still compiles, but cannot be packaged or run.

## Server address

`appsettings.json` holds the server URL. It is bundled into the app as the default; the value can be changed at
runtime from the app's *Settings* page (stored with MAUI `Preferences`).

```json
{
  "Server": {
    "BaseUrl": "https://10.0.2.2:7137/"
  }
}
```

- `10.0.2.2` is how the Android emulator reaches `localhost` of the PC. The iOS simulator can use `localhost` directly.
- The default points at the server's `https` launch profile. The ASP.NET Core development certificate is issued for
  `localhost` and is not trusted by the device, so **Debug builds accept any server certificate**
  (`ServerConnection.CreateHttpClient`). Release builds validate certificates normally, so a real certificate is
  required there.
- `http://...:5085/` also works, but only if the server runs with the `http` profile: with the `https` profile
  `UseHttpsRedirection` answers with a 307 to `https://localhost:7137`, which the device cannot reach.
- For a physical device use the PC's LAN address - see *A physical device over the LAN* below.
- Plain `http` is allowed for development by `android:usesCleartextTraffic="true"` (AndroidManifest.xml) and
  `NSAllowsArbitraryLoads` (Info.plist). Remove both when the server is `https` only.
- Three layers, each overriding the one before: `appsettings.json` (tracked, the template default),
  `appsettings.Development.json` (machine-specific, **`Debug` only**) and `appsettings.local.json`
  (machine-specific, **bundled in every configuration**, gitignored). Use the `local` one when a Release or
  TestFlight build has to reach a real server - it is the only layer that gets there without editing a tracked
  file. Same shape as `appsettings.json`, all optional.

### A physical device over the LAN

`adb reverse` only works while the device is attached to the PC and adb is alive, so a Debug build launched from
the device's home screen has to reach the server by its LAN address, for example `https://192.168.5.150:7137/`.
Three things have to line up.

1. **The server must listen on all interfaces.** The default launch profile binds `localhost`, which opens a
   loopback-only socket: a connection from the device never arrives. The Cookie variant has an `https-lan`
   profile that binds `0.0.0.0` instead - use that one:

   ```powershell
   dotnet run --project Source/Hosts/Cookie/LowCodeApp.Server --launch-profile https-lan
   ```

   There is also an `http-lan` profile (port 5085) that binds http only. Use it when you want to take TLS out
   of the picture: with both schemes bound, `UseHttpsRedirection` can work out the https port and answers 307,
   so plain http only stays plain when nothing else is listening.

2. **The Windows firewall must allow the port inbound** (once, from an elevated PowerShell):

   ```powershell
   New-NetFirewallRule -DisplayName "LowCodeApp Server (LAN)" -Direction Inbound -Protocol TCP -LocalPort 7137 -Profile Private -Action Allow
   ```

   Add a rule for 5085 as well if you want to use the `http-lan` profile.

3. **Keep the URL on `https`.** Over `http` the server answers with a 307 (`UseHttpsRedirection`) and the
   authentication cookie is `Secure`, so it is never sent back. `https` works even though the development
   certificate is issued for `localhost`, because Debug builds skip certificate validation (see
   `ServerConnection.CreateHttpClient`).

Then set the URL on the device: *Settings* in the title bar (⋮ menu on Android), or `appsettings.Development.json`
to make it the bundled default.

This is a Debug-only arrangement. A Release build validates the certificate, so it cannot talk to a development
server on a LAN address at all - a store or internal-test release needs a server with a real certificate.

## Running

The server does not host this app (unlike the WebAssembly client), so both must run.
Debugging a MAUI project together with other startup projects is unreliable, so start them separately:

1. Start the `Server` project first (launch profile `https`, `https://localhost:7137`), e.g. *Debug → Start Without Debugging*.
2. Start this project on an Android emulator / iOS simulator / device.

The server URL can be changed at runtime from the app: open the *Settings* item in the title bar (⋮ menu on Android),
enter the URL and press *Save*. The value is stored with MAUI `Preferences` and overrides `appsettings.json`;
*Reset to default* goes back to the bundled value.

## How it works

- `MauiProgram.cs` registers the same shared services as the browser client (`AddSharedServices`) and one
  `HttpClient` pointing at `Server:BaseUrl`.
- `Services/ServerConnection.cs` keeps the authentication cookie and the antiforgery token (`X-ANTIFORGERY-TOKEN`)
  that the browser normally handles by itself.
- `Pages/Login.razor` signs in through `api/account/login`; `Pages/LowCodePage.razor` hosts the low-code pages.
- The design files are loaded once per WebView. After deploying a new design press *Reload* in the title bar
  (it recreates the WebView). Server-side hot reload (`UseHotReload`) needs a SignalR connection the device can
  trust; with the development certificate it silently stays off.

## Startup time

A cold start on Android is a few seconds, most of it before any of this project's code runs: the .NET runtime
comes up, then MAUI, then the Android WebView, and only then does Blazor boot inside it. Measured on a
low-end Android 12 tablet, process start to the page being drawn:

| Build | First frame | Page drawn |
|---|---|---|
| Debug | 3.4s | 6.4s |
| Release, AOT disabled | 2.8s | |
| Release (Profiled AOT, the default) | **1.6s** | **4.1s** |

So judge startup on a Release build: Debug adds JNI checking, the debug helper library and fast deployment,
which loads every assembly individually from the device instead of from the APK.

A relaunch while the process is still alive (the app was only backgrounded) is ~90ms - the cost above is paid
only when Android has killed the process.

### Release builds fail in the AOT step when TEMP contains non-ASCII characters

    Microsoft.Android.Sdk.Aot.targets(123,5): error : Precompiling failed for ...\linked\<assembly>.dll with exit code 1
    error : The specified response file can not be read

The AOT compiler cannot read its response file when the path holds non-ASCII characters, which it does when the
Windows user name is not ASCII (`C:\Users\<name>\AppData\Local\Temp`). Point `TEMP`/`TMP` at an ASCII path for
the build:

```powershell
$env:TMP = "D:\aottmp"; $env:TEMP = "D:\aottmp"
dotnet build Source/Hosts/Maui/LowCodeApp.Maui/LowCodeApp.Maui.csproj -f net10.0-android -c Release
```

Disabling AOT (`-p:AndroidEnableProfiledAot=false -p:RunAOTCompilation=false`) also builds, but gives up most of
the startup gain in the table above, so prefer fixing `TEMP`.

## Publishing

Change `ApplicationId`, `ApplicationTitle`, the icon (`Resources/AppIcon`) and the splash screen (`Resources/Splash`)
in `LowCodeApp.csproj`, then follow the standard .NET MAUI publishing steps for Android / iOS.

### Before you build something you hand to other people

The development conveniences described above are exactly what must not ship. Work through this list.

1. **Set the server URL for the build.** `appsettings.Development.json` is bundled in `Debug` only, so a
   Release build no longer ships your machine's URL by accident - but it also means testers get whatever
   `appsettings.json` says, and its default is the Android emulator loopback, useless on a real device. Put the
   address in `appsettings.local.json` (gitignored, bundled in every configuration) or, for a real deployment,
   in `appsettings.json` itself.
2. **The server must authenticate.** The branch that treats a server without authentication as usable is
   `#if DEBUG` only (see `LowCodePage.razor`), so a genuine Release build needs the Cookie variant (or another
   host that issues the authentication cookie).
3. **The server needs a certificate that the device trusts.** Release validates certificates - the bypass is
   also `#if DEBUG`. A development certificate on a LAN address will not do; that combination only works for
   local debugging (see *A physical device over the LAN* above).
4. **Remove the cleartext permissions** once the server is https only: `android:usesCleartextTraffic="true"`
   in `Platforms/Android/AndroidManifest.xml` and `NSAllowsArbitraryLoads` in `Platforms/iOS/Info.plist`.
5. **Sign with your own key.** A local Release build is signed with the auto-generated `CN=Android Debug`
   certificate, which Google Play rejects; iOS will not even link without a signing identity. Both are
   configured in `LowCodeApp.Maui.local.props` - see *Publishing identity* below.
6. **Bump `ApplicationVersion`** on every upload (`ApplicationDisplayVersion` is the user-visible string).

Do not reach for `-p:DefineConstants=DEBUG%3BTRACE` to get around 2 or 3. It builds an app that skips
certificate validation entirely, which is fine on your desk and unacceptable in anyone else's hands. It is
useful for measuring startup on a Release build against a development server, and for nothing else.

### Publishing identity (`LowCodeApp.Maui.local.props`)

The tracked project deliberately keeps the neutral template defaults - `com.companyname.lowcodeapp`, no signing.
Anything specific to your organisation goes in `LowCodeApp.Maui.local.props`, which the csproj imports last when
it exists, so its properties override the ones above. `*.local.props` is gitignored, so bundle identifiers,
certificate names and keystore passwords stay out of the repository.

```powershell
copy LowCodeApp.Maui.local.props.sample LowCodeApp.Maui.local.props
```

Then edit it. `LowCodeApp.Maui.local.props.sample` documents every property; the short version is the bundle id,
the version pair, and - per platform - the signing identity.

### iOS: a TestFlight build

Needs a paid Apple Developer Program membership and a Mac with Xcode. Visual Studio's *Pair to Mac* is enough to
build from Windows, but the upload itself happens on the Mac.

**Once, on the Apple side.** All of this is in the [Apple Developer](https://developer.apple.com/account) portal
and [App Store Connect](https://appstoreconnect.apple.com):

1. *Certificates, Identifiers & Profiles > Identifiers*: register an explicit App ID with your bundle id. It has
   to match `ApplicationId` exactly and cannot be changed afterwards.
2. *Certificates*: create an **Apple Distribution** certificate and install it in the Mac's keychain (this is
   easiest from the Mac - the request is generated by Keychain Access). *Keychain Access > My Certificates* then
   shows its full name, e.g. `Apple Distribution: Example Inc. (ABCDE12345)`; that string is `CodesignKey`.
3. *Profiles*: create an **App Store** provisioning profile for that App ID, download it and double-click it on
   the Mac to install. Its *name* (not the file name, not the UUID) is `CodesignProvision`.
4. App Store Connect: *My Apps > +* and create the app record with the same bundle id.

**Every build.**

1. Bump `ApplicationVersion` in `LowCodeApp.Maui.local.props`. App Store Connect rejects a build number that has
   already been uploaded, and it does so *after* the upload finishes.
2. Archive. On the Mac (most reliable - Xcode's toolchain runs locally):

   ```bash
   dotnet publish -f net10.0-ios -c Release
   ```

   From Windows through the pairing, the same command works once *Pair to Mac* is connected; add
   `-p:ServerAddress=<mac> -p:ServerUser=<user>` for a command line build outside Visual Studio. `ArchiveOnBuild`
   is set in `local.props`, so the result is an `.xcarchive` plus an `.ipa` under
   `bin/Release/net10.0-ios/ios-arm64/publish/`.
3. Copy the `.ipa` to the Mac and upload it with **Transporter** (free, in the Mac App Store): sign in with the
   Apple ID that has access to the app record, drag the `.ipa` in, *Deliver*.
4. App Store Connect > *TestFlight*. The build shows as *Processing* for a few minutes, then becomes available to
   internal testers (up to 100 people on your team, no beta review). External testers need a beta review pass.

**Things that trip this up.**

- **Export compliance.** `Info.plist` declares `ITSAppUsesNonExemptEncryption=false` - the app only uses standard
  HTTPS. Without it, every single upload waits for a manual answer in App Store Connect before testers can
  install. Set it to `true` (and add `ITSEncryptionExportComplianceCode`) if you add your own cryptography.
- **The server must have a real certificate.** Release builds validate certificates; the bypass is `#if DEBUG`.
  A TestFlight build pointed at a development server on a LAN address cannot connect at all. Testers can change
  the address from the app's *Settings* page, but no address makes an untrusted certificate work.
- **`NSAllowsArbitraryLoads` is still `true`** in `Info.plist`. Internal TestFlight accepts it, but App Store
  review (and external TestFlight) asks why plain http is needed. Remove it once the server is https only.
- **iOS always AOT compiles**, so the trimming notes below apply to every iOS Release build, not just Android's.

### Pointing a Release build at a development server on the LAN

A TestFlight or Play internal-test build is a Release build, so the certificate bypass in
`ServerConnection.CreateHttpClient` is compiled out and the ASP.NET Core development certificate is rejected -
`https://<lan ip>:7137/` cannot work at all. Testing an actual store build against the PC on the office network
therefore means either giving the server a certificate the device trusts, or taking TLS out of the picture and
talking plain `http`. The second is far less work, and the pieces are already here.

1. **Run the server with `http-lan`** (binds `0.0.0.0:5085`, http only, so `UseHttpsRedirection` has no https
   port to redirect to and leaves requests alone):

   ```powershell
   dotnet run --project Source/Hosts/Cookie/LowCodeApp.Server --launch-profile http-lan
   ```

2. **Open the port for the network profile the device is actually on.** Windows classifies each network as
   Private or Public, and a rule scoped to Private does nothing on a Wi-Fi marked Public - which is the default
   for a wireless network. Check with `Get-NetConnectionProfile`, then, from an elevated PowerShell, either mark
   that network Private (preferred - it is a network you control):

   ```powershell
   Set-NetConnectionProfile -InterfaceAlias "Wi-Fi" -NetworkCategory Private
   New-NetFirewallRule -DisplayName "LowCodeApp Server http (LAN)" -Direction Inbound -Protocol TCP -LocalPort 5085 -Profile Private -Action Allow
   ```

   or, if the network must stay Public, add `-Profile Public` to the rule instead. Do not do that on a network
   you do not control.

3. **Put the PC's address on that interface in `appsettings.local.json`**, e.g.

   ```json
   { "Server": { "BaseUrl": "http://192.168.3.11:5085/" } }
   ```

   A machine with several adapters has several addresses; use the one on the same subnet as the phone
   (`Get-NetIPAddress -AddressFamily IPv4`). It usually comes from DHCP, so it changes - the app's *Settings*
   page fixes that without a rebuild.

4. **iOS lets this through because of `NSAllowsArbitraryLoads`** in `Info.plist`, and Android because of
   `android:usesCleartextTraffic="true"`. Removing either (which a store release should) also removes this
   arrangement.

The antiforgery cookie matters here. The Cookie variant sets it with `Secure = ctx.Request.IsHttps`, so it is
marked `Secure` over https and plain over http. Were it hardcoded to `true`, `CookieContainer` would refuse to
send it back over http and every API call would fail CSRF validation - which looks like a broken login rather
than a cookie problem. The authentication cookie itself already behaves this way (`CookieSecurePolicy.SameAsRequest`).

This is still a development arrangement: traffic is unencrypted, so keep it to a network you control, and give
real testers a server with a real certificate.

### iOS: the Mono interpreter, not full AOT

`Release` builds for iOS set `MtouchInterpreter=all`. This is not an optimisation, it is what makes the app run
at all: an iOS device cannot generate machine code at runtime (Apple does not allow it, so there is no JIT),
and the low code script engine compiles scripts while the app runs. Without the interpreter the app dies with
an unhandled exception while it loads the design - the loading screen stops part way and `#blazor-error-ui`
shows "An unhandled error has occurred". Android is unaffected: it keeps a JIT, and the profiled AOT above only
covers the startup path. Debug builds already use the interpreter by default (which is why the problem only
appears in a published build).

The interpreter executes IL instead of compiled machine code, so it is slower - no difference worth noticing on
current hardware, but if an old device needs the headroom, narrow the setting to the assemblies that actually
need dynamic code (`MtouchInterpreter=A,B`): enabling the interpreter at all is what turns dynamic code support
back on, so the rest can stay AOT compiled. As a side effect of skipping AOT, the device build takes a few
minutes instead of tens of them; expect the long build back if you narrow this.

### Trimming

`Release` keeps `TrimMode=partial` and Profiled AOT, and the csproj roots
`Microsoft.AspNetCore.Components.Web` (`TrimmerRootAssembly`). That last part is not optional: root components
are instantiated by reflection, so without it the trimmer removes `HeadOutlet`'s constructor and startup dies
in `AttachToPageAsync` with `CtorNotLocated` - before any component of this app runs, which means no
`try`/`catch` and no `ErrorBoundary` can catch it and the app simply sits on the loading screen forever. If you
add root components of your own, or see that symptom after a dependency update, look for the exception in the
WebView console (`chrome://inspect`, available because `AddBlazorWebViewDeveloperTools()` is enabled in Debug)
and root the assembly it names.

Two build details worth knowing when you change trimming settings: a csproj `ItemGroup` edit does not re-run
the trimmer (delete `obj/Release` and `bin/Release` first), and `-t:Run` can skip packaging - build normally,
then `-t:Install`.
