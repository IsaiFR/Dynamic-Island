using DynamicIsland.App.Configuration;
using DynamicIsland.App.ViewModels;
using DynamicIsland.Core.Abstractions;
using DynamicIsland.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;

namespace DynamicIsland.App;

public sealed partial class MainWindow : Window
{
    private static readonly CultureInfo CalendarCulture = CultureInfo.GetCultureInfo("es-MX");
    private static readonly string[] WeekdayInitials = ["D", "L", "M", "M", "J", "V", "S"];
    private const int WeatherCompactWidth = 190;
    private const int CodexCompactWidth = 300;
    private const int MediaCompactWidth = 310;
    private const int TimerCompactWidth = 200;
    private const int TimerCompletionWidth = 280;
    private const int TransientCompactWidth = 340;
    private const int CompactHeight = 46;
    private const int ExpandedWidth = 430;
    private const int ExpandedHeight = 176;
    private const int ExpandedHoverMargin = 24;
    private static readonly TimeSpan HoverExitGrace = TimeSpan.FromMilliseconds(350);
    private const int DwmwaBorderColor = 34;
    private const uint DwmColorNone = 0xFFFFFFFE;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const nint WsCaption = 0x00C00000;
    private const nint WsThickFrame = 0x00040000;
    private const nint WsBorder = 0x00800000;
    private const nint WsDlgFrame = 0x00400000;
    private const nint WsExToolWindow = 0x00000080;
    private const nint WsExAppWindow = 0x00040000;
    private const nint WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint WmNcCalcSize = 0x0083;
    private const uint WmNcDestroy = 0x0082;
    private const uint WmMouseActivate = 0x0021;
    private const uint WmStyleChanging = 0x007C;
    private const nint MaNoActivate = 3;
    private const uint WmClipboardUpdate = 0x031D;
    private const nuint FrameSubclassId = 1;
    private static readonly nint HwndTopmost = -1;

    private readonly DispatcherTimer _clockTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private readonly DispatcherTimer _weatherTimer = new()
    {
        Interval = TimeSpan.FromMinutes(15)
    };

    private readonly DispatcherTimer _codexTimer = new()
    {
        Interval = TimeSpan.FromSeconds(5)
    };

    private readonly DispatcherTimer _mediaTimer = new()
    {
        Interval = TimeSpan.FromSeconds(2)
    };

    private readonly DispatcherTimer _transientTimer = new()
    {
        Interval = TimeSpan.FromSeconds(4)
    };

    private readonly INotificationService _notifications;
    private readonly DispatcherTimer _countdownTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(100)
    };
    private readonly DispatcherTimer _hoverTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(50)
    };
    private long? _hoverExitedAt;
    private readonly Dictionary<uint, string> _knownNotifications = [];
    private readonly Dictionary<string, int> _whatsAppNoticeCounts =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _notificationsReady;
    private bool _notificationRefreshRunning;
    private bool _notificationRefreshRequested;

    private AppWindow? _appWindow;
    private nint _windowHandle;
    private readonly WindowSubclassProcedure _windowSubclassProcedure;
    private bool _windowSubclassInstalled;
    private bool _clipboardListenerInstalled;
    private bool _positionFlyoutOpen;
    private IslandPlacement _placement = IslandPlacementSettings.Load();

    public IslandViewModel ViewModel { get; }

    public MainWindow(IslandViewModel viewModel, INotificationService notifications)
    {
        ViewModel = viewModel;
        _notifications = notifications;
        _windowSubclassProcedure = OnWindowMessage;
        InitializeComponent();
        // Window-level x:Bind normally initializes on Activated. This overlay is
        // shown without activation, so initialize before its first render to apply
        // scene visibility and subscribe to updates without waiting for a click.
        Bindings.Initialize();
        Root.Loaded += OnIslandLoaded;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
        _weatherTimer.Tick += OnWeatherTick;
        _weatherTimer.Start();
        _codexTimer.Tick += OnCodexTick;
        _codexTimer.Start();
        _mediaTimer.Tick += OnMediaTick;
        _mediaTimer.Start();
        _transientTimer.Tick += OnTransientTimerTick;
        _countdownTimer.Tick += OnCountdownTick;
        _hoverTimer.Tick += OnHoverTick;
        _notifications.NotificationsChanged += OnNotificationsChanged;
        Closed += OnClosed;
    }

    public void ConfigureWindow()
    {
        _windowHandle = WindowNative.GetWindowHandle(this);
        _appWindow = GetAppWindow();
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "DynamicIsland.ico");
        if (File.Exists(iconPath))
        {
            _appWindow.SetIcon(iconPath);
        }

        OverlappedPresenter presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        _appWindow.SetPresenter(presenter);
        ConfigureBackgroundWindow();
        RemoveNativeFrame();
        InstallFrameSubclass();
        _clipboardListenerInstalled = AddClipboardFormatListener(_windowHandle);
        SetSystemBorderColor();

        BuildCalendarGrid();
        ApplyIslandState();
        // Mostrar sin Activate(): el teclado sigue en la aplicación del usuario.
        _appWindow.Show(activateWindow: false);
        // WinUI puede restaurar estilos del marco durante la primera presentación.
        RemoveNativeFrame();
        ApplyIslandState();
        _ = ViewModel.RefreshWeatherAsync();
        _ = ViewModel.RefreshCodexAsync();
        _ = ViewModel.RefreshMediaAsync();
        _ = InitializeNotificationsAsync();
    }

    private void ConfigureBackgroundWindow()
    {
        if (_appWindow is null || _windowHandle == 0)
        {
            return;
        }

        _appWindow.IsShownInSwitchers = false;
        nint extendedStyle = GetWindowLongPtr(_windowHandle, GwlExStyle);
        // TOOLWINDOW oculta Alt+Tab; NOACTIVATE conserva el foco al hacer clic.
        extendedStyle = (extendedStyle | WsExToolWindow | WsExNoActivate) & ~WsExAppWindow;
        _ = SetWindowLongPtr(_windowHandle, GwlExStyle, extendedStyle);
    }

    private void OnIslandLoaded(object sender, RoutedEventArgs e)
    {
        RemoveNativeFrame();
        ApplyIslandState();
    }

    private void ApplyIslandState()
    {
        bool expanded = ViewModel.State == Core.Models.IslandState.Expanded;
        if (expanded)
        {
            _hoverTimer.Start();
        }
        else
        {
            _hoverTimer.Stop();
            _hoverExitedAt = null;
        }
        TimerCompletionOverlay.Visibility = ViewModel.IsTimerCompleted ? Visibility.Visible : Visibility.Collapsed;
        CompactTimerControl.Visibility = !expanded && !ViewModel.IsTransientVisible &&
            !ViewModel.IsTimerCompleted && ViewModel.Scene == IslandScene.Timer
            ? Visibility.Visible : Visibility.Collapsed;
        CompactIsland.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        TransientCompactContent.Visibility = !expanded && ViewModel.IsTransientVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
        CompactMediaControls.Visibility = !expanded && !ViewModel.IsTransientVisible &&
            ViewModel.Scene == Core.Models.IslandScene.Media
            ? Visibility.Visible
            : Visibility.Collapsed;
        ExpandedIsland.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;

        if (_appWindow is null)
        {
            return;
        }

        int width = expanded
            ? ExpandedWidth
            : ViewModel.IsTimerCompleted
                ? TimerCompletionWidth
            : ViewModel.IsTransientVisible
                ? TransientCompactWidth
            : ViewModel.Scene switch
            {
                Core.Models.IslandScene.Codex => CodexCompactWidth,
                Core.Models.IslandScene.Media => MediaCompactWidth,
                Core.Models.IslandScene.Timer => TimerCompactWidth,
                _ => WeatherCompactWidth
            };
        int height = expanded ? ExpandedHeight : CompactHeight;
        double scale = Root.XamlRoot?.RasterizationScale ?? 1d;
        int pixelWidth = (int)Math.Round(width * scale);
        int pixelHeight = (int)Math.Round(height * scale);

        DisplayArea displayArea = DisplayArea.GetFromWindowId(
            _appWindow.Id,
            DisplayAreaFallback.Primary);
        // Cada posición mantiene el centro de la cápsula en el centro de su tercio,
        // incluso al cambiar de tamaño, de escena o al mostrar una notificación.
        int section = _placement switch
        {
            IslandPlacement.Left => 0,
            IslandPlacement.Right => 2,
            _ => 1
        };
        int sectionCenter = (int)Math.Round(displayArea.WorkArea.Width * ((section + 0.5) / 3d));
        int relativeX = Math.Clamp(sectionCenter - pixelWidth / 2, 0,
            Math.Max(0, displayArea.WorkArea.Width - pixelWidth));
        int x = displayArea.WorkArea.X + relativeX;
        int y = displayArea.WorkArea.Y + 10;
        // Hover, avisos y cambios de escena no deben activar la ventana al moverla.
        _ = SetWindowPos(_windowHandle, HwndTopmost, x, y, pixelWidth, pixelHeight,
            SwpNoActivate);
        ApplyPillRegion(pixelWidth, pixelHeight);
        SetSystemBorderColor();
    }

    private void ApplyPillRegion(int width, int height)
    {
        if (_windowHandle == 0)
        {
            return;
        }

        nint region = CreateRoundRectRgn(0, 0, width + 1, height + 1, height, height);
        if (region == 0)
        {
            return;
        }

        // Cuando SetWindowRgn tiene éxito, Windows pasa a ser dueño de la región.
        if (SetWindowRgn(_windowHandle, region, 1) == 0)
        {
            _ = DeleteObject(region);
        }
    }

    private void SetSystemBorderColor()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        uint borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(
            _windowHandle,
            DwmwaBorderColor,
            ref borderColor,
            sizeof(uint));
    }

    private void RemoveNativeFrame()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        nint style = GetWindowLongPtr(_windowHandle, GwlStyle);
        nint framelessStyle = style & ~(WsCaption | WsThickFrame | WsBorder | WsDlgFrame);
        if (framelessStyle != style)
        {
            _ = SetWindowLongPtr(_windowHandle, GwlStyle, framelessStyle);
        }

        _ = SetWindowPos(
            _windowHandle,
            0,
            0,
            0,
            0,
            0,
            SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    private void InstallFrameSubclass()
    {
        _windowSubclassInstalled = SetWindowSubclass(
            _windowHandle,
            _windowSubclassProcedure,
            FrameSubclassId,
            0);
        if (!_windowSubclassInstalled)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        // Fuerza a Windows a recalcular el área cliente con el nuevo procedimiento.
        _ = SetWindowPos(
            _windowHandle,
            0,
            0,
            0,
            0,
            0,
            SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    private nint OnWindowMessage(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        if (message == WmStyleChanging && lParam != 0)
        {
            nint result = DefSubclassProc(windowHandle, message, wParam, lParam);
            // STYLESTRUCT contiene dos DWORD incluso en x64. Impedir que WinUI
            // restaure el marco o la activación en cambios de tamaño posteriores.
            int requestedStyle = Marshal.ReadInt32(lParam, sizeof(uint));
            if (wParam == GwlStyle)
            {
                requestedStyle &= ~(int)(WsCaption | WsThickFrame | WsBorder | WsDlgFrame);
            }
            else if (wParam == GwlExStyle)
            {
                requestedStyle = (requestedStyle | (int)(WsExToolWindow | WsExNoActivate)) & ~(int)WsExAppWindow;
            }

            Marshal.WriteInt32(lParam, sizeof(uint), requestedStyle);
            return result;
        }

        if (message == WmMouseActivate)
        {
            // No consumir el clic: los controles siguen funcionando sin robar el teclado.
            return MaNoActivate;
        }

        if (message == WmNcCalcSize && wParam != 0)
        {
            return 0;
        }

        if (message == WmClipboardUpdate && ViewModel.State == Core.Models.IslandState.Compact)
        {
            _ = ShowClipboardPreviewAsync();
        }

        if (message == WmNcDestroy)
        {
            _ = RemoveWindowSubclass(windowHandle, _windowSubclassProcedure, subclassId);
            _windowSubclassInstalled = false;
        }

        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private void OnClockTick(object? sender, object e) => ViewModel.UpdateClock();

    private void OnCountdownTick(object? sender, object e) => ViewModel.UpdateTimer();

    private async void OnWeatherTick(object? sender, object e) =>
        await ViewModel.RefreshWeatherAsync();

    private async void OnCodexTick(object? sender, object e) =>
        await ViewModel.RefreshCodexAsync();

    private async void OnMediaTick(object? sender, object e) =>
        await ViewModel.RefreshMediaAsync();

    private async Task ShowClipboardPreviewAsync()
    {
        try
        {
            DataPackageView content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text))
            {
                return;
            }

            string text = await content.GetTextAsync();
            if (ViewModel.State != Core.Models.IslandState.Compact || ViewModel.IsTimerCompleted || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string preview = string.Join(' ', text.Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
            if (preview.Length > 140)
            {
                preview = preview[..137] + "…";
            }

            ViewModel.TransientTitle = "Texto copiado";
            ViewModel.TransientBody = preview;
            ViewModel.TransientImageSource = null;
            ViewModel.TransientGlyph = "\uE8C8";
            ViewModel.IsTransientVisible = true;
            _transientTimer.Stop();
            _transientTimer.Start();
        }
        catch (Exception)
        {
            // El portapapeles puede estar ocupado o contener un formato inaccesible.
        }
    }

    private void OnTransientTimerTick(object? sender, object e)
    {
        _transientTimer.Stop();
        ViewModel.IsTransientVisible = false;
        ViewModel.TransientBody = string.Empty;
        ViewModel.TransientImageSource = null;
    }

    private async Task InitializeNotificationsAsync()
    {
        if (!await _notifications.RequestAccessAsync())
        {
            return;
        }

        await RefreshNotificationsAsync();
    }

    private void OnNotificationsChanged(object? sender, EventArgs e) =>
        Root.DispatcherQueue.TryEnqueue(() => _ = RefreshNotificationsAsync());

    private async Task RefreshNotificationsAsync()
    {
        _notificationRefreshRequested = true;
        if (_notificationRefreshRunning)
        {
            return;
        }

        _notificationRefreshRunning = true;
        try
        {
            while (_notificationRefreshRequested)
            {
                _notificationRefreshRequested = false;
                IReadOnlyList<SystemNotificationSnapshot> current =
                    await _notifications.GetCurrentAsync();
                List<SystemNotificationSnapshot> added = current
                    .Where(item => !_knownNotifications.TryGetValue(item.Id, out string? known) ||
                        known != NotificationFingerprint(item))
                    .OrderBy(item => item.CreatedAt)
                    .ToList();
                _knownNotifications.Clear();
                foreach (SystemNotificationSnapshot item in current)
                {
                    _knownNotifications[item.Id] = NotificationFingerprint(item);
                }

                HashSet<string> activeChats = current.Where(item => item.IsWhatsApp)
                    .Select(item => item.Title)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (string chat in _whatsAppNoticeCounts.Keys.ToList())
                {
                    if (!activeChats.Contains(chat))
                    {
                        _whatsAppNoticeCounts.Remove(chat);
                    }
                }

                if (!_notificationsReady)
                {
                    _notificationsReady = true;
                    foreach (IGrouping<string, SystemNotificationSnapshot> chat in current
                        .Where(item => item.IsWhatsApp)
                        .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase))
                    {
                        _whatsAppNoticeCounts[chat.Key] = chat.Count();
                    }
                    continue;
                }

                foreach (SystemNotificationSnapshot item in added.Where(item => item.IsWhatsApp))
                {
                    _whatsAppNoticeCounts[item.Title] =
                        _whatsAppNoticeCounts.GetValueOrDefault(item.Title) + 1;
                }

                if (ViewModel.State != IslandState.Compact || added.Count == 0)
                {
                    continue;
                }

                SystemNotificationSnapshot latest = added[^1];
                string title = latest.IsWhatsApp ? latest.Title : latest.AppName;
                string body = latest.IsWhatsApp
                    ? latest.Body
                    : string.IsNullOrWhiteSpace(latest.Body)
                        ? latest.Title
                        : $"{latest.Title} · {latest.Body}";
                if (latest.IsWhatsApp && !string.IsNullOrWhiteSpace(latest.Title))
                {
                    int pending = _whatsAppNoticeCounts.GetValueOrDefault(latest.Title);
                    if (pending > 1)
                    {
                        title = $"{latest.Title} ({pending})";
                        body = $"{pending} avisos pendientes · {latest.Body}";
                    }
                }

                await ShowNotificationAsync(title, body, latest.AppIconBytes);
            }
        }
        finally
        {
            _notificationRefreshRunning = false;
        }
    }

    private static string NotificationFingerprint(SystemNotificationSnapshot item) =>
        $"{item.AppName}\u001f{item.Title}\u001f{item.Body}";

    private async Task ShowNotificationAsync(string title, string body, byte[]? icon)
    {
        await ViewModel.SetTransientImageAsync(icon);
        if (ViewModel.State != IslandState.Compact || ViewModel.IsTimerCompleted)
        {
            return;
        }

        ViewModel.TransientGlyph = "\uE7F4";
        ViewModel.TransientTitle = string.IsNullOrWhiteSpace(title) ? "Notificación" : title;
        ViewModel.TransientBody = string.IsNullOrWhiteSpace(body) ? "Nuevo aviso" : body;
        ViewModel.IsTransientVisible = true;
        _transientTimer.Stop();
        _transientTimer.Start();
    }

    private void BuildCalendarGrid()
    {
        CalendarGrid.Children.Clear();
        CalendarGrid.ColumnDefinitions.Clear();
        CalendarGrid.RowDefinitions.Clear();

        for (int column = 0; column < 7; column++)
        {
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        }

        CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(17) });
        for (int row = 0; row < 6; row++)
        {
            CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(19.5) });
        }

        Brush primaryBrush = (Brush)Application.Current.Resources["IslandPrimaryTextBrush"];
        Brush secondaryBrush = (Brush)Application.Current.Resources["IslandSecondaryTextBrush"];
        Brush accentBrush = (Brush)Application.Current.Resources["IslandAccentBrush"];

        for (int column = 0; column < 7; column++)
        {
            TextBlock weekday = new()
            {
                Text = WeekdayInitials[column],
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = column is 0 or 6 ? accentBrush : secondaryBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(weekday, column);
            CalendarGrid.Children.Add(weekday);
        }

        DateTime today = ViewModel.CalendarDate.Date;
        int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        int firstWeekday = (int)new DateTime(today.Year, today.Month, 1).DayOfWeek;
        for (int day = 1; day <= daysInMonth; day++)
        {
            int slot = firstWeekday + day - 1;
            int row = 1 + slot / 7;
            int column = slot % 7;
            bool isToday = day == today.Day;
            TextBlock label = new()
            {
                Text = day.ToString(CalendarCulture),
                FontSize = 11,
                FontWeight = isToday
                    ? Microsoft.UI.Text.FontWeights.Bold
                    : Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = isToday
                    ? primaryBrush
                    : column is 0 or 6 ? accentBrush : primaryBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            FrameworkElement cell = label;
            if (isToday)
            {
                Border highlight = new()
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 105, 68, 145)),
                    CornerRadius = new CornerRadius(10),
                    Height = 20,
                    Width = 20,
                    Child = label,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                AutomationProperties.SetName(highlight, $"Hoy, {today.ToString("D", CalendarCulture)}");
                cell = highlight;
            }

            Grid.SetColumn(cell, column);
            Grid.SetRow(cell, row);
            CalendarGrid.Children.Add(cell);
        }
    }

    private void OnIslandPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hoverExitedAt = null;
        if (ViewModel.IsTimerCompleted) return;
        // Let the compact play button be clicked without moving it under the pointer.
        if (CompactTimerControl.Visibility == Visibility.Visible)
        {
            Windows.Foundation.Point point = e.GetCurrentPoint(CompactTimerControl).Position;
            if (point.X >= 0 && point.X <= CompactTimerControl.ActualWidth &&
                point.Y >= 0 && point.Y <= CompactTimerControl.ActualHeight) return;
        }
        ViewModel.SetExpanded(true);
    }

    private void OnTimerContentPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!ViewModel.IsTimerCompleted) ViewModel.SetExpanded(true);
    }

    private void OnIslandPointerExited(object sender, PointerRoutedEventArgs e)
    {
        CheckHoverBoundary();
    }

    private void OnHoverTick(object? sender, object e) => CheckHoverBoundary();

    private void CheckHoverBoundary()
    {
        // Native PointerExited follows the exact pill outline. Keep the expanded
        // controls available in a wider rectangle, including the curved corners.
        // Poll only while expanded so returning through the margin (outside the
        // native window) cancels dismissal even without a PointerEntered event.
        if (ViewModel.State != IslandState.Expanded || _positionFlyoutOpen ||
            ViewModel.IsTimerCompleted || IsCursorInsideIsland())
        {
            _hoverExitedAt = null;
            return;
        }

        long now = Stopwatch.GetTimestamp();
        _hoverExitedAt ??= now;
        if (Stopwatch.GetElapsedTime(_hoverExitedAt.Value, now) >= HoverExitGrace)
        {
            ViewModel.SetExpanded(false);
        }
    }

    private void OnPositionFlyoutOpening(object sender, object e)
    {
        _hoverExitedAt = null;
        _positionFlyoutOpen = true;
        LeftPositionItem.IsChecked = _placement == IslandPlacement.Left;
        CenterPositionItem.IsChecked = _placement == IslandPlacement.Center;
        RightPositionItem.IsChecked = _placement == IslandPlacement.Right;
    }

    private void OnPositionFlyoutClosed(object sender, object e)
    {
        _positionFlyoutOpen = false;
        // El menú puede quedar fuera de la cápsula. Al cerrarlo, volver a evaluar
        // el cursor, incluyendo el caso en que se acaba de mover la ventana.
        Root.DispatcherQueue.TryEnqueue(() =>
        {
            CheckHoverBoundary();
        });
    }

    private void OnPositionSelected(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem item &&
            Enum.TryParse(item.Tag?.ToString(), out IslandPlacement placement))
        {
            _placement = placement;
            IslandPlacementSettings.Save(placement);
            ApplyIslandState();
            PositionFlyout.Hide();
        }
    }

    private bool IsCursorInsideIsland()
    {
        if (_appWindow is null || !GetCursorPos(out CursorPoint cursor))
        {
            return false;
        }

        double x = cursor.X - _appWindow.Position.X;
        double y = cursor.Y - _appWindow.Position.Y;
        double width = _appWindow.Size.Width;
        double height = _appWindow.Size.Height;
        if (ViewModel.State == IslandState.Expanded)
        {
            double margin = ExpandedHoverMargin * (Root.XamlRoot?.RasterizationScale ?? 1d);
            return x >= -margin && x <= width + margin &&
                y >= -margin && y <= height + margin;
        }

        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return false;
        }

        double radius = height / 2;
        double centerX = Math.Clamp(x, radius, width - radius);
        return Math.Pow(x - centerX, 2) + Math.Pow(y - radius, 2) <= radius * radius;
    }

    private void OnScenarioSwitcherTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        ViewModel.NextSceneCommand.Execute(null);
        _ = ViewModel.RefreshCodexAsync();
        _ = ViewModel.RefreshMediaAsync();
    }

    private void OnWeatherPageToggleTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        ViewModel.ToggleWeatherPageCommand.Execute(null);
    }

    private void OnTimerToggleClicked(object sender, RoutedEventArgs e) => ViewModel.ToggleTimer();

    private void OnTimerResetClicked(object sender, RoutedEventArgs e) => ViewModel.ResetTimer();

    private void OnTimerAdjustClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        switch (button.Tag?.ToString())
        {
            case "MinutesUp": ViewModel.AdjustTimer(1, 0); break;
            case "MinutesDown": ViewModel.AdjustTimer(-1, 0); break;
            case "SecondsUp": ViewModel.AdjustTimer(0, 1); break;
            case "SecondsDown": ViewModel.AdjustTimer(0, -1); break;
        }
    }

    private async void OnMediaPreviousTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        await ViewModel.SkipMediaPreviousAsync();
    }

    private async void OnMediaPlayPauseTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        await ViewModel.ToggleMediaPlaybackAsync();
    }

    private async void OnMediaNextTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        await ViewModel.SkipMediaNextAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IslandViewModel.NeedsTimerTicks))
        {
            if (ViewModel.NeedsTimerTicks) _countdownTimer.Start();
            else _countdownTimer.Stop();
        }

        if (e.PropertyName == nameof(IslandViewModel.IsTimerCompleted))
        {
            TimerCompletionAnimation.Stop();
            if (ViewModel.IsTimerCompleted)
            {
                _transientTimer.Stop();
                ViewModel.IsTransientVisible = false;
                PositionFlyout.Hide();
                ApplyIslandState();
                if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
                    TimerCompletionAnimation.Begin();
            }
            else
            {
                if (ViewModel.State == IslandState.Expanded) CheckHoverBoundary();
                else ViewModel.SetExpanded(IsCursorInsideIsland());
                ApplyIslandState();
            }
        }
        if (e.PropertyName == nameof(IslandViewModel.State) &&
            ViewModel.State == Core.Models.IslandState.Expanded && ViewModel.IsTransientVisible)
        {
            _transientTimer.Stop();
            ViewModel.IsTransientVisible = false;
        }

        if (e.PropertyName == nameof(IslandViewModel.CalendarDate))
        {
            BuildCalendarGrid();
        }

        if (e.PropertyName is nameof(IslandViewModel.State) or nameof(IslandViewModel.Scene) or
            nameof(IslandViewModel.IsTransientVisible))
        {
            ApplyIslandState();
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (_clipboardListenerInstalled)
        {
            _ = RemoveClipboardFormatListener(_windowHandle);
            _clipboardListenerInstalled = false;
        }

        if (_windowSubclassInstalled)
        {
            _ = RemoveWindowSubclass(_windowHandle, _windowSubclassProcedure, FrameSubclassId);
            _windowSubclassInstalled = false;
        }

        _clockTimer.Stop();
        _clockTimer.Tick -= OnClockTick;
        _weatherTimer.Stop();
        _weatherTimer.Tick -= OnWeatherTick;
        _codexTimer.Stop();
        _codexTimer.Tick -= OnCodexTick;
        _mediaTimer.Stop();
        _mediaTimer.Tick -= OnMediaTick;
        _transientTimer.Stop();
        _transientTimer.Tick -= OnTransientTimerTick;
        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTick;
        _hoverTimer.Stop();
        _hoverTimer.Tick -= OnHoverTick;
        TimerCompletionAnimation.Stop();
        _notifications.NotificationsChanged -= OnNotificationsChanged;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Root.Loaded -= OnIslandLoaded;
    }

    private AppWindow GetAppWindow()
    {
        nint windowHandle = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        return AppWindow.GetFromWindowId(windowId);
    }

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int widthEllipse,
        int heightEllipse);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out CursorPoint point);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowRgn(nint windowHandle, nint region, int redraw);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AddClipboardFormatListener(nint windowHandle);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveClipboardFormatListener(nint windowHandle);

    [LibraryImport("gdi32.dll")]
    private static partial int DeleteObject(nint graphicsObject);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref uint attributeValue,
        int attributeSize);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr(nint windowHandle, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr(nint windowHandle, int index, nint value);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowSubclassProcedure(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        nint windowHandle,
        WindowSubclassProcedure procedure,
        nuint subclassId,
        nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        nint windowHandle,
        WindowSubclassProcedure procedure,
        nuint subclassId);

    [LibraryImport("comctl32.dll")]
    private static partial nint DefSubclassProc(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam);

}
