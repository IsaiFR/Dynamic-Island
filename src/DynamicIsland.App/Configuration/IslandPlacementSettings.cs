using DynamicIsland.Core.Models;

namespace DynamicIsland.App.Configuration;

internal static class IslandPlacementSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicIsland",
        "placement.txt");

    public static IslandPlacement Load()
    {
        try
        {
            if (File.Exists(SettingsPath) &&
                Enum.TryParse(File.ReadAllText(SettingsPath).Trim(), out IslandPlacement placement) &&
                Enum.IsDefined(placement))
            {
                return placement;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Una configuración ilegible conserva la posición inicial en el centro.
        }

        return IslandPlacement.Center;
    }

    public static void Save(IslandPlacement placement)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, placement.ToString());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // La posición elegida sigue activa durante esta sesión.
        }
    }
}
