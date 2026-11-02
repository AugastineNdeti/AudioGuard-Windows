using NAudio.CoreAudioApi;
using Windows.Media.Control;

namespace AudioGuard;

/// <summary>
/// Fail-safe policy: any render-device removal/unplug or default-render change
/// enters a latched safety-mute state. Only the user can restore audio.
/// </summary>
internal sealed class AudioProtectionService : IDisposable
{
    private readonly Action<string> _notify;
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDeviceNotificationClient _notifications;
    private readonly object _gate = new();
    private readonly Dictionary<string, bool> _originalMuteStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownRenderDeviceIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _monitoring = true;
    private bool _disposed;
    private bool _isSafetyMuted;
    private int _triggerInProgress;

    public bool IsSafetyMuted { get { lock (_gate) return _isSafetyMuted; } }

    public AudioProtectionService(Action<string> notify)
    {
        _notify = notify;
        _notifications = _enumerator.CreateNotificationClient();
        _notifications.DeviceStateChanged += OnDeviceStateChanged;
        _notifications.DeviceRemoved += OnDeviceRemoved;
        _notifications.DeviceAdded += OnDeviceAdded;
        _notifications.DefaultDeviceChanged += OnDefaultDeviceChanged;
    }

    public void Start()
    {
        RememberActiveRenderDevices();
        Log("AudioGuard started. Monitoring is enabled; manual restoration is required.");
    }

    public void SetMonitoring(bool enabled)
    {
        lock (_gate) _monitoring = enabled;
        Log(enabled ? "Monitoring enabled." : "Monitoring paused by user.");
    }

    private void OnDeviceStateChanged(object? sender, DeviceStateChangedEventArgs e)
    {
        if (!MonitoringEnabled()) return;
        if (e.NewState is not (DeviceState.Unplugged or DeviceState.NotPresent or DeviceState.Disabled))
        {
            if (e.NewState == DeviceState.Active && IsSafetyMuted)
                _ = MuteAllActiveRenderEndpointsAsync();
            return;
        }

        if (IsKnownRenderDevice(e.DeviceId))
            _ = TriggerProtectionAsync($"Output device state changed to {e.NewState}");
    }

    private void OnDeviceRemoved(object? sender, DeviceNotificationEventArgs e)
    {
        if (MonitoringEnabled() && IsKnownRenderDevice(e.DeviceId))
            _ = TriggerProtectionAsync("Output device removed");
    }

    private void OnDeviceAdded(object? sender, DeviceNotificationEventArgs e)
    {
        if (!MonitoringEnabled()) return;
        RememberActiveRenderDevices();
        if (IsSafetyMuted)
            _ = MuteAllActiveRenderEndpointsAsync();
    }

    private void OnDefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
    {
        if (!MonitoringEnabled() || e.Flow != DataFlow.Render) return;
        RememberActiveRenderDevices();
        _ = TriggerProtectionAsync("Default playback device changed");
    }

    public async Task TriggerProtectionAsync(string reason)
    {
        if (!MonitoringEnabled() || Interlocked.Exchange(ref _triggerInProgress, 1) == 1)
            return;

        try
        {
            lock (_gate) _isSafetyMuted = true;
            Log($"SAFETY EVENT: {reason}. Muting active output endpoints and pausing supported media sessions.");

            // Mute first. This is the primary protection against sound reaching speakers.
            await MuteAllActiveRenderEndpointsAsync();

            // Pause each active session that exposes Windows System Media Transport Controls.
            int paused = await MediaSessionController.PauseAllPlayingSessionsAsync();
            Log($"Protection applied. Media sessions paused: {paused}. System output remains muted until manual restore.");
            _notify($"Safety mute active. {paused} media session(s) paused. Use 'Restore audio' when safe.");
        }
        catch (Exception ex)
        {
            Log("Protection error: " + ex);
            _notify("AudioGuard encountered an error while applying protection. Check the log.");
        }
        finally
        {
            Interlocked.Exchange(ref _triggerInProgress, 0);
        }
    }

    private async Task MuteAllActiveRenderEndpointsAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (MMDevice device in devices)
                {
                    using (device)
                    {
                        try
                        {
                            lock (_gate)
                            {
                                if (!_originalMuteStates.ContainsKey(device.ID))
                                    _originalMuteStates[device.ID] = device.AudioEndpointVolume.Mute;
                            }
                            device.AudioEndpointVolume.Mute = true;
                            lock (_gate) _knownRenderDeviceIds.Add(device.ID);
                        }
                        catch (Exception ex) { Log($"Could not mute endpoint '{SafeName(device)}': {ex.Message}"); }
                    }
                }
            }
            catch (Exception ex) { Log("Could not enumerate output endpoints: " + ex.Message); }
        });
    }

    public void RestoreAudioManually()
    {
        Dictionary<string, bool> snapshot;
        lock (_gate)
        {
            if (!_isSafetyMuted)
            {
                _notify("No AudioGuard safety mute is currently active.");
                return;
            }
            snapshot = new Dictionary<string, bool>(_originalMuteStates, StringComparer.OrdinalIgnoreCase);
            _isSafetyMuted = false;
            _originalMuteStates.Clear();
        }

        Task.Run(() =>
        {
            int restored = 0;
            foreach (var pair in snapshot)
            {
                try
                {
                    using var device = _enumerator.GetDevice(pair.Key);
                    if (device.State == DeviceState.Active)
                    {
                        device.AudioEndpointVolume.Mute = pair.Value;
                        restored++;
                    }
                }
                catch (Exception ex) { Log($"Could not restore endpoint {pair.Key}: {ex.Message}"); }
            }
            Log($"Manual restore completed. Endpoint states restored: {restored}.");
            _notify($"Manual restore completed. {restored} endpoint(s) restored. Check your output device and volume before speaking.");
        });
    }

    private void RememberActiveRenderDevices()
    {
        try
        {
            using var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (MMDevice device in devices)
            {
                using (device)
                    lock (_gate) _knownRenderDeviceIds.Add(device.ID);
            }
        }
        catch (Exception ex) { Log("Device inventory error: " + ex.Message); }
    }

    private bool IsKnownRenderDevice(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_gate) return _knownRenderDeviceIds.Contains(id);
    }

    private bool MonitoringEnabled() { lock (_gate) return _monitoring && !_disposed; }

    private static string SafeName(MMDevice device)
    {
        try { return device.FriendlyName; } catch { return "unknown"; }
    }

    private static void Log(string message)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioGuard");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "audioguard.log"), $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {message}{Environment.NewLine}");
        }
        catch { /* Logging must never crash the protection path. */ }
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
        _notifications.Dispose();
        _enumerator.Dispose();
    }
}
