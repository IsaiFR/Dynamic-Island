using DynamicIsland.Core.Models;

namespace DynamicIsland.Core.Abstractions;

public interface ICodexStatusService
{
    Task<CodexSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default);
}
