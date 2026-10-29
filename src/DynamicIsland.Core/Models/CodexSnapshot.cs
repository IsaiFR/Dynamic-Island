namespace DynamicIsland.Core.Models;

public sealed record CodexSnapshot(
    CodexActivityStatus Status,
    int? FiveHourRemainingPercent,
    int? WeeklyRemainingPercent,
    DateTimeOffset? FiveHourResetsAt,
    DateTimeOffset? WeeklyResetsAt);
