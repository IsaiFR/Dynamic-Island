using DynamicIsland.Core.Abstractions;
using DynamicIsland.Core.Models;
using System.Runtime.InteropServices;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DynamicIsland.Infrastructure.Media;

public sealed class WindowsMediaSessionService : IMediaSessionService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private string? _artworkKey;
    private byte[]? _artworkBytes;

    public event EventHandler<MediaSnapshot?>? CurrentSessionChanged;

    public async ValueTask<MediaSnapshot?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GlobalSystemMediaTransportControlsSessionManager manager = await GetManagerAsync();
        GlobalSystemMediaTransportControlsSession? session = manager.GetCurrentSession();
        if (session is null)
        {
            _artworkKey = null;
            _artworkBytes = null;
            CurrentSessionChanged?.Invoke(this, null);
            return null;
        }

        GlobalSystemMediaTransportControlsSessionMediaProperties properties =
            await session.TryGetMediaPropertiesAsync();
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playback =
            session.GetPlaybackInfo();
        GlobalSystemMediaTransportControlsSessionTimelineProperties timeline =
            session.GetTimelineProperties();

        string artworkKey = $"{session.SourceAppUserModelId}\u001f{properties.Title}\u001f{properties.Artist}\u001f{properties.AlbumTitle}\u001f{properties.TrackNumber}";
        if (_artworkKey != artworkKey)
        {
            _artworkKey = artworkKey;
            _artworkBytes = await ReadArtworkAsync(properties.Thumbnail);
        }

        MediaSnapshot snapshot = new(
            properties.Title ?? string.Empty,
            properties.Artist ?? string.Empty,
            playback.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            timeline.Position,
            timeline.EndTime - timeline.StartTime,
            _artworkBytes);
        CurrentSessionChanged?.Invoke(this, snapshot);
        return snapshot;
    }

    public async Task<bool> TryTogglePlaybackAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GlobalSystemMediaTransportControlsSession? session =
            (await GetManagerAsync()).GetCurrentSession();
        return session is not null && await session.TryTogglePlayPauseAsync();
    }

    public async Task<bool> TrySkipNextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GlobalSystemMediaTransportControlsSession? session =
            (await GetManagerAsync()).GetCurrentSession();
        return session is not null && await session.TrySkipNextAsync();
    }

    public async Task<bool> TrySkipPreviousAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GlobalSystemMediaTransportControlsSession? session =
            (await GetManagerAsync()).GetCurrentSession();
        return session is not null && await session.TrySkipPreviousAsync();
    }

    private async Task<GlobalSystemMediaTransportControlsSessionManager> GetManagerAsync() =>
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

    private static async Task<byte[]?> ReadArtworkAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null)
        {
            return null;
        }

        try
        {
            using IRandomAccessStreamWithContentType stream = await thumbnail.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 4 * 1024 * 1024)
            {
                return null;
            }

            using var reader = new DataReader(stream.GetInputStreamAt(0));
            uint length = (uint)stream.Size;
            if (await reader.LoadAsync(length) != length)
            {
                return null;
            }

            byte[] bytes = new byte[length];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception exception) when (exception is COMException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
