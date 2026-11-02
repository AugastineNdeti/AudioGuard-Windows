using Windows.Media.Control;

namespace AudioGuard;

internal static class MediaSessionController
{
    /// <summary>
    /// Requests pause on every session currently reporting Playing.
    /// This only controls apps that publish Windows System Media Transport Controls.
    /// </summary>
    public static async Task<int> PauseAllPlayingSessionsAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var sessions = manager.GetSessions();
            int paused = 0;

            foreach (var session in sessions)
            {
                try
                {
                    var info = session.GetPlaybackInfo();
                    if (info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        if (await session.TryPauseAsync())
                            paused++;
                    }
                }
                catch
                {
                    // A session may disappear or reject remote-control requests while iterating.
                }
            }
            return paused;
        }
        catch
        {
            // The endpoint mute remains the primary safety barrier if GSMTC is unavailable.
            return 0;
        }
    }
}
