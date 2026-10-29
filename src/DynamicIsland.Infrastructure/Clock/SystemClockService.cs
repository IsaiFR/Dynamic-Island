using DynamicIsland.Core.Abstractions;

namespace DynamicIsland.Infrastructure.Clock;

public sealed class SystemClockService : IClockService
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
