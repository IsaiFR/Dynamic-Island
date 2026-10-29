using DynamicIsland.Core.Abstractions;
using DynamicIsland.Core.Models;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicIsland.Infrastructure.Notifications;

public sealed class WindowsNotificationService : INotificationService
{
    private UserNotificationListener? _listener;
    private readonly Dictionary<string, byte[]?> _icons = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? NotificationsChanged;

    public async ValueTask<bool> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            UserNotificationListener listener = UserNotificationListener.Current;
            UserNotificationListenerAccessStatus status = listener.GetAccessStatus();
            if (status != UserNotificationListenerAccessStatus.Allowed)
            {
                status = await listener.RequestAccessAsync();
            }

            if (status != UserNotificationListenerAccessStatus.Allowed)
            {
                return false;
            }

            _listener = listener;
            listener.NotificationChanged += OnNotificationChanged;
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or
            InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    public async ValueTask<IReadOnlyList<SystemNotificationSnapshot>> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_listener is null)
        {
            return [];
        }

        try
        {
            if (_listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
            {
                return [];
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or
            InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return [];
        }

        IReadOnlyList<UserNotification> source;
        try
        {
            source = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or
            InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return [];
        }

        List<SystemNotificationSnapshot> result = [];
        foreach (UserNotification notification in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string appName = notification.AppInfo.DisplayInfo.DisplayName;
                NotificationBinding? binding = notification.Notification.Visual.GetBinding(
                    KnownNotificationBindings.ToastGeneric);
                if (binding is null)
                {
                    continue;
                }

                IReadOnlyList<AdaptiveNotificationText> text = binding.GetTextElements();
                string title = text.Count > 0 ? text[0].Text : appName;
                string body = text.Count > 1
                    ? string.Join(" ", text.Skip(1).Select(item => item.Text))
                    : string.Empty;
                bool isWhatsApp = appName.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);
                byte[]? icon = await GetAppIconAsync(notification, appName);
                result.Add(new SystemNotificationSnapshot(
                    notification.Id, appName, title, body, isWhatsApp, icon,
                    notification.CreationTime));
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or
                InvalidOperationException or System.Runtime.InteropServices.COMException)
            {
                // Un aviso defectuoso no bloquea el resto.
            }
        }

        return result;
    }

    private void OnNotificationChanged(
        UserNotificationListener sender,
        UserNotificationChangedEventArgs args) =>
        NotificationsChanged?.Invoke(this, EventArgs.Empty);

    private async Task<byte[]?> GetAppIconAsync(UserNotification notification, string appName)
    {
        if (_icons.TryGetValue(appName, out byte[]? cached))
        {
            return cached;
        }

        try
        {
            IRandomAccessStreamReference logo = notification.AppInfo.DisplayInfo.GetLogo(
                new Size(48, 48));
            using IRandomAccessStreamWithContentType stream = await logo.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 1024 * 1024)
            {
                return null;
            }

            using var reader = new DataReader(stream.GetInputStreamAt(0));
            uint length = (uint)stream.Size;
            if (await reader.LoadAsync(length) != length)
            {
                return null;
            }

            byte[] bytes = new byte[length];
            reader.ReadBytes(bytes);
            _icons[appName] = bytes;
            return bytes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }
}
