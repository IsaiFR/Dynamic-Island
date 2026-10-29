namespace DynamicIsland.Core.Models;

public sealed record SystemNotificationSnapshot(
    uint Id,
    string AppName,
    string Title,
    string Body,
    bool IsWhatsApp,
    byte[]? AppIconBytes,
    DateTimeOffset CreatedAt);
