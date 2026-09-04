using Windows.Media.Control;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal sealed class NowPlayingService
{
    private readonly object _sync = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private MusicSnapshot? _snapshot;
    private int _emptySamples;

    internal MusicSnapshot? Snapshot
    {
        get { lock (_sync) return _snapshot; }
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await RefreshAsync(cancellationToken);
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var session = _manager.GetCurrentSession();
            if (session is null)
            {
                ApplyEmpty();
                return;
            }

            var properties = await session.TryGetMediaPropertiesAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var timeline = session.GetTimelineProperties();
            var playback = session.GetPlaybackInfo();
            var title = properties?.Title?.Trim() ?? string.Empty;
            if (title.Length == 0)
            {
                ApplyEmpty();
                return;
            }

            var duration = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
            var elapsed = Math.Max(0, timeline.Position.TotalSeconds);
            var playing = playback?.PlaybackStatus ==
                          GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (playing && timeline.LastUpdatedTime.Year > 2000)
                elapsed += (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds;
            if (duration > 0) elapsed = Math.Clamp(elapsed, 0, duration);

            lock (_sync)
            {
                _emptySamples = 0;
                _snapshot = new MusicSnapshot(title, properties?.Artist?.Trim() ?? string.Empty,
                    properties?.AlbumTitle?.Trim() ?? string.Empty, playing &&
                    !(duration > 0 && elapsed >= duration - 0.25), elapsed, duration, DateTimeOffset.UtcNow);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or COMException)
        {
            ApplyEmpty();
        }
    }

    private void ApplyEmpty()
    {
        lock (_sync)
        {
            _emptySamples++;
            if (_emptySamples < 3 && _snapshot is not null) return;
            _snapshot = new MusicSnapshot(string.Empty, string.Empty, string.Empty, false,
                0, 0, DateTimeOffset.UtcNow);
        }
    }
}
