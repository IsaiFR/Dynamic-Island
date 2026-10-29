using DynamicIsland.Core.Models;

namespace DynamicIsland.Core.Abstractions;

public interface IWeatherService
{
    ValueTask<WeatherSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
