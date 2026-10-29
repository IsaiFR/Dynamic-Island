namespace DynamicIsland.Infrastructure.Weather;

public sealed record OpenMeteoWeatherOptions(
    double Latitude,
    double Longitude,
    string LocationName);
