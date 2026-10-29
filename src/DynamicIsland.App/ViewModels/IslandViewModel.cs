using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core.Abstractions;
using DynamicIsland.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Storage.Streams;

namespace DynamicIsland.App.ViewModels;

public partial class IslandViewModel : ObservableObject
{
    private static readonly CultureInfo MexicanSpanish = CultureInfo.GetCultureInfo("es-MX");
    private readonly IClockService _clock;
    private readonly IWeatherService _weather;
    private readonly ICodexStatusService _codexStatus;
    private readonly IMediaSessionService _media;
    private TimeZoneInfo? _googleTimeZone;
    private byte[]? _currentArtworkBytes;

    [ObservableProperty]
    public partial IslandState State { get; set; } = IslandState.Compact;

    [ObservableProperty]
    public partial IslandScene Scene { get; set; } = IslandScene.Weather;

    [ObservableProperty]
    public partial string ClockText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime CalendarDate { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial bool ShowCalendar { get; set; } = true;

    [ObservableProperty]
    public partial string Location { get; set; } = "Loma Linda, Puebla";

    [ObservableProperty]
    public partial string Temperature { get; set; } = "—";

    [ObservableProperty]
    public partial string Condition { get; set; } = "Cargando clima";

    [ObservableProperty]
    public partial string Wind { get; set; } = "—";

    [ObservableProperty]
    public partial string FeelsLike { get; set; } = "—";

    [ObservableProperty]
    public partial string Humidity { get; set; } = "—";

    [ObservableProperty]
    public partial string WeatherSource { get; set; } = "Consultando Open-Meteo";

    [ObservableProperty]
    public partial string CodexStatusText { get; set; } = "Codex en espera";

    [ObservableProperty]
    public partial string CodexMessage { get; set; } = "Listo para la siguiente tarea.";

    [ObservableProperty]
    public partial int FiveHourRemainingPercent { get; set; }

    [ObservableProperty]
    public partial int WeeklyRemainingPercent { get; set; }

    [ObservableProperty]
    public partial string FiveHourRemainingText { get; set; } = "— restante";

    [ObservableProperty]
    public partial string WeeklyRemainingText { get; set; } = "— restante";

    [ObservableProperty]
    public partial string CodexDataSource { get; set; } = "Esperando datos locales de Codex";

    [ObservableProperty]
    public partial string MediaTitle { get; set; } = "Sin reproducción";

    [ObservableProperty]
    public partial string MediaArtist { get; set; } = "Abre Spotify o YouTube Music";

    [ObservableProperty]
    public partial string MediaPlaybackText { get; set; } = "Sin sesión multimedia";

    [ObservableProperty]
    public partial string MediaPlayPauseGlyph { get; set; } = "\uE768";

    public string MediaPlaybackActionName => MediaPlayPauseGlyph == "\uE769"
        ? "Pausar"
        : "Reproducir";

    [ObservableProperty]
    public partial int MediaProgressPercent { get; set; }

    [ObservableProperty]
    public partial string MediaElapsedText { get; set; } = "--:--";

    [ObservableProperty]
    public partial string MediaDurationText { get; set; } = "--:--";

    [ObservableProperty]
    public partial ImageSource? MediaArtworkSource { get; set; }

    [ObservableProperty]
    public partial bool IsTransientVisible { get; set; }

    [ObservableProperty]
    public partial string TransientTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TransientBody { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TransientGlyph { get; set; } = "\uE8C8";

    [ObservableProperty]
    public partial ImageSource? TransientImageSource { get; set; }

    public Visibility TransientImageVisibility =>
        TransientImageSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility TransientGlyphVisibility =>
        TransientImageSource is null ? Visibility.Visible : Visibility.Collapsed;

    public async Task SetTransientImageAsync(byte[]? bytes) =>
        TransientImageSource = await DecodeArtworkAsync(bytes);

    public Visibility MediaArtworkVisibility =>
        MediaArtworkSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility MediaArtworkFallbackVisibility =>
        MediaArtworkSource is null ? Visibility.Visible : Visibility.Collapsed;

    public IslandViewModel(
        IClockService clock,
        IWeatherService weather,
        ICodexStatusService codexStatus,
        IMediaSessionService media)
    {
        _clock = clock;
        _weather = weather;
        _codexStatus = codexStatus;
        _media = media;
        UpdateClock();
    }

    public Visibility WeatherSceneVisibility =>
        Scene == IslandScene.Weather ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WeatherCompactVisibility =>
        !IsTransientVisible ? WeatherSceneVisibility : Visibility.Collapsed;

    public Visibility CalendarDashboardVisibility =>
        Scene == IslandScene.Weather && ShowCalendar ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WeatherDetailsVisibility =>
        Scene == IslandScene.Weather && !ShowCalendar ? Visibility.Visible : Visibility.Collapsed;

    public string CalendarMonthText => CalendarDate.ToString("MMMM", MexicanSpanish).ToUpper(MexicanSpanish);

    public string CalendarYearText => CalendarDate.ToString("yyyy", MexicanSpanish);

    public string CalendarDayText => CalendarDate.Day.ToString(MexicanSpanish);

    public string CalendarWeekdayText => CalendarDate.ToString("dddd", MexicanSpanish);

    public string WeatherPageToggleName => ShowCalendar
        ? "Ver detalles del clima"
        : "Ver calendario";

    public string WeatherPageToggleGlyph => ShowCalendar ? "\uE753" : "\uE787";

    public Visibility CodexSceneVisibility =>
        Scene == IslandScene.Codex ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CodexCompactVisibility =>
        !IsTransientVisible ? CodexSceneVisibility : Visibility.Collapsed;

    public Visibility MediaSceneVisibility =>
        Scene == IslandScene.Media ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MediaCompactVisibility =>
        !IsTransientVisible ? MediaSceneVisibility : Visibility.Collapsed;

    public string ScenarioButtonName => Scene switch
    {
        IslandScene.Weather => "Cambiar al escenario de Codex",
        IslandScene.Codex => "Cambiar al escenario de música",
        IslandScene.Media => "Cambiar al escenario de cronómetro",
        _ => "Cambiar al escenario del clima"
    };

    public string ScenarioGlyph => Scene switch
    {
        IslandScene.Weather => "\uE943",
        IslandScene.Codex => "\uE8D6",
        IslandScene.Media => "\uE916",
        _ => "\uE706"
    };

    public string AccessibleState
    {
        get
        {
            if (IsTimerCompleted) return "Cronómetro. Tiempo terminado.";
            if (State == IslandState.Compact)
            {
                if (IsTransientVisible)
                {
                    return $"Isla contraída. {TransientTitle}: {TransientBody}";
                }

                return Scene switch
                {
                    IslandScene.Weather => $"Isla contraída. Hora {ClockText}",
                    IslandScene.Codex => $"Isla contraída. {CodexStatusText}. {FiveHourRemainingText}",
                    IslandScene.Timer => $"Isla contraída. Cronómetro {TimerText}. {TimerStatusText}",
                    _ => $"Isla contraída. {MediaPlaybackText}: {MediaTitle}"
                };
            }

            return Scene switch
            {
                IslandScene.Weather when ShowCalendar =>
                    $"Isla expandida. Calendario de {CalendarDate.ToString("MMMM yyyy", MexicanSpanish)}. Hoy {CalendarDate.ToString("D", MexicanSpanish)}. Pulsa el botón para ver el clima.",
                IslandScene.Weather =>
                    $"Isla expandida. Clima en {Location}: {Temperature}, {Condition}",
                IslandScene.Codex => $"Isla expandida. {CodexStatusText}. Cinco horas: {FiveHourRemainingText}. Semanal: {WeeklyRemainingText}",
                IslandScene.Timer => $"Isla expandida. Cronómetro {TimerText}. {TimerStatusText}. Ajusta minutos y segundos.",
                _ => $"Isla expandida. {MediaPlaybackText}: {MediaTitle}, {MediaArtist}"
            };
        }
    }

    public void UpdateClock()
    {
        DateTimeOffset currentTime = _clock.Now;
        if (_googleTimeZone is not null)
        {
            currentTime = TimeZoneInfo.ConvertTime(currentTime, _googleTimeZone);
        }

        ClockText = currentTime.ToString("hh:mm tt", MexicanSpanish).ToLower(MexicanSpanish);
        CalendarDate = currentTime.Date;
        OnPropertyChanged(nameof(AccessibleState));
    }

    public async Task RefreshWeatherAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            WeatherSnapshot? snapshot = await _weather.GetCurrentAsync(cancellationToken);
            if (snapshot is null)
            {
                WeatherSource = "Clima no disponible";
                if (Temperature == "—")
                {
                    Condition = "Sin datos del clima";
                }
                return;
            }

            Temperature = $"{Math.Round(snapshot.TemperatureCelsius):0}°";
            Condition = snapshot.Condition;
            FeelsLike = snapshot.FeelsLikeCelsius is double feelsLike
                ? $"{Math.Round(feelsLike):0}°"
                : "—";
            Humidity = snapshot.RelativeHumidityPercent is int humidity
                ? $"{humidity}%"
                : "—";
            Wind = snapshot.WindSpeedKilometersPerHour is double windSpeed
                ? $"{Math.Round(windSpeed):0} km/h {FormatCardinal(snapshot.WindDirection)}".TrimEnd()
                : "—";
            WeatherSource = "Open-Meteo · C.P. 72477";

            if (!string.IsNullOrWhiteSpace(snapshot.TimeZoneId))
            {
                _googleTimeZone = TimeZoneInfo.FindSystemTimeZoneById(snapshot.TimeZoneId);
                UpdateClock();
            }

            OnPropertyChanged(nameof(AccessibleState));
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
            TaskCanceledException or
            JsonException or
            TimeZoneNotFoundException or
            InvalidTimeZoneException)
        {
            WeatherSource = "Open-Meteo no disponible";
            if (Temperature == "—")
            {
                Condition = "Sin datos del clima";
            }
        }
    }

    public async Task RefreshCodexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            CodexSnapshot snapshot = await _codexStatus.GetCurrentAsync(cancellationToken);

            (CodexStatusText, CodexMessage) = snapshot.Status switch
            {
                CodexActivityStatus.Working =>
                    ("Codex trabajando", "Estoy trabajando en tu tarea."),
                CodexActivityStatus.AwaitingApproval =>
                    ("Codex pide aprobación", "Necesito tu aprobación para continuar."),
                CodexActivityStatus.Idle =>
                    ("Codex en espera", "Listo para la siguiente tarea."),
                _ =>
                    ("Codex no disponible", "Abre Codex para ver su actividad.")
            };

            FiveHourRemainingPercent = snapshot.FiveHourRemainingPercent ?? 0;
            WeeklyRemainingPercent = snapshot.WeeklyRemainingPercent ?? 0;
            FiveHourRemainingText = FormatRemaining(snapshot.FiveHourRemainingPercent);
            WeeklyRemainingText = FormatRemaining(snapshot.WeeklyRemainingPercent);
            CodexDataSource = snapshot.Status == CodexActivityStatus.Unavailable
                ? "Sin registro local"
                : "Actividad local de Codex";
            OnPropertyChanged(nameof(AccessibleState));
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            TaskCanceledException)
        {
            CodexStatusText = "Codex no disponible";
            CodexMessage = "No pude leer la actividad local.";
            CodexDataSource = "Sin conexión local";
        }
    }

    public async Task RefreshMediaAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            MediaSnapshot? snapshot = await _media.GetCurrentAsync(cancellationToken);
            if (snapshot is null)
            {
                MediaTitle = "Sin reproducción";
                MediaArtist = "Abre Spotify o YouTube Music";
                MediaPlaybackText = "Sin sesión multimedia";
                MediaPlayPauseGlyph = "\uE768";
                MediaProgressPercent = 0;
                MediaElapsedText = "--:--";
                MediaDurationText = "--:--";
                _currentArtworkBytes = null;
                MediaArtworkSource = null;
                OnPropertyChanged(nameof(AccessibleState));
                return;
            }

            MediaTitle = string.IsNullOrWhiteSpace(snapshot.Title)
                ? "Reproducción multimedia"
                : snapshot.Title;
            MediaArtist = string.IsNullOrWhiteSpace(snapshot.Artist)
                ? "Aplicación multimedia"
                : snapshot.Artist;
            MediaPlaybackText = snapshot.IsPlaying ? "Reproduciendo" : "En pausa";
            MediaPlayPauseGlyph = snapshot.IsPlaying ? "\uE769" : "\uE768";
            MediaProgressPercent = snapshot.Duration.TotalSeconds > 0
                ? Math.Clamp((int)Math.Round(
                    snapshot.Position.TotalSeconds / snapshot.Duration.TotalSeconds * 100),
                    0,
                    100)
                : 0;
            MediaElapsedText = snapshot.Duration.TotalSeconds > 0
                ? FormatMediaTime(TimeSpan.FromSeconds(Math.Clamp(
                    snapshot.Position.TotalSeconds, 0, snapshot.Duration.TotalSeconds)))
                : "--:--";
            MediaDurationText = snapshot.Duration.TotalSeconds > 0
                ? FormatMediaTime(snapshot.Duration)
                : "--:--";
            if (!ReferenceEquals(_currentArtworkBytes, snapshot.ArtworkBytes))
            {
                _currentArtworkBytes = snapshot.ArtworkBytes;
                MediaArtworkSource = await DecodeArtworkAsync(snapshot.ArtworkBytes);
            }
            OnPropertyChanged(nameof(AccessibleState));
        }
        catch (Exception exception) when (
            exception is COMException or UnauthorizedAccessException or
                InvalidOperationException or TaskCanceledException)
        {
            MediaPlaybackText = "Música no disponible";
            OnPropertyChanged(nameof(AccessibleState));
        }
    }

    public Task ToggleMediaPlaybackAsync() =>
        RunMediaControlAsync(() => _media.TryTogglePlaybackAsync());

    public Task SkipMediaNextAsync() =>
        RunMediaControlAsync(() => _media.TrySkipNextAsync());

    public Task SkipMediaPreviousAsync() =>
        RunMediaControlAsync(() => _media.TrySkipPreviousAsync());

    private static async Task<ImageSource?> DecodeArtworkAsync(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception exception) when (exception is COMException or IOException or ArgumentException)
        {
            return null;
        }
    }

    private async Task RunMediaControlAsync(Func<Task<bool>> action)
    {
        try
        {
            await action();
            await RefreshMediaAsync();
        }
        catch (Exception exception) when (
            exception is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            MediaPlaybackText = "Control no disponible";
            OnPropertyChanged(nameof(AccessibleState));
        }
    }

    private static string FormatCardinal(string? cardinal) => cardinal switch
    {
        "NORTH" => "N",
        "NORTH_NORTHEAST" => "NNE",
        "NORTHEAST" => "NE",
        "EAST_NORTHEAST" => "ENE",
        "EAST" => "E",
        "EAST_SOUTHEAST" => "ESE",
        "SOUTHEAST" => "SE",
        "SOUTH_SOUTHEAST" => "SSE",
        "SOUTH" => "S",
        "SOUTH_SOUTHWEST" => "SSO",
        "SOUTHWEST" => "SO",
        "WEST_SOUTHWEST" => "OSO",
        "WEST" => "O",
        "WEST_NORTHWEST" => "ONO",
        "NORTHWEST" => "NO",
        "NORTH_NORTHWEST" => "NNO",
        _ => string.Empty
    };

    private static string FormatRemaining(int? percent) =>
        percent is int value ? $"{value}% restante" : "— restante";

    private static string FormatMediaTime(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
        : $"{(int)time.TotalMinutes}:{time.Seconds:00}";

    public void SetExpanded(bool expanded)
    {
        IslandState targetState = expanded ? IslandState.Expanded : IslandState.Compact;
        if (State == targetState)
        {
            return;
        }

        if (expanded && Scene == IslandScene.Weather)
        {
            ShowCalendar = true;
        }

        State = targetState;
        OnPropertyChanged(nameof(AccessibleState));
    }

    [RelayCommand]
    private void NextScene()
    {
        Scene = Scene switch
        {
            IslandScene.Weather => IslandScene.Codex,
            IslandScene.Codex => IslandScene.Media,
            IslandScene.Media => IslandScene.Timer,
            _ => IslandScene.Weather
        };
    }

    [RelayCommand]
    private void ToggleWeatherPage() => ShowCalendar = !ShowCalendar;

    partial void OnCalendarDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(CalendarMonthText));
        OnPropertyChanged(nameof(CalendarYearText));
        OnPropertyChanged(nameof(CalendarDayText));
        OnPropertyChanged(nameof(CalendarWeekdayText));
        OnPropertyChanged(nameof(AccessibleState));
    }

    partial void OnShowCalendarChanged(bool value)
    {
        OnPropertyChanged(nameof(CalendarDashboardVisibility));
        OnPropertyChanged(nameof(WeatherDetailsVisibility));
        OnPropertyChanged(nameof(WeatherPageToggleName));
        OnPropertyChanged(nameof(WeatherPageToggleGlyph));
        OnPropertyChanged(nameof(AccessibleState));
    }

    partial void OnSceneChanged(IslandScene value)
    {
        OnPropertyChanged(nameof(WeatherSceneVisibility));
        OnPropertyChanged(nameof(WeatherCompactVisibility));
        OnPropertyChanged(nameof(CalendarDashboardVisibility));
        OnPropertyChanged(nameof(WeatherDetailsVisibility));
        OnPropertyChanged(nameof(CodexSceneVisibility));
        OnPropertyChanged(nameof(CodexCompactVisibility));
        OnPropertyChanged(nameof(MediaSceneVisibility));
        OnPropertyChanged(nameof(MediaCompactVisibility));
        OnPropertyChanged(nameof(TimerSceneVisibility));
        OnPropertyChanged(nameof(TimerCompactVisibility));
        OnPropertyChanged(nameof(ScenarioButtonName));
        OnPropertyChanged(nameof(ScenarioGlyph));
        OnPropertyChanged(nameof(AccessibleState));
    }

    partial void OnMediaArtworkSourceChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(MediaArtworkVisibility));
        OnPropertyChanged(nameof(MediaArtworkFallbackVisibility));
    }

    partial void OnMediaPlayPauseGlyphChanged(string value) =>
        OnPropertyChanged(nameof(MediaPlaybackActionName));

    partial void OnIsTransientVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(TimerCompactVisibility));
        OnPropertyChanged(nameof(WeatherCompactVisibility));
        OnPropertyChanged(nameof(CodexCompactVisibility));
        OnPropertyChanged(nameof(MediaCompactVisibility));
        OnPropertyChanged(nameof(AccessibleState));
    }

    partial void OnTransientTitleChanged(string value) =>
        OnPropertyChanged(nameof(AccessibleState));

    partial void OnTransientBodyChanged(string value) =>
        OnPropertyChanged(nameof(AccessibleState));

    partial void OnTransientImageSourceChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(TransientImageVisibility));
        OnPropertyChanged(nameof(TransientGlyphVisibility));
    }
}
