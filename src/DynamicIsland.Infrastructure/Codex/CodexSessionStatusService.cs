using DynamicIsland.Core.Abstractions;
using DynamicIsland.Core.Models;
using System.Text;
using System.Text.Json;

namespace DynamicIsland.Infrastructure.Codex;

public sealed class CodexSessionStatusService : ICodexStatusService
{
    private const int TailByteCount = 2 * 1024 * 1024;
    private readonly string _sessionsDirectory;

    public CodexSessionStatusService()
    {
        string codexHome = Environment.GetEnvironmentVariable("CODEX_HOME")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex");
        _sessionsDirectory = Path.Combine(codexHome, "sessions");
    }

    public async Task<CodexSnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        string? latestSession = FindLatestSession();
        if (latestSession is null)
        {
            return Unavailable();
        }

        string[] lines = await ReadTailLinesAsync(latestSession, cancellationToken);
        return Parse(lines);
    }

    private string? FindLatestSession()
    {
        if (!Directory.Exists(_sessionsDirectory))
        {
            return null;
        }

        try
        {
            return Directory
                .EnumerateFiles(_sessionsDirectory, "*.jsonl", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault()
                ?.FullName;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task<string[]> ReadTailLinesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 16 * 1024,
            useAsync: true);

        long start = Math.Max(0, stream.Length - TailByteCount);
        stream.Seek(start, SeekOrigin.Begin);

        using StreamReader reader = new(stream, Encoding.UTF8, true, leaveOpen: false);
        if (start > 0)
        {
            _ = await reader.ReadLineAsync(cancellationToken);
        }

        List<string> lines = [];
        while (await reader.ReadLineAsync(cancellationToken) is string line)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    private static CodexSnapshot Parse(IEnumerable<string> lines)
    {
        CodexActivityStatus status = CodexActivityStatus.Idle;
        DateTimeOffset? lastTaskStartedAt = null;
        DateTimeOffset? lastTaskCompletedAt = null;
        DateTimeOffset? lastUsageAt = null;
        int? fiveHourRemaining = null;
        int? weeklyRemaining = null;
        DateTimeOffset? fiveHourResetsAt = null;
        DateTimeOffset? weeklyResetsAt = null;
        HashSet<string> pendingApprovalCalls = [];

        foreach (string line in lines)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                string? recordType = GetString(root, "type");
                DateTimeOffset? recordTimestamp = GetTimestamp(root);
                if (!root.TryGetProperty("payload", out JsonElement payload))
                {
                    continue;
                }

                string? payloadType = GetString(payload, "type");
                if (recordType == "event_msg")
                {
                    if (payloadType == "task_started")
                    {
                        lastTaskStartedAt = recordTimestamp;
                    }
                    else if (payloadType == "task_complete")
                    {
                        lastTaskCompletedAt = recordTimestamp;
                        pendingApprovalCalls.Clear();
                    }
                    else if (payloadType == "token_count" &&
                             payload.TryGetProperty("rate_limits", out JsonElement limits))
                    {
                        lastUsageAt = recordTimestamp;
                        ReadLimits(
                            limits,
                            ref fiveHourRemaining,
                            ref weeklyRemaining,
                            ref fiveHourResetsAt,
                            ref weeklyResetsAt);
                    }
                }
                else if (recordType == "response_item")
                {
                    if (payloadType == "custom_tool_call")
                    {
                        TrackPotentialApproval(payload, pendingApprovalCalls);
                    }
                    else if (payloadType == "custom_tool_call_output")
                    {
                        string? callId = GetString(payload, "call_id");
                        if (callId is not null)
                        {
                            pendingApprovalCalls.Remove(callId);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Un fragmento inicial incompleto es normal al leer solo la cola.
            }
        }

        bool taskStartedAfterComplete = lastTaskStartedAt is DateTimeOffset startedAt &&
            (lastTaskCompletedAt is null || startedAt > lastTaskCompletedAt);
        bool recentUsageAfterComplete = lastUsageAt is DateTimeOffset usageAt &&
            (lastTaskCompletedAt is null || usageAt > lastTaskCompletedAt);

        if (taskStartedAfterComplete || recentUsageAfterComplete)
        {
            status = CodexActivityStatus.Working;
        }

        if (pendingApprovalCalls.Count > 0)
        {
            status = CodexActivityStatus.AwaitingApproval;
        }

        return new CodexSnapshot(
            status,
            fiveHourRemaining,
            weeklyRemaining,
            fiveHourResetsAt,
            weeklyResetsAt);
    }

    private static void TrackPotentialApproval(
        JsonElement payload,
        HashSet<string> pendingApprovalCalls)
    {
        string? callId = GetString(payload, "call_id");
        string? name = GetString(payload, "name");
        string? input = GetString(payload, "input");

        bool asksForApproval = name == "request_user_input" ||
            (input?.Contains("require_escalated", StringComparison.OrdinalIgnoreCase) ?? false);
        if (callId is not null && asksForApproval)
        {
            pendingApprovalCalls.Add(callId);
        }
    }

    private static void ReadLimits(
        JsonElement limits,
        ref int? fiveHourRemaining,
        ref int? weeklyRemaining,
        ref DateTimeOffset? fiveHourResetsAt,
        ref DateTimeOffset? weeklyResetsAt)
    {
        if (limits.TryGetProperty("primary", out JsonElement primary))
        {
            fiveHourRemaining = RemainingPercent(primary);
            fiveHourResetsAt = ResetTime(primary);
        }

        if (limits.TryGetProperty("secondary", out JsonElement secondary))
        {
            weeklyRemaining = RemainingPercent(secondary);
            weeklyResetsAt = ResetTime(secondary);
        }
    }

    private static int? RemainingPercent(JsonElement window)
    {
        if (!window.TryGetProperty("used_percent", out JsonElement used) ||
            !used.TryGetDouble(out double value))
        {
            return null;
        }

        return Math.Clamp(100 - (int)Math.Round(value), 0, 100);
    }

    private static DateTimeOffset? ResetTime(JsonElement window)
    {
        if (!window.TryGetProperty("resets_at", out JsonElement reset) ||
            !reset.TryGetInt64(out long unixTime))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixTime);
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static DateTimeOffset? GetTimestamp(JsonElement element) =>
        GetString(element, "timestamp") is string timestamp &&
        DateTimeOffset.TryParse(timestamp, out DateTimeOffset value)
            ? value
            : null;

    private static CodexSnapshot Unavailable() => new(
        CodexActivityStatus.Unavailable,
        null,
        null,
        null,
        null);
}
