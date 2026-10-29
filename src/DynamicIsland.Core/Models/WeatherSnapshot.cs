namespace DynamicIsland.Core.Models;

public sealed record WeatherSnapshot(
    string Condition,
    double TemperatureCelsius,
    DateTimeOffset ObservedAt,
    double? FeelsLikeCelsius = null,
    int? RelativeHumidityPercent = null,
    double? WindSpeedKilometersPerHour = null,
    string? WindDirection = null,
    string? TimeZoneId = null,
    Uri? IconBaseUri = null);
