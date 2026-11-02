# AudioGuard (Windows 11)

AudioGuard is a tray utility that watches Windows playback-endpoint changes. When a known output endpoint is unplugged/removed, or Windows changes its default playback endpoint, AudioGuard:

1. Latches a safety state.
2. Mutes every currently active Windows render endpoint (including speakers that Windows may have selected as the new default).
3. Requests pause for every active media session exposed through Windows Global System Media Transport Controls (GSMTC).
4. Keeps output muted until the user explicitly chooses **Restore audio (manual unmute)** from the tray menu.

It is designed as a fail-safe helper, not as a replacement for the physical mute button in a meeting application.

## Important limitations

- **Mute is the primary safety barrier.** GSMTC can pause only applications that publish a Windows media session and accept remote pause requests. Some Teams/Zoom meeting builds and browser tabs may not expose a controllable session. The output mute still protects against audible playback when Windows routes audio through a normal shared-mode endpoint.
- Some 3.5 mm audio drivers do not report jack insertion/removal as a Windows endpoint state/default-device event. Detection then depends on the driver. Test the actual laptop jack and driver before relying on it.
- AudioGuard mutes active Windows render endpoints. An application using exclusive-mode audio, a separate USB hardware mixer, HDMI/monitor hardware controls, or a communications path that bypasses Windows endpoint mute may not be covered.
- Manual restore restores the mute state recorded for each endpoint when it was first muted. Devices that vanished before restore are skipped. Review the selected output and volume before speaking.
- The project has not been compiled or hardware-tested in this environment. Build and test it on the target Windows 11 laptop before using it for important meetings.

## Requirements

- Windows 11 x64
- .NET 10 SDK (for building)
- Internet access for first NuGet restore

## Quick setup (recommended)

1. Install the **.NET 10 SDK** for Windows x64 from https://dotnet.microsoft.com/download/dotnet/10.0.
2. Extract this ZIP to a normal folder (for example, Downloads\AudioGuard).
3. Double-click `setup.bat`. If Windows blocks the script, right-click `setup.ps1` and choose **Run with PowerShell**, or open PowerShell in this folder and run `powershell -NoProfile -ExecutionPolicy Bypass -File .\setup.ps1`.
4. The setup script publishes the app, copies it into `%LOCALAPPDATA%\Programs\AudioGuard`, creates a per-user scheduled task, and starts AudioGuard.

AudioGuard starts **when your Windows account signs in** (after login), not before the Windows sign-in screen. This is intentional: the tray app and the signed-in user's audio session are available only after sign-in. No administrator rights should be needed for this per-user setup.

## Build and run from source

Open PowerShell in `src/AudioGuard`:

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

The app starts in the system tray. Right-click its shield icon to access controls.

## Publish a self-contained Windows build

From the repository root:

```powershell
.\publish.ps1
```

The output is written to `publish\AudioGuard\AudioGuard.exe`.

## Install and launch at sign-in

After publishing, run PowerShell in the repository root:

```powershell
.\install.ps1
```

This copies the published application into `%LOCALAPPDATA%\Programs\AudioGuard` and creates a per-user logon scheduled task named `AudioGuard`. It does not require administrator rights. The task runs only when your Windows account signs in.

To remove the startup task and installed files:

```powershell
.\uninstall.ps1
```

## Tray controls

- **Pause monitoring / Resume monitoring** — temporarily disables or re-enables detection. It does not unmute audio.
- **Restore audio (manual unmute)** — explicitly restores each endpoint's saved mute state.
- **Test protection now** — triggers the safety path without disconnecting a device. Use this to verify that output is muted and media sessions pause.
- **Open log folder** — opens `%LOCALAPPDATA%\AudioGuard`.
- **Exit AudioGuard** — stops monitoring. It does not automatically unmute; use Restore audio first if you want to restore sound.

## First-time test procedure (do this before a real meeting)

1. Connect your usual headphones and start a harmless test video/music track at low volume.
2. Run AudioGuard and use **Test protection now**.
3. Confirm laptop speakers and other active outputs are muted, and supported media apps pause.
4. Reconnect/change outputs as you normally would. Confirm AudioGuard remains safety-muted.
5. Use **Restore audio (manual unmute)** and verify the original mute states return.
6. Test each connection type separately: 3.5 mm, USB and Bluetooth.
7. Test a Teams/Zoom test call with a colleague or a test meeting, not a live executive meeting.

If a 3.5 mm unplug does not trigger the safety state, the laptop's audio driver is not surfacing the jack event through the endpoint notifications AudioGuard currently watches. Do not assume it is protected until that is resolved.
