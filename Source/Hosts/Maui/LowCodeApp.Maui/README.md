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
- Put machine-specific overrides in `appsettings.Development.json` (same shape, picked up automatically when present).

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

1. **Delete `appsettings.Development.json`.** The csproj bundles it *when the file exists*
   (`Condition="Exists(...)"`), so a build made with a local file still in place ships your machine's server URL
   as the app's default - testers get `http://localhost:...` and a connection error. Put the real URL in
   `appsettings.json` instead.
2. **The server must authenticate.** The branch that treats a server without authentication as usable is
   `#if DEBUG` only (see `LowCodePage.razor`), so a genuine Release build needs the Cookie variant (or another
   host that issues the authentication cookie).
3. **The server needs a certificate that the device trusts.** Release validates certificates - the bypass is
   also `#if DEBUG`. A development certificate on a LAN address will not do; that combination only works for
   local debugging (see *A physical device over the LAN* above).
4. **Remove the cleartext permissions** once the server is https only: `android:usesCleartextTraffic="true"`
   in `Platforms/Android/AndroidManifest.xml` and `NSAllowsArbitraryLoads` in `Platforms/iOS/Info.plist`.
5. **Sign with your own key.** A local Release build is signed with the auto-generated `CN=Android Debug`
   certificate, which Google Play rejects. Create an upload keystore, keep it out of the repository, and pass
   its path and password from the environment or a gitignored `.props` file - never from a tracked file.
6. **Bump `ApplicationVersion`** on every upload (`ApplicationDisplayVersion` is the user-visible string).

Do not reach for `-p:DefineConstants=DEBUG%3BTRACE` to get around 2 or 3. It builds an app that skips
certificate validation entirely, which is fine on your desk and unacceptable in anyone else's hands. It is
useful for measuring startup on a Release build against a development server, and for nothing else.

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
