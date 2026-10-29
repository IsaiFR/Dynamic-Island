using CommunityToolkit.Mvvm.ComponentModel;
using DynamicIsland.Core.Models;
using Microsoft.UI.Xaml;

namespace DynamicIsland.App.ViewModels;

public partial class IslandViewModel
{
    private readonly CountdownTimer _countdown = new();
    private CountdownStatus _lastTimerStatus;
    private int _lastSelectedSeconds = 5 * 60;

    [ObservableProperty]
    public partial string TimerText { get; set; } = "05:00";

    [ObservableProperty]
    public partial bool IsTimerRunning { get; set; }

    [ObservableProperty]
    public partial bool IsTimerCompleted { get; set; }

    [ObservableProperty]
    public partial bool CanEditTimer { get; set; } = true;

    public bool NeedsTimerTicks => _countdown.Status is CountdownStatus.Running or CountdownStatus.Completed;
    public string TimerMinutesText => (_countdown.SelectedSeconds / 60).ToString("00");
    public string TimerSecondsText => (_countdown.SelectedSeconds % 60).ToString("00");
    public string TimerActionName => IsTimerRunning ? "Pausar cronómetro" :
        _countdown.Status == CountdownStatus.Paused ? "Continuar cronómetro" : "Iniciar cronómetro";
    public string TimerActionGlyph => IsTimerRunning ? "\uE769" : "\uE768";
    public string TimerStatusText => IsTimerCompleted ? "¡Tiempo terminado!" : IsTimerRunning ?
        "En curso" : _countdown.Status == CountdownStatus.Paused ? "En pausa" : "Elige la duración";
    public double TimerEditorOpacity => CanEditTimer ? 1 : 0.4;
    public Visibility TimerSceneVisibility => Scene == IslandScene.Timer ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TimerCompactVisibility => !IsTransientVisible ? TimerSceneVisibility : Visibility.Collapsed;

    public void AdjustTimer(int minutes, int seconds)
    {
        if (!CanEditTimer) return;
        // Each field is adjusted separately, so changing seconds does not wrap minutes.
        int selectedMinutes = Math.Clamp(_countdown.SelectedSeconds / 60 + minutes, 0, 99);
        int selectedSeconds = Math.Clamp(_countdown.SelectedSeconds % 60 + seconds, 0, 59);
        _countdown.SetDuration(selectedMinutes * 60 + selectedSeconds);
        RefreshTimerProperties();
    }

    public void ToggleTimer()
    {
        _countdown.Toggle();
        RefreshTimerProperties();
    }

    public void ResetTimer()
    {
        _countdown.Reset();
        RefreshTimerProperties();
    }

    public void UpdateTimer()
    {
        _countdown.Update();
        RefreshTimerProperties();
    }

    private void RefreshTimerProperties()
    {
        int remainingSeconds = _countdown.RemainingSeconds;
        TimerText = $"{remainingSeconds / 60:00}:{remainingSeconds % 60:00}";
        if (_lastTimerStatus == _countdown.Status && _lastSelectedSeconds == _countdown.SelectedSeconds) return;
        _lastTimerStatus = _countdown.Status;
        _lastSelectedSeconds = _countdown.SelectedSeconds;
        IsTimerRunning = _countdown.Status == CountdownStatus.Running;
        CanEditTimer = _countdown.Status == CountdownStatus.Idle;
        // Set last so window callbacks see a consistent state.
        IsTimerCompleted = _countdown.Status == CountdownStatus.Completed;
        OnPropertyChanged(nameof(TimerMinutesText));
        OnPropertyChanged(nameof(TimerSecondsText));
        OnPropertyChanged(nameof(TimerActionName));
        OnPropertyChanged(nameof(TimerActionGlyph));
        OnPropertyChanged(nameof(TimerStatusText));
        OnPropertyChanged(nameof(TimerEditorOpacity));
        OnPropertyChanged(nameof(NeedsTimerTicks));
        OnPropertyChanged(nameof(AccessibleState));
    }

    partial void OnTimerTextChanged(string value) => OnPropertyChanged(nameof(AccessibleState));
}
