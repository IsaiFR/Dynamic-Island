using DynamicIsland.App.ViewModels;
using DynamicIsland.Core.Abstractions;
using DynamicIsland.Infrastructure.Calendar;
using DynamicIsland.Infrastructure.Clock;
using DynamicIsland.Infrastructure.Codex;
using DynamicIsland.Infrastructure.Media;
using DynamicIsland.Infrastructure.Notifications;
using DynamicIsland.Infrastructure.Weather;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DynamicIsland.App;

public partial class App : Application
{
    private readonly ServiceProvider _services;
    private Window? _window;

    public App()
    {
        InitializeComponent();
        ServiceCollection services = new();
        services.AddSingleton<IClockService, SystemClockService>();
        services.AddSingleton<ICodexStatusService, CodexSessionStatusService>();
        services.AddSingleton<IMediaSessionService, WindowsMediaSessionService>();
        services.AddSingleton<INotificationService, WindowsNotificationService>();
        services.AddSingleton<ICalendarService, CalendarService>();
        services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        });
        services.AddSingleton(new OpenMeteoWeatherOptions(
            Latitude: 18.99887,
            Longitude: -98.21347,
            LocationName: "Loma Linda, Puebla"));
        services.AddSingleton<IWeatherService, OpenMeteoWeatherService>();
        services.AddTransient<IslandViewModel>();
        services.AddTransient<MainWindow>();

        _services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = _services.GetRequiredService<MainWindow>();
        ((MainWindow)_window).ConfigureWindow();
    }
}
