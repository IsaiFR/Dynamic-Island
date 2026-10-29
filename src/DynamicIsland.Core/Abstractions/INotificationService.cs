using DynamicIsland.Core.Models;

namespace DynamicIsland.Core.Abstractions;

public interface INotificationService
{
    event EventHandler? NotificationsChanged;

    ValueTask<bool> RequestAccessAsync(CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<SystemNotificationSnapshot>> GetCurrentAsync(
        CancellationToken cancellationToken = default);
}
