using SchoolMS.Repository;

namespace SchoolMS.Services;

public record MaintenanceState(bool Enabled, string Message);

/// <summary>
/// Maintenance mode for the mobile app. The admin switches it on the website; every /api request is then answered with
/// 503 + the message (see Program.cs) and the app shows a full-screen "under maintenance" page until it is switched off.
/// Singleton: the state is cached a few seconds so the API does not hit the database on every request, and a failed
/// read keeps the last known state (it never blocks anyone because the settings table is missing).
/// </summary>
public class MaintenanceService(AppSettingsRepo repo)
{
    public const string KeyEnabled = "app.maintenance_enabled";
    public const string KeyMessage = "app.maintenance_message";
    public const string DefaultMessage = "RK Classes is under maintenance for a short while. Please try again shortly.";

    static readonly TimeSpan Ttl = TimeSpan.FromSeconds(5);
    MaintenanceState _state = new(false, DefaultMessage);
    DateTime _at = DateTime.MinValue;
    readonly object _lock = new();

    public MaintenanceState Get()
    {
        lock (_lock)
        {
            if (DateTime.UtcNow - _at < Ttl) return _state;
            try
            {
                var all = repo.GetAll();
                var msg = all.TryGetValue(KeyMessage, out var m) && !string.IsNullOrWhiteSpace(m) ? m.Trim() : DefaultMessage;
                _state = new MaintenanceState(all.TryGetValue(KeyEnabled, out var e) && e == "1", msg);
            }
            catch { /* keep the last known state */ }
            _at = DateTime.UtcNow;
            return _state;
        }
    }

    public (bool ok, string? error) Set(bool enabled, string? message, int by)
    {
        var msg = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message.Trim();
        if (msg.Length > 500) msg = msg[..500];
        try
        {
            repo.Set(KeyMessage, msg, by);
            repo.Set(KeyEnabled, enabled ? "1" : "0", by);
        }
        catch (Exception ex)
        {
            return (false, "Could not save the setting (" + ex.Message + "). Run SQL/66_AppSettings.sql on the database and try again.");
        }
        lock (_lock) { _state = new MaintenanceState(enabled, msg); _at = DateTime.UtcNow; }
        return (true, null);
    }
}
