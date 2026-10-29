using DynamicIsland.Core.Models;

namespace DynamicIsland.Core.Abstractions;

public interface ICalendarService
{
    ValueTask<IReadOnlyList<CalendarEventSnapshot>> GetUpcomingAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);
}
