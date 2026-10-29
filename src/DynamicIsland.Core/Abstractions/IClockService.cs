namespace DynamicIsland.Core.Abstractions;

public interface IClockService
{
    DateTimeOffset Now { get; }
}
