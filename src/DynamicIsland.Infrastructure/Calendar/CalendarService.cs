using DynamicIsland.Core.Abstractions;
using DynamicIsland.Core.Models;

namespace DynamicIsland.Infrastructure.Calendar;

public sealed class CalendarService : ICalendarService
{
    public ValueTask<IReadOnlyList<CalendarEventSnapshot>> GetUpcomingAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CalendarEventSnapshot> events = [];
        return ValueTask.FromResult(events);
    }
}
