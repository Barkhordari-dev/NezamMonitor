using System.IO;

namespace NezamMonitorPro.App.Services;

/// <summary>Simple frame-based navigation using dictionary of view factories.</summary>
public sealed class NavigationService
{
    private readonly Dictionary<string, Func<System.Windows.Controls.UserControl>> _factories = new();
    private System.Windows.Controls.Frame? _frame;
    private string _currentKey = "";

    public event Action<string>? Navigated;
    public string CurrentKey => _currentKey;

    public void Register(string key, Func<System.Windows.Controls.UserControl> factory)
        => _factories[key] = factory;

    public void SetFrame(System.Windows.Controls.Frame frame)
        => _frame = frame;

    public void NavigateTo(string key)
    {
        if (_frame is null) return;
        if (_factories.TryGetValue(key, out var factory))
        {
            try
            {
                _frame.Content = factory();
                _currentKey = key;
                Navigated?.Invoke(key);
            }
            catch (Exception ex)
            {
                LogError(key, ex);
                _currentKey = key;
            }
        }
    }

    /// <summary>Writes navigation errors to a log file next to the executable (hard to debug otherwise).</summary>
    private static void LogError(string key, Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
                      ?? AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(dir, "startup.log");
            File.AppendAllText(path,
                $"[{DateTime.Now}] NAVIGATION FAILED for '{key}': {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n");
        }
        catch { }
    }

    public bool CanNavigate(string key) => _factories.ContainsKey(key);
}
