using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace AudioGuard;

internal sealed class AudioGuardContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _monitorItem;
    private readonly AudioProtectionService _service;
    private bool _monitoring = true;

    public AudioGuardContext()
    {
        var menu = new ContextMenuStrip();
        _statusItem = new ToolStripMenuItem("AudioGuard: monitoring") { Enabled = false };
        _monitorItem = new ToolStripMenuItem("Pause monitoring") { CheckOnClick = false };
        _monitorItem.Click += (_, _) => ToggleMonitoring();
        var restoreItem = new ToolStripMenuItem("Restore audio (manual unmute)");
        restoreItem.Click += (_, _) => RestoreAudio();
        var testItem = new ToolStripMenuItem("Test protection now");
        testItem.Click += async (_, _) => await _service.TriggerProtectionAsync("Manual safety test");
        var logItem = new ToolStripMenuItem("Open log folder");
        logItem.Click += (_, _) => OpenLogFolder();
        var exitItem = new ToolStripMenuItem("Exit AudioGuard");
        exitItem.Click += (_, _) => ExitThread();
        menu.Items.AddRange([_statusItem, new ToolStripSeparator(), _monitorItem, restoreItem,
            testItem, logItem, new ToolStripSeparator(), exitItem]);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "AudioGuard — monitoring audio devices",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowStatus();

        _service = new AudioProtectionService(UpdateStatus);
        _service.Start();
        UpdateStatus("Monitoring audio devices. Audio restores only when you choose Restore audio.");
    }

    private void ToggleMonitoring()
    {
        _monitoring = !_monitoring;
        _service.SetMonitoring(_monitoring);
        _monitorItem.Text = _monitoring ? "Pause monitoring" : "Resume monitoring";
        UpdateStatus(_monitoring ? "Monitoring enabled." : "Monitoring paused.");
    }

    private void RestoreAudio()
    {
        _service.RestoreAudioManually();
        UpdateStatus("Manual restore requested. Previously muted states were restored where possible.");
    }

    private void UpdateStatus(string message)
    {
        if (_tray == null || !_tray.Visible) return;
        void Update()
        {
            _statusItem.Text = _service.IsSafetyMuted
                ? "AudioGuard: SAFETY MUTED"
                : _monitoring ? "AudioGuard: monitoring" : "AudioGuard: monitoring paused";
            _tray.Text = _service.IsSafetyMuted
                ? "AudioGuard — safety mute active"
                : "AudioGuard — " + (_monitoring ? "monitoring" : "paused");
            _tray.BalloonTipTitle = "AudioGuard";
            _tray.BalloonTipText = message.Length > 240 ? message[..240] : message;
            _tray.ShowBalloonTip(2500);
        }
        if (_tray.ContextMenuStrip?.InvokeRequired == true)
            _tray.ContextMenuStrip.BeginInvoke((Action)Update);
        else
            Update();
    }

    private void ShowStatus() => UpdateStatus(_service.IsSafetyMuted
        ? "Safety mute is active. Use Restore audio when it is safe."
        : "AudioGuard is monitoring your audio devices.");

    private static void OpenLogFolder()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioGuard");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    protected override void ExitThreadCore()
    {
        _service.Dispose();
        _tray?.Visible = false;
        _tray?.Dispose();
        base.ExitThreadCore();
    }
}
