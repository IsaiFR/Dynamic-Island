using DynamicIsland.Core.Models;

int passed = 0;
Check("Duration limits", () =>
{
    var timer = new CountdownTimer();
    timer.SetDuration(0);
    Expect(timer.RemainingSeconds == 1);
    timer.SetDuration(int.MaxValue);
    Expect(timer.RemainingSeconds == 5999);
});
Check("Elapsed time survives delayed ticks", () =>
{
    var time = new ManualTimeProvider();
    var timer = new CountdownTimer(time);
    timer.SetDuration(60);
    timer.Toggle();
    time.Advance(12.4);
    timer.Update();
    Expect(timer.Status == CountdownStatus.Running && timer.RemainingSeconds == 48);
});
Check("Pause and resume preserve fractional seconds", () =>
{
    var time = new ManualTimeProvider();
    var timer = new CountdownTimer(time);
    timer.SetDuration(2);
    timer.Toggle();
    time.Advance(0.75);
    timer.Toggle();
    time.Advance(100);
    timer.Update();
    Expect(timer.Status == CountdownStatus.Paused && timer.RemainingSeconds == 2);
    timer.Toggle();
    time.Advance(1.24);
    timer.Update();
    Expect(timer.Status == CountdownStatus.Running && timer.RemainingSeconds == 1);
    time.Advance(0.01);
    timer.Update();
    Expect(timer.Status == CountdownStatus.Completed && timer.RemainingSeconds == 0);
});
Check("Completion lasts three seconds and restores selected duration", () =>
{
    var time = new ManualTimeProvider();
    var timer = new CountdownTimer(time);
    timer.SetDuration(1);
    timer.Toggle();
    time.Advance(1);
    timer.Update();
    time.Advance(2.999);
    timer.Update();
    Expect(timer.Status == CountdownStatus.Completed);
    time.Advance(0.001);
    timer.Update();
    Expect(timer.Status == CountdownStatus.Idle && timer.RemainingSeconds == 1);
    timer.Toggle();
    Expect(timer.Status == CountdownStatus.Running);
});
Check("Resume from sleep still presents the entire completion interval", () =>
{
    var time = new ManualTimeProvider();
    var timer = new CountdownTimer(time);
    timer.SetDuration(5);
    timer.Toggle();
    time.Advance(3600);
    timer.Update();
    Expect(timer.Status == CountdownStatus.Completed);
    time.Advance(2);
    timer.Update();
    Expect(timer.Status == CountdownStatus.Completed);
});
Check("Duration cannot change while running, paused or completed", () =>
{
    var time = new ManualTimeProvider();
    var timer = new CountdownTimer(time);
    timer.SetDuration(10);
    timer.Toggle();
    timer.SetDuration(20);
    Expect(timer.SelectedSeconds == 10);
    timer.Toggle();
    timer.SetDuration(30);
    Expect(timer.SelectedSeconds == 10);
    timer.Toggle();
    time.Advance(10);
    timer.Update();
    timer.SetDuration(40);
    timer.Toggle();
    Expect(timer.SelectedSeconds == 10 && timer.Status == CountdownStatus.Completed);
});
Check("Reset cancels the previous countdown", () =>
{
    var time = new ManualTimeProvider();
    var timer = new CountdownTimer(time);
    timer.SetDuration(15);
    timer.Toggle();
    time.Advance(5);
    timer.Reset();
    time.Advance(100);
    timer.Update();
    Expect(timer.Status == CountdownStatus.Idle && timer.RemainingSeconds == 15);
    timer.SetDuration(7);
    timer.Toggle();
    time.Advance(1);
    Expect(timer.RemainingSeconds == 6);
});
Console.WriteLine($"{passed} countdown checks passed.");

void Check(string name, Action action)
{
    action();
    passed++;
    Console.WriteLine($"PASS: {name}");
}
static void Expect(bool condition)
{
    if (!condition) throw new InvalidOperationException("Countdown assertion failed.");
}
sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _timestamp;
    public void Advance(double seconds) => _timestamp += (long)Math.Round(seconds * TimestampFrequency);
}
