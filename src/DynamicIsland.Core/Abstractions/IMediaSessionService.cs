using DynamicIsland.Core.Models;

namespace DynamicIsland.Core.Abstractions;

public interface IMediaSessionService
{
    event EventHandler<MediaSnapshot?>? CurrentSessionChanged;

    ValueTask<MediaSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<bool> TryTogglePlaybackAsync(CancellationToken cancellationToken = default);

    Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default);

    Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default);
}
