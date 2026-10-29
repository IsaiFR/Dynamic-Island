using DynamicIsland.Core.Abstractions;
using DynamicIsland.Core.Models;
using System.Globalization;
using System.Text.Json;

namespace DynamicIsland.Infrastructure.Weather;

public sealed class OpenMeteoWeatherService(
    HttpClient httpClient,
    OpenMeteoWeatherOptions options) : IWeatherService
{
    public async ValueTask<WeatherSnapshot?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        string latitude = options.Latitude.ToString(CultureInfo.InvariantCulture);
        string longitude = options.Longitude.ToString(CultureInfo.InvariantCulture);
        string requestUri =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={latitude}" +
            $"&longitude={longitude}" +
            "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m,wind_direction_10m" +
            "&temperature_unit=celsius" +
            "&wind_speed_unit=kmh" +
            "&timeformat=unixtime" +
            "&timezone=auto" +
            "&forecast_days=1" +
            "&cell_selection=nearest";

        using HttpResponseMessage response = await httpClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        JsonElement root = document.RootElement;
        JsonElement? current = Property(root, "current");
        double? temperature = Number(Property(current, "temperature_2m"));
        int? weatherCode = Integer(Property(current, "weather_code"));
        if (temperature is null || weatherCode is null)
        {
            return null;
        }

        long? observedUnixTime = Long(Property(current, "time"));
        DateTimeOffset observedAt = observedUnixTime is long unixTime
            ? DateTimeOffset.FromUnixTimeSeconds(unixTime)
            : DateTimeOffset.UtcNow;
        double? windDirection = Number(Property(current, "wind_direction_10m"));

        return new WeatherSnapshot(
            DescribeWeatherCode(weatherCode.Value),
            temperature.Value,
            observedAt,
            Number(Property(current, "apparent_temperature")),
            Integer(Property(current, "relative_humidity_2m")),
            Number(Property(current, "wind_speed_10m")),
            windDirection is double degrees ? ToCardinal(degrees) : null,
            String(Property(root, "timezone")));
    }

    private static string DescribeWeatherCode(int code) => code switch
    {
        0 => "Despejado",
        1 => "Mayormente despejado",
        2 => "Parcialmente nublado",
        3 => "Nublado",
        45 or 48 => "Niebla",
        51 or 53 or 55 => "Llovizna",
        56 or 57 => "Llovizna helada",
        61 => "Lluvia ligera",
        63 => "Lluvia moderada",
        65 => "Lluvia intensa",
        66 or 67 => "Lluvia helada",
        71 => "Nieve ligera",
        73 => "Nieve moderada",
        75 or 77 => "Nieve intensa",
        80 => "Chubascos ligeros",
        81 => "Chubascos moderados",
        82 => "Chubascos intensos",
        85 or 86 => "Chubascos de nieve",
        95 => "Tormenta eléctrica",
        96 or 99 => "Tormenta con granizo",
        _ => "Condición variable"
    };

    private static string ToCardinal(double degrees)
    {
        string[] directions =
        [
            "NORTH", "NORTH_NORTHEAST", "NORTHEAST", "EAST_NORTHEAST",
            "EAST", "EAST_SOUTHEAST", "SOUTHEAST", "SOUTH_SOUTHEAST",
            "SOUTH", "SOUTH_SOUTHWEST", "SOUTHWEST", "WEST_SOUTHWEST",
            "WEST", "WEST_NORTHWEST", "NORTHWEST", "NORTH_NORTHWEST"
        ];
        int index = (int)Math.Round(((degrees % 360) + 360) % 360 / 22.5) % 16;
        return directions[index];
    }

    private static JsonElement? Property(JsonElement? parent, string name) =>
        parent is JsonElement element &&
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out JsonElement value)
            ? value
            : null;

    private static string? String(JsonElement? element) =>
        element is JsonElement value && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? Number(JsonElement? element) =>
        element is JsonElement value && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double number)
            ? number
            : null;

    private static int? Integer(JsonElement? element) =>
        element is JsonElement value && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int number)
            ? number
            : null;

    private static long? Long(JsonElement? element) =>
        element is JsonElement value && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out long number)
            ? number
            : null;
}
