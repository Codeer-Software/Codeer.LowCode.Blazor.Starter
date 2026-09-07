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
   loopback-only socket: a connection from the device never arrives. Both the Cookie and the Normal variant have
   an `https-lan` profile that binds `0.0.0.0` instead - use that one:

   ```powershell
   dotnet run --project Source/Hosts/Cookie/LowCodeApp.Server --launch-profile https-lan
   ```

   The two variants use different ports (Cookie 7137, Normal 7169), so both can run at the same time. A Debug
   build talking to the Normal variant skips the login page (see `LowCodePage.razor`); a Release build cannot
   use it at all, because that branch is `#if DEBUG`.

2. **The Windows firewall must allow the port inbound** (once, from an elevated PowerShell):

   ```powershell
   New-NetFirewallRule -DisplayName "LowCodeApp Server (LAN)" -Direction Inbound -Protocol TCP -LocalPort 7137 -Profile Private -Action Allow
   ```

   Add a second rule for 7169 to reach the Normal variant as well.

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
