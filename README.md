# BetterSound for Windows

A tiny, open-source tray app for Windows that puts a Windows 11 Quick
Settings–style sound mixer next to the clock: master volume, mute, output/input
device switching, and **per-app volume sliders and mute** — all from one
flyout, no separate mixer window to dig for.

This is a Windows port of [BetterSound for macOS](https://github.com/FernandoVD/BetterSound),
built to match its feature set and interaction style using WPF and the
Windows Core Audio APIs instead of SwiftUI and CoreAudio.

> **This code was written without access to a Windows machine to compile,
> run, or visually check it.** Everything here uses documented, stable
> Win32/.NET APIs except one small file (see [Known limitations](#known-limitations)
> below), but it hasn't been built or run yet. Treat this as a strong first
> draft to compile and iterate on, not a tested release.

## Features

- Tray icon with a borderless flyout, styled to look like Windows 11's own
  Quick Settings panel — capsule sliders, Segoe Fluent Icons glyphs, light/dark
  palette picked up from your Windows theme at launch
- Master volume slider + mute for the current default playback device
- Output device switcher — lists every active playback device, live-updates
  when devices connect/disconnect, and excludes anything Windows won't
  actually let you set as the default
- Input: a general input-level slider + mute, plus a device picker, same as
  the macOS build
- **Per-app volume sliders and mute**, one row per app that has an active
  audio session. Unlike macOS — which has no public "set this app's volume"
  API and needs a from-scratch Core Audio Process Tap engine to fake one —
  Windows has shipped this natively since Vista via `IAudioSessionManager2`
  / `ISimpleAudioVolume`, both documented public WASAPI interfaces. This is
  the one place the Windows port is *simpler* than the Mac original.
- Tray context menu and a Settings window: **Launch at login** (HKCU `Run`
  key) and update checking (manual "Check for Updates…" or an automatic daily
  background check), mirroring the macOS Settings panel
- An About window with version info and a link back to the macOS project

## Known limitations

- **Per-app output device routing is not implemented.** The macOS build can
  send one app to AirPods while everything else stays on speakers, via its
  Process Tap engine. Windows' own "App volume and device preferences" panel
  does the equivalent through a second undocumented internal mechanism (on
  top of the also-undocumented default-device-switch API below), and
  guessing at that blind — with no Windows machine to verify against — felt
  like the wrong tradeoff for a first pass. If you want this, look at
  [EarTrumpet](https://github.com/File-New-Project/EarTrumpet) (Microsoft's
  own open-source per-app mixer) for a working reference implementation.
- **Default-device switching uses a reverse-engineered COM interface**
  (`IPolicyConfig`) in
  [`Audio/DefaultDeviceInterop.cs`](src/BetterSound/Audio/DefaultDeviceInterop.cs).
  This is the same trick every Windows volume-switcher app uses (there's no
  public API), and the values in that file are the commonly-cited Windows 7+
  ones — but they're unverified against a real build. If switching the
  output/input device throws on first use, that file has pointers to
  known-good reference implementations to cross-check against.
- **Segoe Fluent Icons glyph codepoints weren't visually verified** (see
  `Audio/AudioDeviceKind.cs` and the mute-button glyphs on `AudioEngine`) —
  the app will run fine either way, worst case an icon renders as a blank box
  and needs its codepoint corrected.
- **App grouping is per-executable, not per-app-bundle.** An app that spawns
  several audio-capable processes (Chrome's per-tab renderers are the common
  case) shows as a single merged row — moving its slider applies to every
  session under that executable — but this hasn't been checked against a
  real multi-process app.
- **No live theme switching.** The light/dark palette is picked once at
  startup from `HKCU\...\Personalize\AppsUseLightTheme`; toggling Windows'
  theme while the app is running won't re-theme it until restart.
- **No acrylic/Mica blur.** The flyout uses a plain semi-transparent solid
  background rather than a real blur-behind effect, to avoid a second
  undocumented API (`SetWindowCompositionAttribute`) on top of the one
  already in use for device switching.

## Requirements

- **.NET 8 SDK** to build (`dotnet --version`) — get it from
  [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- Windows 7 SP1 or later to run — this is a .NET 8 desktop app, not WinUI 3,
  specifically so it isn't limited to Windows 10/11 only. (Fluent-styled
  chrome will look a bit out of place pre-Windows 10, but everything should
  still function.)

## Building

```bash
dotnet build src/BetterSound
```

## Running (development)

```bash
dotnet run --project src/BetterSound
```

## Publishing a standalone build

```bash
dotnet publish src/BetterSound -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

produces a single `BetterSound.exe` under
`src/BetterSound/bin/Release/net8.0-windows/win-x64/publish/` with the .NET
runtime bundled in, so it runs on a machine without .NET installed.

## Architecture

- `Audio/AudioEngine.cs` — mirrors `AudioEngine.swift`: bindable master/input
  volume and mute, driven by NAudio's `IMMNotificationClient` and each
  device's `AudioEndpointVolume.OnVolumeNotification`, no polling
- `Audio/AudioDevice.cs` — device model + classification into an icon kind,
  using the documented `PKEY_AudioEndpoint_FormFactor` property plus name
  heuristics (Bluetooth/USB/HDMI), the same intent as `AudioDevice.swift`'s
  transport-type classification
- `Audio/DefaultDeviceInterop.cs` — the one reverse-engineered piece; see
  [Known limitations](#known-limitations)
- `Audio/AppAudioController.cs` / `AppAudioItem.cs` — per-app volume via
  `IAudioSessionManager2`/`ISimpleAudioVolume` (NAudio's `AudioSessionManager`
  wrapper), polled every 1.5s to catch sessions starting/ending rather than
  depending on session-lifecycle event plumbing that's less battle-tested
  than the rest of NAudio's API surface
- `Views/MainFlyout.xaml(.cs)` — the tray flyout, WPF counterpart to
  `MenuBarContentView.swift`
- `Views/SettingsWindow.xaml(.cs)` / `AboutWindow.xaml(.cs)` — WPF
  counterparts to `SettingsView.swift` / `AboutView.swift`
- `Services/LoginItemManager.cs` — HKCU `Run` key registration, the Windows
  equivalent of `LoginItemManager.swift`'s `SMAppService` wrapper
- `Services/UpdateChecker.cs` — polls the GitHub Releases API for a newer
  tag; same no-silent-install philosophy as `UpdateChecker.swift`. **Update
  the `Repo` constant** once this project has its own GitHub repository —
  it's currently a placeholder (`FernandoVD/BetterSound-Windows`)
- `Styles/Colors.xaml` / `DarkColors.xaml` / `Controls.xaml` — the Fluent-ish
  palette and the capsule slider / toggle-switch control templates

## License

MIT — see [LICENSE](LICENSE). Created in 2026 by
[FernandoVD](https://github.com/FernandoVD), ported from
[BetterSound for macOS](https://github.com/FernandoVD/BetterSound).
