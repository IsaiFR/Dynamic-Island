namespace DynamicIsland.Core.Models;

public enum CountdownStatus { Idle, Running, Paused, Completed }

/// <summary>A local countdown measured against elapsed time, independent of UI tick frequency.</summary>
public sealed class CountdownTimer(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private long _startedAt;
    private long _completedAt;
    private TimeSpan _remainingAtStart;

    public const int MaximumSeconds = 99 * 60 + 59;
    public int SelectedSeconds { get; private set; } = 5 * 60;
    public CountdownStatus Status { get; private set; }
    public int RemainingSeconds => Status switch
    {
        CountdownStatus.Running => (int)Math.Ceiling(Remaining.TotalSeconds),
        CountdownStatus.Paused => (int)Math.Ceiling(_remainingAtStart.TotalSeconds),
        CountdownStatus.Completed => 0,
        _ => SelectedSeconds
    };

    private TimeSpan Remaining => TimeSpan.FromTicks(Math.Max(0,
        (_remainingAtStart - _time.GetElapsedTime(_startedAt)).Ticks));

    public void SetDuration(int seconds)
    {
        if (Status == CountdownStatus.Idle)
        {
            SelectedSeconds = Math.Clamp(seconds, 1, MaximumSeconds);
        }
    }

    public void Toggle()
    {
        Update();
        if (Status == CountdownStatus.Completed) return;
        if (Status == CountdownStatus.Running)
        {
            _remainingAtStart = Remaining;
            Status = CountdownStatus.Paused;
            return;
        }

        if (Status == CountdownStatus.Idle)
        {
            _remainingAtStart = TimeSpan.FromSeconds(SelectedSeconds);
        }
        _startedAt = _time.GetTimestamp();
        Status = CountdownStatus.Running;
    }

    public void Reset() => Status = CountdownStatus.Idle;

    public void Update()
    {
        if (Status == CountdownStatus.Running && Remaining == TimeSpan.Zero)
        {
            // The full completion animation starts when the UI observes zero,
            // including after sleep or a delayed dispatcher tick.
            _completedAt = _time.GetTimestamp();
            Status = CountdownStatus.Completed;
        }
        else if (Status == CountdownStatus.Completed &&
            _time.GetElapsedTime(_completedAt) >= TimeSpan.FromSeconds(3))
        {
            Reset();
        }
    }
}
