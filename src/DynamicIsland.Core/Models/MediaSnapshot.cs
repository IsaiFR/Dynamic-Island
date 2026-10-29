namespace DynamicIsland.Core.Models;

public sealed record MediaSnapshot(
    string Title,
    string Artist,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    byte[]? ArtworkBytes = null);
