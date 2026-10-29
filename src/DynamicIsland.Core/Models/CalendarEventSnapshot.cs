namespace DynamicIsland.Core.Models;

public sealed record CalendarEventSnapshot(
    string Id,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Location = null);
